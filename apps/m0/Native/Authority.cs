using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Kohami.M0.Native;
public sealed record OperationPreview(string Action,string Target,string DatasetId,string InstanceDbFingerprint,long SecurityEpoch,string PreviewHash,string Reason);
public sealed record NativePermit(string Id,OperationPreview Preview,DateTimeOffset IssuedAt,DateTimeOffset ExpiresAt,string Signature);
public static class PermitVerifier {
 public static readonly string[] Actions=["InitialOwnerPrepare","InitialOwnerActivate","CredentialRecovery","RenewDemoOwnerGrant","TransferDemoOwner","RotateKeyMaterial","RestoreSecurityCheckpoint"];
 public static byte[] Payload(NativePermit p)=>Encoding.UTF8.GetBytes(string.Join('\n',"kohami-native-permit-v1",p.Id,p.Preview.Action,p.Preview.Target,p.Preview.DatasetId,p.Preview.InstanceDbFingerprint,p.Preview.SecurityEpoch,p.Preview.PreviewHash,p.Preview.Reason,p.IssuedAt.ToUnixTimeSeconds(),p.ExpiresAt.ToUnixTimeSeconds()));
 public static string Hash(string canonicalPayload)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload))).ToLowerInvariant();
 public static void Verify(NativePermit p,OperationPreview expected,ReadOnlySpan<byte> publicKey,DateTimeOffset now) {
  if(!Actions.Contains(p.Preview.Action)||p.Preview!=expected||p.Preview.DatasetId!="demo_m0"||p.Preview.Target.Length is <1 or >160||p.Preview.Reason.Contains('\n')||!Guid.TryParse(p.Id,out _))throw new InvalidOperationException("PERMIT_SCOPE_INVALID");
  if(p.IssuedAt>now||p.ExpiresAt<=now||p.ExpiresAt-p.IssuedAt!=TimeSpan.FromMinutes(10))throw new InvalidOperationException("PERMIT_EXPIRED");
  using var key=ECDsa.Create();key.ImportSubjectPublicKeyInfo(publicKey,out var consumed);
  if(consumed!=publicKey.Length||!key.VerifyData(Payload(p),Convert.FromBase64String(p.Signature),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence))throw new InvalidOperationException("PERMIT_SIGNATURE_INVALID");
 }
}
public sealed record RestoreManifest(string SourceDataset,string SourceNamespace,string NewDatabase,string TargetNamespace,DateTimeOffset BackupCutoff,DateTimeOffset LedgerThrough,string LedgerHash,string[] KeyReferences);
public static class RestoreGuard {
 public static void Verify(RestoreManifest m,string expectedLedgerHash,IEnumerable<string> availableKeys) {
  if(m.SourceDataset!="demo_m0"||m.SourceNamespace!="kohami.m0.v1"||!System.Text.RegularExpressions.Regex.IsMatch(m.NewDatabase,@"\AKohamiM0Restore_[a-zA-Z0-9_]{1,32}\z")||m.TargetNamespace==m.SourceNamespace||!m.TargetNamespace.StartsWith("kohami.m0.restore.",StringComparison.Ordinal))throw new InvalidOperationException("RESTORE_SCOPE_INVALID");
  if(m.LedgerThrough<m.BackupCutoff||m.LedgerHash!=expectedLedgerHash||m.LedgerHash.Length!=64)throw new InvalidOperationException("RESTORE_LEDGER_MISSING");
  if(m.KeyReferences.Length<2||m.KeyReferences.Any(k=>!availableKeys.Contains(k)))throw new InvalidOperationException("RESTORE_KEY_MISSING");
 }
}
public static class HostJournal {
 public static void Append(string directory,string operationId,string phase,string previewHash) {
  Directory.CreateDirectory(directory);File.SetUnixFileMode(directory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
  var file=Path.Combine(directory,operationId+".jsonl");
  using var stream=new FileStream(file,FileMode.Append,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough);
  File.SetUnixFileMode(file,UnixFileMode.UserRead|UnixFileMode.UserWrite);
  using var writer=new StreamWriter(stream);writer.WriteLine(JsonSerializer.Serialize(new{operationId,phase,previewHash,at=DateTimeOffset.UtcNow}));writer.Flush();stream.Flush(true);
 }
}
