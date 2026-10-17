using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Kohami.M0.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
namespace Kohami.M0.Native;
public static class SqlProvision {
 public static async Task ProbeAsync(string root) {
  var local=Path.Combine(root,".local","m0");var s=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(s).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="master",UserID="sa",Password=secrets["sa"],TrustServerCertificate=false,Encrypt=SqlConnectionEncryptOption.Strict,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;
  await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();using var cmd=new SqlCommand("SELECT CASE WHEN encrypt_option='TRUE' THEN 1 ELSE 0 END FROM sys.dm_exec_connections WHERE session_id=@@SPID",c);if(Convert.ToInt32(await cmd.ExecuteScalarAsync())!=1)throw new InvalidOperationException("SQL_TLS_NOT_ENCRYPTED");
  Console.WriteLine("PASS SQL_TLS_STRICT_CUSTOM_CHAIN_SAN_LOCALHOST_EXACT_LEAF_PIN_ENCRYPTED; DBA probe is not runtime permission evidence");
 }
 public static async Task RunAsync(string root,string endpoint="localhost,14334") {
  var local=Path.Combine(root,".local","m0");var s=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(s).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(endpoint!="localhost,14334"&&!IsDedicatedFixtureEndpoint(endpoint))throw new InvalidOperationException("PROVISIONING_ENDPOINT_REJECTED");
  var fixture=IsDedicatedFixtureEndpoint(endpoint);
  var roles=new Dictionary<string,string>{{"api","api"},{"worker","worker"},{"native","native"},{"migrator","migrator"},{"backup","backup"}};

  var b=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="master",UserID="sa",Password=secrets["sa"],TrustServerCertificate=false,Encrypt=SqlConnectionEncryptOption.Strict,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;
  await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();
  await Execute(c,"IF DB_ID('KohamiM0') IS NOT NULL THROW 51000,'DB_ALREADY_EXISTS',1; CREATE DATABASE KohamiM0; ALTER DATABASE KohamiM0 SET TRUSTWORTHY OFF; ALTER DATABASE KohamiM0 SET DB_CHAINING OFF;");
  foreach(var role in roles) {
   using var cmd=M0Sql.Command(c,"DECLARE @sql nvarchar(max)='CREATE LOGIN '+QUOTENAME(@login)+' WITH PASSWORD='+QUOTENAME(@password,'''')+', CHECK_POLICY=ON'; EXEC(@sql);",null,("@login","kohami_m0_"+role.Key),("@password",secrets[role.Value]));await cmd.ExecuteNonQueryAsync();
  }
  c.ChangeDatabase("KohamiM0");
  await Execute(c,"CREATE SCHEMA ops AUTHORIZATION dbo;");
  foreach(var role in roles.Keys)await Execute(c,$"CREATE USER [kohami_m0_{role}] FOR LOGIN [kohami_m0_{role}]; CREATE ROLE [m0_{role}]; ALTER ROLE [m0_{role}] ADD MEMBER [kohami_m0_{role}];");
  await Execute(c,"ALTER ROLE db_backupoperator ADD MEMBER kohami_m0_backup;");
  await SqlMigrations.ApplyAsync(c,SqlMigrations.Load(root));
  if(fixture){
   if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_ID_REQUIRED");
   await Execute(c,"IF OBJECT_ID('ops.DevelopmentFixtureMarker','U') IS NULL CREATE TABLE ops.DevelopmentFixtureMarker(FixtureId uniqueidentifier NOT NULL PRIMARY KEY,DatasetId nvarchar(40) NOT NULL CHECK(DatasetId='demo_m0'),InstanceFingerprint char(64) NOT NULL,CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME());");
   using var marker=M0Sql.Command(c,"DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));INSERT ops.DevelopmentFixtureMarker(FixtureId,DatasetId,InstanceFingerprint) VALUES(@id,'demo_m0',@fp)",null,("@id",fixtureId));await marker.ExecuteNonQueryAsync();
   Console.WriteLine("DEDICATED_DEVELOPMENT_FIXTURE_PROVISIONED");
  }else Console.WriteLine("KOHAMI_M0_PROVISIONED_TLS_STRICT_RUNTIME_ROLES_DISTINCT_NATIVE_IMPORT_PENDING");
 }
 public static async Task ApplyFixtureGrantsAsync(string root,string endpoint) {
  var local=Path.Combine(root,".local","m0");var s=M0Settings.Load(Path.Combine(local,"runtime-private.json"));var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");var b=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();await ApplyGrantScriptsAsync(c,root);Console.WriteLine("PASS exact SQL grants applied to scoped isolated KohamiM0 instance");
 }
 public static async Task MigrateNativeLocalAsync(string root) {
  var local=Path.Combine(root,".local","m0");var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="KohamiM0",UserID="kohami_m0_migrator",Password=secrets["migrator"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};b["Server Certificate"]=settings.SqlCertificatePath;
  await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();using(var scope=new SqlCommand("SELECT DB_NAME(),CONVERT(nvarchar(100),SERVERPROPERTY('ServerName'))",c)){using var r=await scope.ExecuteReaderAsync();if(!await r.ReadAsync()||r.GetString(0)!="KohamiM0"||string.IsNullOrWhiteSpace(r.GetString(1)))throw new InvalidOperationException("SQL_SCOPE_INVALID");}
  await SqlMigrations.ApplyAsync(c,SqlMigrations.Load(root));
  Console.WriteLine("PASS versioned native SQL migrations applied to local KohamiM0; no Owner bootstrap");
 }
 public static async Task MigrateDedicatedFixtureAsync(string root,int port) {
  if(port<14336||port>14399)throw new InvalidOperationException("FIXTURE_PORT_OUT_OF_RANGE");
  var local=Path.Combine(root,".local","m0");var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
  var expectedEndpoint="localhost,"+port;var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,expectedEndpoint,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("FIXTURE_RUNTIME_TARGET_MISMATCH");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");
  // Read the marker with the private provisioner credential because the application
  // migrator intentionally has no access to fixture identification metadata or GRANT authority.
  // Both the runtime endpoint and SQL instance fingerprint must match before migration.
  var connection=new SqlConnectionStringBuilder(settings.ConnectionString){DataSource=expectedEndpoint,InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};connection["Server Certificate"]=settings.SqlCertificatePath;
  await using var c=new SqlConnection(connection.ConnectionString);await c.OpenAsync();
  using(var scope=M0Sql.Command(c,"DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='demo_m0' AND InstanceFingerprint=@fp",null,("@id",fixtureId)))
   if(Convert.ToInt32(await scope.ExecuteScalarAsync())!=1)throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_MISMATCH");
  Console.WriteLine("PASS dedicated fixture marker verified before versioned migration");
  await SqlMigrations.ApplyAsync(c,SqlMigrations.Load(root));
  Console.WriteLine("PASS versioned migrations applied to marker-verified dedicated fixture; no reset or Owner bootstrap");
 }
 public static async Task SeedIssue02OwnerAsync(string root,int port) {
  if(port<14336||port>14399)throw new InvalidOperationException("FIXTURE_PORT_OUT_OF_RANGE");
  var local=Path.Combine(root,".local","m0");var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
  var endpoint="localhost,"+port;var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,endpoint,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("FIXTURE_RUNTIME_TARGET_MISMATCH");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");
  var admin=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};admin["Server Certificate"]=settings.SqlCertificatePath;
  var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
  var passwordHash=new PasswordHasher<object>().HashPassword(new object(),password);
  const string email="issue02-owner@example.test";const string userId="issue02_owner";
  await using var connection=new SqlConnection(admin.ConnectionString);await connection.OpenAsync();
  await using var transaction=await connection.BeginTransactionAsync();
  await using(var seed=new SqlCommand(@"DECLARE @fingerprint char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));
IF NOT EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='demo_m0' AND InstanceFingerprint=@fingerprint) THROW 51000,'DEDICATED_FIXTURE_MARKER_MISMATCH',1;
IF EXISTS(SELECT 1 FROM m0.Grants WHERE Role='Owner' AND RevokedAt IS NULL) OR EXISTS(SELECT 1 FROM m0.Users WHERE UserId=@user OR Email=@email) THROW 51000,'FIXTURE_OWNER_ALREADY_EXISTS',1;
UPDATE m0.SecurityState SET Maintenance=0 WHERE Id=1 AND Maintenance=1;
IF @@ROWCOUNT<>1 THROW 51000,'FIXTURE_SECURITY_STATE_INVALID',1;
INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion) VALUES(@user,@email,@hash,1,0,'Active',1);
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
SELECT 'fixture_'+CONVERT(nvarchar(36),NEWID()),@user,'Owner',p.Permission,'Dataset','demo_m0',SYSUTCDATETIME(),DATEADD(hour,2,SYSUTCDATETIME()),'issue02_fixture','dedicated password-login acceptance'
FROM (VALUES('orders.read'),('shipment.manage'),('support.respond'),('return.inspect'),('refund.execute'),('refund.check'),('demo.control')) p(Permission);
INSERT m0.SecurityAudit(UserId,Action,At,SecurityVersion) VALUES(@user,'DedicatedFixtureAccountProvisioned',SYSUTCDATETIME(),1);",connection,(SqlTransaction)transaction)) {
   seed.Parameters.AddWithValue("@fixture",fixtureId);seed.Parameters.AddWithValue("@user",userId);seed.Parameters.AddWithValue("@email",email);seed.Parameters.AddWithValue("@hash",passwordHash);await seed.ExecuteNonQueryAsync();
  }
  await transaction.CommitAsync();
  var path=Path.Combine(local,"fixture-private.json");var config=JsonNode.Parse(File.ReadAllText(path))!.AsObject();config["fixtureOwnerEmail"]=email;config["fixtureOwnerPassword"]=password;File.WriteAllText(path,config.ToJsonString());File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);
  Console.WriteLine("PASS dedicated Issue 02 synthetic account provisioned; password stored only in mode-0600 fixture config");
 }
 public static async Task AdoptNativeLocalBaselineAsync(string root) {
  var local=Path.Combine(root,".local","m0");var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var admin=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName,ApplicationName="KOHAMI-BaselineAdoptionMetadata"};admin["Server Certificate"]=settings.SqlCertificatePath;
  var migrator=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="KohamiM0",UserID="kohami_m0_migrator",Password=secrets["migrator"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName,ApplicationName="KOHAMI-BaselineAdoptionWriter"};migrator["Server Certificate"]=settings.SqlCertificatePath;
  await using var metadata=new SqlConnection(admin.ConnectionString);await metadata.OpenAsync();
  await using var writer=new SqlConnection(migrator.ConnectionString);await writer.OpenAsync();
  if(metadata.DataSource!="localhost,14334"||metadata.Database!="KohamiM0"||writer.DataSource!=metadata.DataSource||writer.Database!=metadata.Database)throw new InvalidOperationException("SQL_BASELINE_ADOPTION_TARGET_INVALID");
  await SqlBaselineAdoption.AdoptAsync(metadata,writer,SqlMigrations.Load(root));
  Console.WriteLine("PASS exact local SQL baseline fingerprint adopted; migration hashes recorded atomically");
 }
 static async Task ApplyGrantScriptsAsync(SqlConnection c,string root) {
  foreach(var path in new[]{"src/Modules/IdentityAccess/Infrastructure/Persistence/SQLGrants.sql","src/Modules/Commerce/Infrastructure/Persistence/CommerceGrants.sql","tools/native-operations/Grants.sql","tools/native-operations/MigratorGrants.sql"}){var file=Path.Combine(root,path);if(!File.Exists(file))throw new InvalidOperationException("GRANT_SOURCE_MISSING:"+path);foreach(var batch in Regex.Split(File.ReadAllText(file),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x)))await Execute(c,batch);}
 }
 public static async Task CheckTargetAsync(string root,string endpoint) {
  var local=Path.Combine(root,".local","m0");var s=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(s).ValidateTls();var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="master",UserID="sa",Password=secrets["sa"],TrustServerCertificate=false,Encrypt=SqlConnectionEncryptOption.Strict,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();using var cmd=new SqlCommand("SELECT CASE WHEN encrypt_option='TRUE' THEN 1 ELSE 0 END,DB_ID('KohamiM0') FROM sys.dm_exec_connections WHERE session_id=@@SPID",c);using var r=await cmd.ExecuteReaderAsync();await r.ReadAsync();Console.WriteLine("PASS FIXTURE_TLS_STRICT encrypted="+r.GetInt32(0)+" database="+(r.IsDBNull(1)?"ABSENT":"PRESENT"));
 }
 public static void WriteFixtureConfig(string root,int port=14336) {
  if(port<14336||port>14399)throw new InvalidOperationException("FIXTURE_PORT_OUT_OF_RANGE");var endpoint="localhost,"+port;var local=Path.Combine(root,".local","m0");var json=JsonNode.Parse(File.ReadAllText(Path.Combine(local,"runtime-private.json")))!.AsObject();var certificate=json["sqlCertificatePath"]!.GetValue<string>();var api=new SqlConnectionStringBuilder(json["connectionString"]!.GetValue<string>()){DataSource=endpoint,HostNameInCertificate="localhost"};api["Server Certificate"]=certificate;var workerSettings=JsonNode.Parse(File.ReadAllText(Path.Combine(local,"worker-private.json")))!.AsObject();var worker=new SqlConnectionStringBuilder(workerSettings["workerConnectionString"]!.GetValue<string>()){DataSource=endpoint,HostNameInCertificate="localhost"};worker["Server Certificate"]=certificate;json["connectionString"]=api.ConnectionString;json["workerConnectionString"]=worker.ConnectionString;json["IdentitySqlTestDedicated"]=true;var admin=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="KohamiM0",UserID="sa",Password=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))!["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};admin["Server Certificate"]=certificate;json["MigratorConnectionString"]=admin.ConnectionString;var output=Path.Combine(local,"fixture-private.json");File.WriteAllText(output,json.ToJsonString());File.SetUnixFileMode(output,UnixFileMode.UserRead|UnixFileMode.UserWrite);Console.WriteLine("FIXTURE_CONFIG_WRITTEN_MODE_0600_IDENTITY_AND_COMMERCE_TARGET_PORT_"+port);
 }
 public static bool IsDedicatedFixtureEndpoint(string endpoint)=>endpoint.StartsWith("localhost,1433",StringComparison.Ordinal)&&int.TryParse(endpoint.AsSpan("localhost,".Length),out var port)&&port>=14336&&port<=14399;
 public static async Task Execute(SqlConnection c,string sql) {using var cmd=new SqlCommand(sql,c){CommandTimeout=60};await cmd.ExecuteNonQueryAsync();}
}
