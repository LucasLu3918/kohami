using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
namespace Kohami.Core;
public sealed class RuntimeSettings {
 public string DatasetId {get;set;}="kohami_local";
 public string Origin {get;set;}="https://localhost:3000";
 public string ConnectionString {get;set;}="";
 public string WorkerConnectionString {get;set;}="";
 public string SqlCertificatePath {get;set;}="";
 public string SqlCaPath {get;set;}="";
 public string SqlServerName {get;set;}="localhost";
 public string PrivateInboxPath {get;set;}="";
 public string IdentityNamespace {get;set;}="kohami.identity_access.v1";
 public string SessionRingPath {get;set;}="";
 public string SessionCertificatePath {get;set;}="";
 public string SessionPrivateKeyPath {get;set;}="";
 public bool ProtectSessionKeysWithCertificate {get;set;}=true;
 private static bool IsSupportedLocalOrigin(string origin) {
  if(!Uri.TryCreate(origin,UriKind.Absolute,out var uri) || uri.Scheme!=Uri.UriSchemeHttps ||
     uri.Host!="localhost" || uri.Port is <3000 or >3999 || uri.UserInfo.Length!=0 ||
     uri.Query.Length!=0 || uri.Fragment.Length!=0 || uri.AbsolutePath!="/") return false;
  return string.Equals(origin,$"https://localhost:{uri.Port}",StringComparison.Ordinal);
 }
 public static RuntimeSettings Load(string path) {
  var s=JsonSerializer.Deserialize<RuntimeSettings>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ?? throw new InvalidOperationException("CONFIG_INVALID");
  ApplyEnvironmentProfile(s);
  if(s.DatasetId!="kohami_local" || !IsSupportedLocalOrigin(s.Origin) || s.IdentityNamespace!="kohami.identity_access.v1") throw new InvalidOperationException("ENVIRONMENT_SCOPE_INVALID");
  return s;
 }
 static void ApplyEnvironmentProfile(RuntimeSettings s) {
  var profile=Environment.GetEnvironmentVariable("KOHAMI_ENVIRONMENT");
  if(string.IsNullOrEmpty(profile))return; // Dedicated legacy fixtures retain their explicit private JSON config.
  if(profile is not ("local" or "dev"))throw new InvalidOperationException("KOHAMI_ENVIRONMENT_INVALID");
  static string Required(string name)=>Environment.GetEnvironmentVariable(name) is {Length:>0} value?value:throw new InvalidOperationException("ENVIRONMENT_VALUE_REQUIRED:"+name);
  var host=Required("KOHAMI_SQL_HOST");
  if(!int.TryParse(Required("KOHAMI_SQL_PORT"),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var port)||port is <1 or >65535)throw new InvalidOperationException("KOHAMI_SQL_PORT_INVALID");
  if(profile=="local"&&(host!="localhost"||port!=14349||Required("KOHAMI_SQL_DATABASE")!="Kohami"))throw new InvalidOperationException("LOCAL_SQL_TARGET_MUST_BE_KOHAMI_SQL");
  var database=Required("KOHAMI_SQL_DATABASE");
  static string Build(string source,string host,int port,string database,string user,string password) {
   var b=new SqlConnectionStringBuilder(source){DataSource=$"{host},{port}",InitialCatalog=database,UserID=user,Password=password,Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false};
   return b.ConnectionString;
  }
  s.ConnectionString=Build(s.ConnectionString,host,port,database,Required("KOHAMI_API_SQL_USER"),Required("KOHAMI_API_SQL_PASSWORD"));
  s.WorkerConnectionString=Build(s.WorkerConnectionString,host,port,database,Required("KOHAMI_WORKER_SQL_USER"),Required("KOHAMI_WORKER_SQL_PASSWORD"));
 }
 public static X509Certificate2 LoadSessionCertificate(string certificatePath,string privateKeyPath) {
  try {return X509Certificate2.CreateFromPemFile(certificatePath,privateKeyPath);}
  catch {throw new InvalidOperationException("SESSION_CERTIFICATE_OR_KEY_INVALID");}
 }
}
public sealed class SqlRuntime(RuntimeSettings settings) {
 public RuntimeSettings Settings=>settings;
 public async Task<SqlConnection> OpenAsync(CancellationToken ct=default,bool worker=false) {
  ValidateTls();
  var builder=new SqlConnectionStringBuilder(worker?settings.WorkerConnectionString:settings.ConnectionString);
  if(builder.InitialCatalog!="Kohami" || builder.TrustServerCertificate) throw new InvalidOperationException("SQL_SCOPE_INVALID");
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
public sealed class SessionCrypto {
 public IDataProtectionProvider Sessions {get;}
 public SessionCrypto(IDataProtectionProvider sessions){Sessions=sessions;}
 public static SessionCrypto Load(RuntimeSettings s) {
  static IDataProtectionProvider Ring(string path,string certPath,string privateKeyPath,bool protectWithCertificate,string application) {
   if(!Directory.Exists(path)||!Directory.EnumerateFiles(path,"key-*.xml").Any())throw new InvalidOperationException("KEYSTORE_BLOCKED");
   var certificate=protectWithCertificate?RuntimeSettings.LoadSessionCertificate(certPath,privateKeyPath):null;
   var provider=DataProtectionProvider.Create(new DirectoryInfo(path),b=>{b.SetApplicationName(application);if(certificate is not null)b.ProtectKeysWithCertificate(certificate);b.DisableAutomaticKeyGeneration();});
   var sentinel=Path.Combine(path,"namespace.sentinel");
   if(!File.Exists(sentinel) || provider.CreateProtector("namespace-sentinel-v1").Unprotect(File.ReadAllText(sentinel))!=application)throw new InvalidOperationException("KEYSTORE_BLOCKED");
   certificate?.Dispose();
   return provider;
  }
  return new(Ring(s.SessionRingPath,s.SessionCertificatePath,s.SessionPrivateKeyPath,s.ProtectSessionKeysWithCertificate,s.IdentityNamespace+".session"));
 }
}
public sealed record Principal(string UserId,string SessionId,long SecurityVersion,string Stage,IReadOnlyList<string> Permissions,string Email="");
public sealed record ReconciliationAssignee(string UserId,long SecurityVersion);
public interface IReconciliationAssigneeResolver {
 Task<ReconciliationAssignee?> ResolveAsync(string email,string resourceId,CancellationToken ct=default);
}
public interface ISecurity {
 Task<Principal?> ResolveAsync(HttpContext context,CancellationToken ct=default);
 Task<Principal> RequirePermissionAsync(HttpContext context,string permission,string resourceKind,string resourceId,CancellationToken ct=default);
 Task<bool> CanDispatchAsync(string actorUserId,long securityVersion,string permission,string resourceKind,string resourceId,CancellationToken ct=default);
}
public interface ICatalogOwnerCreationAuthorization {
 Task<Principal> RequireCatalogOwnerCreateAsync(HttpContext context,CancellationToken ct=default);
}
public sealed class KohamiException(int status,string code):Exception(code){public int Status=>status;public string Code=>Message;}
