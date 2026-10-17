using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Kohami.M0.Core;
namespace Kohami.M0.Native;
public static class Cli {
 public static async Task Run(string[] args) {
  if(args.Length!=3||args[0] is not ("native" or "native-resume"))throw new InvalidOperationException("USAGE: native|native-resume <private-runtime-config> <nonsecret-host-config>");
  var settings=M0Settings.Load(args[1]);var host=JsonSerializer.Deserialize<NativeHostConfig>(File.ReadAllText(args[2]))??throw new InvalidOperationException("HOST_CONFIG_INVALID");
  var ops=new Operations(settings,host);
  if(args[0]=="native-resume") {
   Console.Error.WriteLine("After the target Owner signs in, reconcile an existing host checkpoint. The native confirmation will show its exact action and target. This never reapplies the operation.");
   var resume=JsonSerializer.Deserialize<Dictionary<string,string>>(Console.ReadLine()??"")??throw new InvalidOperationException("REQUEST_INVALID");
   if(resume.Count!=2||!resume.TryGetValue("operationId",out var operationId)||!Guid.TryParse(operationId,out _)||!resume.TryGetValue("previewHash",out var previewHash)||!System.Text.RegularExpressions.Regex.IsMatch(previewHash,@"\A[a-f0-9]{64}\z"))throw new InvalidOperationException("REQUEST_INVALID");
   await ops.ResumeAsync(operationId,previewHash);Console.WriteLine(JsonSerializer.Serialize(new{operationId,previewHash,status="Verified"}));return;
  }
  Console.Error.WriteLine("Request action, target, synthetic email, replacementUser, reason as JSON on stdin. Password is read locally with hidden TTY, never argv.");
  var request=JsonSerializer.Deserialize<Dictionary<string,string>>(Console.ReadLine()??"")??throw new InvalidOperationException("REQUEST_INVALID");
  var action=request["action"];var target=request["target"];var reason=request["reason"];
  var email=request.GetValueOrDefault("email");var replacement=request.GetValueOrDefault("replacementUser");
  if(!PermitVerifier.Actions.Contains(action)||target.Contains('\n')||reason.Contains('\n')||!System.Text.RegularExpressions.Regex.IsMatch(target,@"\A[a-zA-Z0-9_-]{1,100}\z"))throw new InvalidOperationException("REQUEST_INVALID");
  string? hash=null;
  if(action is "InitialOwnerPrepare" or "TransferDemoOwner") {
   if(email==null||!email.EndsWith("@example.test",StringComparison.Ordinal)||Console.IsInputRedirected)throw new InvalidOperationException("LOCAL_PASSWORD_TTY_REQUIRED");
   Console.Error.Write("本機合成 Owner 密碼（隱藏輸入）: ");var password=ReadSecret();
   if(password.Length is <15 or >256 ||new[]{"passwordpassword","123456789012345","kohamikohamikohami"}.Contains(password.ToLowerInvariant()))throw new InvalidOperationException("PASSWORD_POLICY");
   hash=new PasswordHasher<object>().HashPassword(new object(),password);Console.Error.WriteLine();
  }
  var preview=new OperationPreview(action,target,settings.DatasetId,host.InstanceDbFingerprint,await ops.InspectEpochAsync(),PermitVerifier.Hash(JsonSerializer.Serialize(new{Action=action,Target=target,email,passwordHash=hash,replacementUser=replacement})),reason);
  var permit=await ops.ConfirmAsync(preview);var id=await ops.ApplyAsync(permit,preview,email,hash,replacement);var status=action is "CredentialRecovery" or "TransferDemoOwner"?"AwaitingOwnerSignIn":action=="RenewDemoOwnerGrant"?"AwaitingOwnerSignIn":"Verified";Console.WriteLine(JsonSerializer.Serialize(new{operationId=id,previewHash=preview.PreviewHash,status}));
 }
 static string ReadSecret() {
  var value=new StringBuilder();while(true){var key=Console.ReadKey(true);if(key.Key==ConsoleKey.Enter)break;if(key.Key==ConsoleKey.Backspace){if(value.Length>0)value.Length--;continue;}if(!char.IsControl(key.KeyChar))value.Append(key.KeyChar);if(value.Length>256)throw new InvalidOperationException("PASSWORD_POLICY");}return value.ToString();
 }
}
