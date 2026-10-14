using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using Kohami.M0.Core;
using Microsoft.Data.SqlClient;
namespace Kohami.M0.Native;
public sealed record NativeHostConfig(string BrokerPath,string BrokerSha256,string IssuerPublicKeyPath,string JournalPath,string InstanceDbFingerprint,bool OsAuthorityEvidenceBound);
public sealed class Operations(M0Settings settings,NativeHostConfig host) {
 async Task<byte[]> Broker(string command,byte[]? input=null) {
  if(!host.OsAuthorityEvidenceBound||!OperatingSystem.IsMacOS()||!File.Exists(host.BrokerPath)||!File.Exists(host.IssuerPublicKeyPath))throw new InvalidOperationException("NATIVE_AUTHORITY_BLOCKED");
  var hash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(host.BrokerPath))).ToLowerInvariant();if(hash!=host.BrokerSha256)throw new InvalidOperationException("BROKER_SOURCE_CHANGED");
  var info=new ProcessStartInfo(host.BrokerPath){RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};info.ArgumentList.Add(command);
  using var process=Process.Start(info)??throw new InvalidOperationException("BROKER_UNAVAILABLE");
  if(input!=null)await process.StandardInput.BaseStream.WriteAsync(input);process.StandardInput.Close();
  using var output=new MemoryStream();await process.StandardOutput.BaseStream.CopyToAsync(output);await process.WaitForExitAsync();if(process.ExitCode!=0)throw new InvalidOperationException("NATIVE_CONFIRMATION_REJECTED");return output.ToArray();
 }
 public async Task<NativePermit> ConfirmAsync(OperationPreview preview) {
  if(preview.InstanceDbFingerprint!=host.InstanceDbFingerprint)throw new InvalidOperationException("NATIVE_SCOPE_INVALID");
  var now=DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());var p=new NativePermit(Guid.NewGuid().ToString(),preview,now,now.AddMinutes(10),"");
  var signature=await Broker("sign",PermitVerifier.Payload(p));return p with{Signature=Convert.ToBase64String(signature)};
 }
 async Task<SqlConnection> OpenAsync() {
  new M0Sql(settings).ValidateTls();var secret=await Broker("credential");
  var b=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="KohamiM0",UserID="kohami_m0_native",Password=System.Text.Encoding.UTF8.GetString(secret),Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};b["Server Certificate"]=settings.SqlCertificatePath;
  CryptographicOperations.ZeroMemory(secret);var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();return c;
 }
 public async Task<long> InspectEpochAsync() {
  await using var c=await OpenAsync();using var cmd=M0Sql.Command(c,"EXEC ops.Inspect");using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync()||r.GetString(r.GetOrdinal("Fingerprint"))!=host.InstanceDbFingerprint)throw new InvalidOperationException("INSTANCE_SCOPE_INVALID");return r.GetInt64(r.GetOrdinal("Epoch"));
 }
 public async Task<string> ApplyAsync(NativePermit permit,OperationPreview expected,string? email=null,string? passwordHash=null,string? replacementUser=null) {
  var pub=await File.ReadAllBytesAsync(host.IssuerPublicKeyPath);PermitVerifier.Verify(permit,expected,pub,DateTimeOffset.UtcNow);
  if(expected.InstanceDbFingerprint!=host.InstanceDbFingerprint)throw new InvalidOperationException("INSTANCE_SCOPE_INVALID");
  // Bind sensitive input hashes before exact native approval; never print password/hash.
  var body=JsonSerializer.Serialize(new{expected.Action,expected.Target,email,passwordHash,replacementUser});if(PermitVerifier.Hash(body)!=expected.PreviewHash)throw new InvalidOperationException("PREVIEW_PAYLOAD_CHANGED");
  if(expected.Action is "RotateKeyMaterial" or "RestoreSecurityCheckpoint")throw new InvalidOperationException("OFFLINE_CHECKPOINT_EVIDENCE_REQUIRED");
  HostJournal.Append(host.JournalPath,permit.Id,"Intent",expected.PreviewHash);
  await using var c=await OpenAsync();using var cmd=M0Sql.Command(c,"EXEC ops.Apply @PermitId,@OperationId,@Action,@Target,@Dataset,@Fingerprint,@PreviewHash,@Epoch,@IssuedAt,@ExpiresAt,@Email,@PasswordHash,@NewUserId",null,("@PermitId",Guid.Parse(permit.Id)),("@OperationId",Guid.Parse(permit.Id)),("@Action",expected.Action),("@Target",expected.Target),("@Dataset",expected.DatasetId),("@Fingerprint",expected.InstanceDbFingerprint),("@PreviewHash",expected.PreviewHash),("@Epoch",expected.SecurityEpoch),("@IssuedAt",permit.IssuedAt),("@ExpiresAt",permit.ExpiresAt),("@Email",email),("@PasswordHash",passwordHash),("@NewUserId",replacementUser));
  await cmd.ExecuteNonQueryAsync();HostJournal.Append(host.JournalPath,permit.Id,"Applied",expected.PreviewHash);
  if(expected.Action is not ("CredentialRecovery" or "RenewDemoOwnerGrant" or "TransferDemoOwner")) { await Reconcile(c,permit,permit.Id,expected,expected.SecurityEpoch+1);HostJournal.Append(host.JournalPath,permit.Id,"Verified",expected.PreviewHash); }
  return permit.Id;
 }
 async Task<(string Action,string Target,long Epoch)> InspectAuditAsync(string operationId,string hash) {
  await using var c=await OpenAsync();using var cmd=M0Sql.Command(c,"EXEC ops.InspectAudit @OperationId,@PreviewHash",null,("@OperationId",Guid.Parse(operationId)),("@PreviewHash",hash));using var r=await cmd.ExecuteReaderAsync();
  if(!await r.ReadAsync())throw new InvalidOperationException("HOST_CHECKPOINT_MISSING");return(r.GetString(r.GetOrdinal("Action")),r.GetString(r.GetOrdinal("Target")),r.GetInt64(r.GetOrdinal("NewEpoch")));
 }
 static async Task Reconcile(SqlConnection c,NativePermit permit,string operationId,OperationPreview preview,long epoch) {
  using var cmd=M0Sql.Command(c,"EXEC ops.Reconcile @PermitId,@OperationId,@Action,@Target,@Dataset,@Fingerprint,@PreviewHash,@PermitEpoch,@Epoch,@IssuedAt,@ExpiresAt",null,
   ("@PermitId",Guid.Parse(permit.Id)),("@OperationId",Guid.Parse(operationId)),("@Action",preview.Action),("@Target",preview.Target),("@Dataset",preview.DatasetId),("@Fingerprint",preview.InstanceDbFingerprint),("@PreviewHash",preview.PreviewHash),("@PermitEpoch",permit.Preview.SecurityEpoch),("@Epoch",epoch),("@IssuedAt",permit.IssuedAt),("@ExpiresAt",permit.ExpiresAt));
  await cmd.ExecuteNonQueryAsync();
 }
 public async Task ResumeAsync(string operationId,string hash) {
  // Recovery checks an existing Intent; it never re-applies the business/native action.
  if(!HostJournal.HasCheckpoint(host.JournalPath,operationId,hash))throw new InvalidOperationException("HOST_CHECKPOINT_MISSING");
  operationId=Guid.Parse(operationId).ToString();
  var epoch=await InspectEpochAsync(); // Checks that this journal belongs to the exact SQL instance/database.
  var audit=await InspectAuditAsync(operationId,hash);if(!NativeResumeEpoch.Allows(audit.Action,audit.Epoch,epoch))throw new InvalidOperationException("NATIVE_EPOCH_CHANGED");
  var preview=new OperationPreview(audit.Action,audit.Target,settings.DatasetId,host.InstanceDbFingerprint,epoch,hash,"Resume committed native operation reconciliation");
  var permit=await ConfirmAsync(preview);var pub=await File.ReadAllBytesAsync(host.IssuerPublicKeyPath);PermitVerifier.Verify(permit,preview,pub,DateTimeOffset.UtcNow);
  await using var c=await OpenAsync();await Reconcile(c,permit,operationId,preview,epoch);HostJournal.Append(host.JournalPath,operationId,"Verified",hash);
 }
}
