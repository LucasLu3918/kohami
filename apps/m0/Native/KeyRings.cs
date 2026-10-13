using Kohami.M0.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography.X509Certificates;
namespace Kohami.M0.Native;
public static class KeyRings {
 public static void Initialize(M0Settings settings) {
  Create(settings.SessionRingPath,settings.SessionCertificatePath,settings.SessionCertificatePassword,settings.IdentityNamespace+".session");
  Create(settings.TotpRingPath,settings.TotpCertificatePath,settings.TotpCertificatePassword,settings.IdentityNamespace+".totp");
  // Each freshly created ring receives its bound sentinel before M0Crypto.Load can validate it.
 }
 public static void ValidateSentinel(string path,IDataProtectionProvider provider,string binding) {
  var f=Path.Combine(path,"namespace.sentinel");if(!File.Exists(f)||provider.CreateProtector("namespace-sentinel-v1").Unprotect(File.ReadAllText(f))!=binding)throw new InvalidOperationException("KEYSTORE_BLOCKED");
 }
 static void WriteSentinel(string path,IDataProtectionProvider provider,string binding) {
  var f=Path.Combine(path,"namespace.sentinel");if(File.Exists(f))throw new InvalidOperationException("ALREADY_INITIALIZED");
  File.WriteAllText(f,provider.CreateProtector("namespace-sentinel-v1").Protect(binding));File.SetUnixFileMode(f,UnixFileMode.UserRead|UnixFileMode.UserWrite);
 }
 static void Create(string path,string cert,string password,string app) {
  if(Directory.Exists(path)&&Directory.EnumerateFileSystemEntries(path).Any())throw new InvalidOperationException("ALREADY_INITIALIZED");
  Directory.CreateDirectory(path);File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
  using var certificate=X509CertificateLoader.LoadPkcs12FromFile(cert,password,OperatingSystem.IsMacOS()?X509KeyStorageFlags.DefaultKeySet:X509KeyStorageFlags.EphemeralKeySet);
  var services=new ServiceCollection();services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(path)).SetApplicationName(app).ProtectKeysWithCertificate(certificate).DisableAutomaticKeyGeneration();
  using var service=services.BuildServiceProvider();service.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(90));
  foreach(var f in Directory.EnumerateFiles(path))File.SetUnixFileMode(f,UnixFileMode.UserRead|UnixFileMode.UserWrite);
  WriteSentinel(path,service.GetRequiredService<IDataProtectionProvider>(),app);
 }
}
