using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
namespace Kohami.M0.Core;
public sealed class M0Settings {
 public string DatasetId {get;set;}="demo_m0";
 public string Origin {get;set;}="https://localhost:3000";
 public string ConnectionString {get;set;}="";
 public string WorkerConnectionString {get;set;}="";
 public string SqlCertificatePath {get;set;}="";
 public string SqlCaPath {get;set;}="";
 public string SqlServerName {get;set;}="localhost";
 public string PrivateInboxPath {get;set;}="";
 public string IdentityNamespace {get;set;}="kohami.m0.v1";
 public string SessionRingPath {get;set;}="";
 public string TotpRingPath {get;set;}="";
 public string SessionCertificatePath {get;set;}="";
 public string SessionCertificatePassword {get;set;}="";
 public string TotpCertificatePath {get;set;}="";
 public string TotpCertificatePassword {get;set;}="";
 public static M0Settings Load(string path) {
  var s=JsonSerializer.Deserialize<M0Settings>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ?? throw new InvalidOperationException("CONFIG_INVALID");
  if(s.DatasetId!="demo_m0" || s.Origin!="https://localhost:3000" || s.IdentityNamespace!="kohami.m0.v1") throw new InvalidOperationException("ENVIRONMENT_SCOPE_INVALID");
  return s;
 }
}
public sealed class M0Sql(M0Settings settings) {
 public M0Settings Settings=>settings;
 public async Task<SqlConnection> OpenAsync(CancellationToken ct=default,bool worker=false) {
  ValidateTls();
  var builder=new SqlConnectionStringBuilder(worker?settings.WorkerConnectionString:settings.ConnectionString);
  if(builder.InitialCatalog!="KohamiM0" || builder.TrustServerCertificate) throw new InvalidOperationException("SQL_SCOPE_INVALID");
  builder.Encrypt=SqlConnectionEncryptOption.Strict;builder["Server Certificate"]=settings.SqlCertificatePath;builder.HostNameInCertificate=settings.SqlServerName;
  var c=new SqlConnection(builder.ConnectionString);await c.OpenAsync(ct);return c;
 }
 public void ValidateTls() {
  using var cert=X509CertificateLoader.LoadCertificateFromFile(settings.SqlCertificatePath);
  using var ca=X509CertificateLoader.LoadCertificateFromFile(settings.SqlCaPath);
  using var chain=new X509Chain();chain.ChainPolicy.TrustMode=X509ChainTrustMode.CustomRootTrust;chain.ChainPolicy.CustomTrustStore.Add(ca);chain.ChainPolicy.RevocationMode=X509RevocationMode.NoCheck;
  var san=cert.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
  if(!chain.Build(cert) || san is null || !san.EnumerateDnsNames().Contains(settings.SqlServerName,StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("SQL_CERTIFICATE_INVALID");
 }
 public static SqlCommand Command(SqlConnection c,string sql,SqlTransaction? tx=null,params (string Name,object? Value)[] values) {
  var cmd=new SqlCommand(sql,c,tx){CommandTimeout=20};foreach(var v in values)cmd.Parameters.AddWithValue(v.Name,v.Value??DBNull.Value);return cmd;
 }
}
public sealed class M0Crypto {
 public IDataProtectionProvider Sessions {get;}
 public IDataProtectionProvider Totp {get;}
 public M0Crypto(IDataProtectionProvider sessions,IDataProtectionProvider totp){Sessions=sessions;Totp=totp;}
 public static M0Crypto Load(M0Settings s) {
  static IDataProtectionProvider Ring(string path,string certPath,string password,string application) {
   if(!Directory.Exists(path)||!Directory.EnumerateFiles(path,"key-*.xml").Any())throw new InvalidOperationException("KEYSTORE_BLOCKED");
   var certificate=X509CertificateLoader.LoadPkcs12FromFile(certPath,password,OperatingSystem.IsMacOS()?X509KeyStorageFlags.DefaultKeySet:X509KeyStorageFlags.EphemeralKeySet);
   var provider=DataProtectionProvider.Create(new DirectoryInfo(path),b=>b.SetApplicationName(application).ProtectKeysWithCertificate(certificate).DisableAutomaticKeyGeneration());
   var sentinel=Path.Combine(path,"namespace.sentinel");
   if(!File.Exists(sentinel) || provider.CreateProtector("namespace-sentinel-v1").Unprotect(File.ReadAllText(sentinel))!=application)throw new InvalidOperationException("KEYSTORE_BLOCKED");
   return provider;
  }
  return new(Ring(s.SessionRingPath,s.SessionCertificatePath,s.SessionCertificatePassword,s.IdentityNamespace+".session"),Ring(s.TotpRingPath,s.TotpCertificatePath,s.TotpCertificatePassword,s.IdentityNamespace+".totp"));
 }
}
public sealed record M0Principal(string UserId,string SessionId,long SecurityVersion,string Stage,IReadOnlyList<string> Permissions);
public interface IM0Security {
 Task<M0Principal?> ResolveAsync(HttpContext context,CancellationToken ct=default);
 Task<M0Principal> RequirePermissionAsync(HttpContext context,string permission,string resourceKind,string resourceId,string? recentPurpose=null,CancellationToken ct=default);
 Task<bool> CanDispatchAsync(string actorUserId,long securityVersion,string permission,string resourceKind,string resourceId,CancellationToken ct=default);
}
public sealed class M0Exception(int status,string code):Exception(code){public int Status=>status;public string Code=>Message;}
