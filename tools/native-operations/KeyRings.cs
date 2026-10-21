using Kohami.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography.X509Certificates;
namespace Kohami.Native;
public static class KeyRings {
 public static void Initialize(RuntimeSettings settings) {
  Create(settings.SessionRingPath,settings.SessionCertificatePath,settings.SessionPrivateKeyPath,settings.ProtectSessionKeysWithCertificate,settings.IdentityNamespace+".session");
  // Each freshly created ring receives its bound sentinel before SessionCrypto.Load can validate it.
 }
 public static void RebindSessionRing(RuntimeSettings settings) {
  var path=settings.SessionRingPath;
  if(new DirectoryInfo(path).LinkTarget is not null||!Directory.Exists(path)||!Directory.EnumerateFiles(path,"key-*.xml").Any())throw new InvalidOperationException("KEYSTORE_BLOCKED");
  var sentinel=Path.Combine(path,"namespace.sentinel");var backup=sentinel+".previous";
  if(new FileInfo(sentinel).LinkTarget is not null||!File.Exists(sentinel)||File.Exists(backup))throw new InvalidOperationException("KEYSTORE_REBIND_BLOCKED");
  using var certificate=settings.ProtectSessionKeysWithCertificate?RuntimeSettings.LoadSessionCertificate(settings.SessionCertificatePath,settings.SessionPrivateKeyPath):null;
  var services=new ServiceCollection();var protection=services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(path)).SetApplicationName(settings.IdentityNamespace+".session");if(certificate is not null)protection.ProtectKeysWithCertificate(certificate);protection.DisableAutomaticKeyGeneration();
  using var provider=services.BuildServiceProvider();
  File.Move(sentinel,backup);
  try {
   WriteSentinel(path,provider.GetRequiredService<IDataProtectionProvider>(),settings.IdentityNamespace+".session");
  } catch {
   File.Delete(sentinel);File.Move(backup,sentinel);throw;
  }
 }
 public static void ValidateSentinel(string path,IDataProtectionProvider provider,string binding) {
  var f=Path.Combine(path,"namespace.sentinel");if(!File.Exists(f)||provider.CreateProtector("namespace-sentinel-v1").Unprotect(File.ReadAllText(f))!=binding)throw new InvalidOperationException("KEYSTORE_BLOCKED");
 }
 static void WriteSentinel(string path,IDataProtectionProvider provider,string binding) {
  var f=Path.Combine(path,"namespace.sentinel");if(File.Exists(f))throw new InvalidOperationException("ALREADY_INITIALIZED");
  File.WriteAllText(f,provider.CreateProtector("namespace-sentinel-v1").Protect(binding));File.SetUnixFileMode(f,UnixFileMode.UserRead|UnixFileMode.UserWrite);
 }
 static void Create(string path,string cert,string privateKey,bool protectWithCertificate,string app) {
  if(Directory.Exists(path)&&Directory.EnumerateFileSystemEntries(path).Any())throw new InvalidOperationException("ALREADY_INITIALIZED");
  Directory.CreateDirectory(path);File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
  X509Certificate2? certificate=null;
  if(protectWithCertificate)try {certificate=RuntimeSettings.LoadSessionCertificate(cert,privateKey);}
  catch (InvalidOperationException) {throw;}
  catch {throw new InvalidOperationException("SESSION_CERTIFICATE_INVALID");}
  using(certificate) {
   var services=new ServiceCollection();var protection=services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(path)).SetApplicationName(app);if(certificate is not null)protection.ProtectKeysWithCertificate(certificate);protection.DisableAutomaticKeyGeneration();
   using var service=services.BuildServiceProvider();
   try {service.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(90));}
   catch {throw new InvalidOperationException("SESSION_KEY_ENCRYPTION_FAILED");}
   foreach(var f in Directory.EnumerateFiles(path))File.SetUnixFileMode(f,UnixFileMode.UserRead|UnixFileMode.UserWrite);
   WriteSentinel(path,service.GetRequiredService<IDataProtectionProvider>(),app);
  }
 }
}
