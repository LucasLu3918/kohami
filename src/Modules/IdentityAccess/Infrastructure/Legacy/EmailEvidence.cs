using System.Text.Json;
namespace Kohami.Security;

public interface IEmailEvidenceSink { Task WriteAsync(string email, string challengeId, string proof, string requestNonce, DateTimeOffset expires, CancellationToken ct); }
public sealed class UnconfiguredEmailEvidenceSink : IEmailEvidenceSink
{
    public Task WriteAsync(string email, string challengeId, string proof, string requestNonce, DateTimeOffset expires, CancellationToken ct) => throw new Kohami.Core.KohamiException(503, "EVIDENCE_SINK_BLOCKED");
}
/// <summary>Private OS-side evidence, never mounted into SSR/static hosting. Values must not be logged or traced.</summary>
public sealed class PrivateEmailEvidenceSink(string directory) : IEmailEvidenceSink
{
    public async Task WriteAsync(string email, string challengeId, string proof, string requestNonce, DateTimeOffset expires, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows()) throw new InvalidOperationException("PRIVATE_INBOX_UNSUPPORTED");
        var path = Path.GetFullPath(directory); if (!Directory.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("PRIVATE_INBOX_BLOCKED");
        var permissions = File.GetUnixFileMode(path); if ((permissions & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0) throw new InvalidOperationException("PRIVATE_INBOX_BLOCKED");
        SecurityRules.Identifier(challengeId); var target = Path.Combine(path, challengeId + ".json");
        await using var output = new FileStream(target, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
        await JsonSerializer.SerializeAsync(output, new { email, challengeId, proof, requestNonce, expiresAt = expires }, cancellationToken: ct); await output.FlushAsync(ct);
    }
}
