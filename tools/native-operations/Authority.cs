using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Kohami.Native;
public sealed record OperationPreview(string Action,string Target,string DatasetId,string InstanceDbFingerprint,long SecurityEpoch,string PreviewHash,string Reason);
public sealed record NativePermit(string Id,OperationPreview Preview,DateTimeOffset IssuedAt,DateTimeOffset ExpiresAt,string Signature);
public static class PermitVerifier {
 public static readonly string[] Actions=["InitialOwnerPrepare","InitialOwnerActivate","CredentialRecovery","RenewDemoOwnerGrant","TransferDemoOwner","RotateKeyMaterial","RestoreSecurityCheckpoint"];
 public static byte[] Payload(NativePermit p)=>Encoding.UTF8.GetBytes(string.Join('\n',"kohami-native-permit-v1",p.Id,p.Preview.Action,p.Preview.Target,p.Preview.DatasetId,p.Preview.InstanceDbFingerprint,p.Preview.SecurityEpoch,p.Preview.PreviewHash,p.Preview.Reason,p.IssuedAt.ToUnixTimeSeconds(),p.ExpiresAt.ToUnixTimeSeconds()));
 public static string Hash(string canonicalPayload)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload))).ToLowerInvariant();
 public static void Verify(NativePermit p,OperationPreview expected,ReadOnlySpan<byte> publicKey,DateTimeOffset now) {
  if(!Actions.Contains(p.Preview.Action)||p.Preview!=expected||p.Preview.DatasetId!="kohami_local"||p.Preview.Target.Length is <1 or >160||p.Preview.Reason.IndexOfAny(['\n','\r'])>=0||p.Preview.Target.IndexOfAny(['\n','\r'])>=0||!System.Text.RegularExpressions.Regex.IsMatch(p.Preview.InstanceDbFingerprint,@"\A[a-f0-9]{64}\z")||!System.Text.RegularExpressions.Regex.IsMatch(p.Preview.PreviewHash,@"\A[a-f0-9]{64}\z")||!Guid.TryParse(p.Id,out _))throw new InvalidOperationException("PERMIT_SCOPE_INVALID");
  if(p.IssuedAt>now||p.ExpiresAt<=now||p.ExpiresAt-p.IssuedAt!=TimeSpan.FromMinutes(10))throw new InvalidOperationException("PERMIT_EXPIRED");
  using var key=ECDsa.Create();key.ImportSubjectPublicKeyInfo(publicKey,out var consumed);
  if(consumed!=publicKey.Length||!key.VerifyData(Payload(p),Convert.FromBase64String(p.Signature),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence))throw new InvalidOperationException("PERMIT_SIGNATURE_INVALID");
 }
}
public sealed record RestoreManifest(string SourceDataset,string SourceNamespace,string NewDatabase,string TargetNamespace,DateTimeOffset BackupCutoff,DateTimeOffset LedgerThrough,string LedgerHash,string[] KeyReferences);
public static class RestoreGuard {
 public static void Verify(RestoreManifest m,string expectedLedgerHash,IEnumerable<string> availableKeys) {
  if(m.SourceDataset!="kohami_local"||m.SourceNamespace!="kohami.identity_access.v1"||!System.Text.RegularExpressions.Regex.IsMatch(m.NewDatabase,@"\AKohamiRestore_[a-zA-Z0-9_]{1,32}\z")||m.TargetNamespace==m.SourceNamespace||!m.TargetNamespace.StartsWith("kohami.identity_access.restore.",StringComparison.Ordinal))throw new InvalidOperationException("RESTORE_SCOPE_INVALID");
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
 public static bool HasCheckpoint(string directory,string operationId,string previewHash) {
  if(!Guid.TryParse(operationId,out var parsed)||!System.Text.RegularExpressions.Regex.IsMatch(previewHash,@"\A[a-f0-9]{64}\z"))return false;
  var file=Path.Combine(directory,parsed+".jsonl");if(!File.Exists(file))return false;
  foreach(var line in File.ReadLines(file))try{using var entry=JsonDocument.Parse(line);var root=entry.RootElement;
   if(root.TryGetProperty("operationId",out var id)&&id.GetString()==parsed.ToString()&&root.TryGetProperty("phase",out var phase)&&phase.GetString() is ("Intent" or "Applied")&&root.TryGetProperty("previewHash",out var hash)&&hash.GetString()==previewHash)return true;
  }catch(JsonException){}
  return false;
 }
}
public static class NativeResumeEpoch {
 public static bool Allows(string action,long auditEpoch,long currentEpoch)=>
  auditEpoch==currentEpoch || action=="TransferDemoOwner"&&auditEpoch==currentEpoch-1;
}
