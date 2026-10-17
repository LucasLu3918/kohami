using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kohami.M0.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kohami.M0.Security;

public sealed class AccountInactivityWorker(M0Sql sql, TimeProvider clock, ILogger<AccountInactivityWorker> logger) : BackgroundService
{
    const int NoticeBatchSize = 50;
    readonly string leaseOwner = "inactivity_" + Guid.NewGuid().ToString("N");
    readonly PrivateAccountInactivityEmailEvidence emailEvidence = new(sql.Settings.PrivateInboxPath);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnce(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError("Account inactivity processing failed ({Type})", error.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken);
        }
    }

    public async Task ProcessOnce(CancellationToken cancellationToken = default)
    {
        await SuspendDueAccounts(clock.GetUtcNow(), cancellationToken);
        try
        {
            await PurgeExpired(clock.GetUtcNow(), cancellationToken);
            await emailEvidence.PurgeExpiredAsync(clock.GetUtcNow(), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            logger.LogError("Account inactivity evidence cleanup failed ({Type})", error.GetType().Name);
        }

        for (var count = 0; count < NoticeBatchSize; count++)
        {
            var notice = await ClaimNotice(clock.GetUtcNow(), cancellationToken);
            if (notice is null) break;

            await emailEvidence.WriteAsync(notice, clock.GetUtcNow(), cancellationToken);
            await MarkNoticeSent(notice.NoticeId, clock.GetUtcNow(), cancellationToken);
        }
        await SuspendDueAccounts(clock.GetUtcNow(), cancellationToken);
    }

    async Task PurgeExpired(DateTimeOffset now, CancellationToken cancellationToken)
    {
        _ = await InSecurityTransaction(async (connection, transaction) =>
        {
            await using var command = M0Sql.Command(connection, "m0.account_inactivity_purge_expired", transaction,
                ("@Now", now));
            command.CommandType = CommandType.StoredProcedure;
            _ = await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    async Task<InactiveAccountNotice?> ClaimNotice(DateTimeOffset now, CancellationToken cancellationToken) =>
        await InSecurityTransaction(async (connection, transaction) =>
        {
            await using var command = M0Sql.Command(connection, "m0.account_inactivity_claim_notice", transaction,
                ("@Now", now), ("@LeaseOwner", leaseOwner));
            command.CommandType = CommandType.StoredProcedure;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return new InactiveAccountNotice(reader.GetString(0), reader.GetString(1), reader.GetDateTimeOffset(2));
        }, cancellationToken);

    async Task MarkNoticeSent(string noticeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _ = await InSecurityTransaction(async (connection, transaction) =>
        {
            await using var command = M0Sql.Command(connection, "m0.account_inactivity_mark_notice_sent", transaction,
                ("@NoticeId", noticeId), ("@LeaseOwner", leaseOwner), ("@Now", now));
            command.CommandType = CommandType.StoredProcedure;
            _ = await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    async Task SuspendDueAccounts(DateTimeOffset now, CancellationToken cancellationToken)
    {
        _ = await InSecurityTransaction(async (connection, transaction) =>
        {
            await using var command = M0Sql.Command(connection, "m0.account_inactivity_suspend_due", transaction,
                ("@Now", now));
            command.CommandType = CommandType.StoredProcedure;
            _ = await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    async Task<T> InSecurityTransaction<T>(Func<SqlConnection, SqlTransaction, Task<T>> action, CancellationToken cancellationToken)
    {
        await using var connection = await sql.OpenAsync(cancellationToken, worker: true);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await using (var acquire = M0Sql.Command(connection, "m0.security_lock", transaction))
            {
                acquire.CommandType = CommandType.StoredProcedure;
                await acquire.ExecuteNonQueryAsync(cancellationToken);
            }
            var result = await action(connection, transaction);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            try { await transaction.RollbackAsync(cancellationToken); } catch { }
            throw;
        }
    }

    sealed record InactiveAccountNotice(string NoticeId, string Email, DateTimeOffset LastActivityAt);

    sealed class PrivateAccountInactivityEmailEvidence(string directory)
    {
        public async Task PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows()) throw new InvalidOperationException("PRIVATE_INBOX_UNSUPPORTED");
            var path = Path.GetFullPath(directory);
            if (!Directory.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("PRIVATE_INBOX_BLOCKED");
            var permissions = File.GetUnixFileMode(path);
            if ((permissions & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                               UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                throw new InvalidOperationException("PRIVATE_INBOX_BLOCKED");

            var cutoff = now.AddYears(-1);
            var temporaryCutoff = now.AddDays(-1);
            foreach (var temporaryPath in Directory.EnumerateFiles(path, "inactive_*.tmp"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var temporaryFile = new FileInfo(temporaryPath);
                if (temporaryFile.LinkTarget is not null || (File.GetAttributes(temporaryPath) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("PRIVATE_INBOX_BLOCKED");
                if (temporaryFile.LastWriteTimeUtc <= temporaryCutoff.UtcDateTime) File.Delete(temporaryPath);
            }

            foreach (var filePath in Directory.EnumerateFiles(path, "inactive_*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = new FileInfo(filePath);
                if (file.LinkTarget is not null || (File.GetAttributes(filePath) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("PRIVATE_INBOX_BLOCKED");
                var filePermissions = File.GetUnixFileMode(filePath);
                if ((filePermissions & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                                        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                    throw new InvalidOperationException("PRIVATE_INBOX_BLOCKED");
                try
                {
                    await using var input = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                        4096, FileOptions.Asynchronous);
                    using var document = await JsonDocument.ParseAsync(input, cancellationToken: cancellationToken);
                    if (document.RootElement.TryGetProperty("noticeId", out var id) &&
                        Regex.IsMatch(id.GetString() ?? "", "^inactive_[0-9a-f]{64}$", RegexOptions.CultureInvariant) &&
                        document.RootElement.TryGetProperty("recordedAt", out var recordedAt) &&
                        recordedAt.GetDateTimeOffset() <= cutoff)
                        File.Delete(filePath);
                }
                catch (JsonException) { }
                catch (FormatException) { }
            }
        }

        public async Task WriteAsync(InactiveAccountNotice notice, DateTimeOffset sentAt, CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows()) throw new InvalidOperationException("PRIVATE_INBOX_UNSUPPORTED");
            if (!Regex.IsMatch(notice.NoticeId, "^inactive_[0-9a-f]{64}$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("INACTIVITY_NOTICE_ID_INVALID");

            var path = Path.GetFullPath(directory);
            if (!Directory.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("PRIVATE_INBOX_BLOCKED");
            var permissions = File.GetUnixFileMode(path);
            if ((permissions & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                               UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                throw new InvalidOperationException("PRIVATE_INBOX_BLOCKED");

            var target = Path.Combine(path, notice.NoticeId + ".json");
            if (File.Exists(target))
            {
                await ValidateExisting(target, notice, cancellationToken);
                return;
            }

            var temporary = Path.Combine(path, notice.NoticeId + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await using (var output = new FileStream(temporary, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
                }))
                {
                    await JsonSerializer.SerializeAsync(output, new
                    {
                        mode = "LocalMockEmail",
                        noticeId = notice.NoticeId,
                        recipient = notice.Email,
                        subject = "KOHAMI account inactivity notice",
                        body = "Your KOHAMI account has been inactive for 24 months. Sign in within 30 days to keep it active. Your account data will be retained if login is suspended.",
                        lastActivityAt = notice.LastActivityAt,
                        recordedAt = sentAt
                    }, cancellationToken: cancellationToken);
                    await output.FlushAsync(cancellationToken);
                }

                try { File.Move(temporary, target); }
                catch (IOException) when (File.Exists(target)) { await ValidateExisting(target, notice, cancellationToken); }
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        static async Task ValidateExisting(string target, InactiveAccountNotice notice, CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows()) throw new InvalidOperationException("PRIVATE_INBOX_UNSUPPORTED");
            var file = new FileInfo(target);
            if (file.LinkTarget is not null || (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("PRIVATE_INBOX_BLOCKED");
            var permissions = File.GetUnixFileMode(target);
            if ((permissions & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                               UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                throw new InvalidOperationException("PRIVATE_INBOX_BLOCKED");
            await using var input = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            using var document = await JsonDocument.ParseAsync(input, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("noticeId", out var id) || id.GetString() != notice.NoticeId ||
                !document.RootElement.TryGetProperty("recipient", out var recipient) || recipient.GetString() != notice.Email)
                throw new InvalidOperationException("INACTIVITY_NOTICE_EVIDENCE_CONFLICT");
        }
    }
}
