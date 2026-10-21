using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Kohami.M0.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
namespace Kohami.M0.Native;
public static class SqlProvision {
 public static bool IsWallet03HttpPort(int port)=>port>=14336&&port<=14399&&port!=14349;
 public static bool IsLocalDemoPort(int port)=>port>=14336&&port<=14399&&port is not (14349 or 14350);
 static string FixtureLocal(string root) {
  var repo=Path.GetFullPath(root);var localRoot=Path.Combine(repo,".local");
  if(new DirectoryInfo(localRoot).LinkTarget is not null||!Directory.Exists(localRoot))throw new InvalidOperationException("LOCAL_ROOT_INVALID");
  var configured=Environment.GetEnvironmentVariable("KOHAMI_M0_LOCAL_DIR");var local=Path.GetFullPath(string.IsNullOrWhiteSpace(configured)?Path.Combine(localRoot,"m0"):Path.IsPathRooted(configured)?configured:Path.Combine(repo,configured));
  var relative=Path.GetRelativePath(localRoot,local);if(relative=="."||relative==".."||relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidOperationException("LOCAL_PROFILE_INVALID");
  var current=localRoot;foreach(var part in relative.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)){current=Path.Combine(current,part);if(new DirectoryInfo(current).LinkTarget is not null||!Directory.Exists(current))throw new InvalidOperationException("LOCAL_PROFILE_INVALID");}
  return local;
 }
 public static async Task ProbeAsync(string root) {
  var local=FixtureLocal(root);var s=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(s).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="master",UserID="sa",Password=secrets["sa"],TrustServerCertificate=false,Encrypt=SqlConnectionEncryptOption.Strict,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;
  await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();using var cmd=new SqlCommand("SELECT CASE WHEN encrypt_option='TRUE' THEN 1 ELSE 0 END FROM sys.dm_exec_connections WHERE session_id=@@SPID",c);if(Convert.ToInt32(await cmd.ExecuteScalarAsync())!=1)throw new InvalidOperationException("SQL_TLS_NOT_ENCRYPTED");
  Console.WriteLine("PASS SQL_TLS_STRICT_CUSTOM_CHAIN_SAN_LOCALHOST_EXACT_LEAF_PIN_ENCRYPTED; DBA probe is not runtime permission evidence");
 }
 public static async Task RunAsync(string root,string endpoint="localhost,14334") {
  var local=FixtureLocal(root);var s=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(s).ValidateTls();
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
  var local=FixtureLocal(root);var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="KohamiM0",UserID="kohami_m0_migrator",Password=secrets["migrator"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};b["Server Certificate"]=settings.SqlCertificatePath;
  await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();using(var scope=new SqlCommand("SELECT DB_NAME(),CONVERT(nvarchar(100),SERVERPROPERTY('ServerName'))",c)){using var r=await scope.ExecuteReaderAsync();if(!await r.ReadAsync()||r.GetString(0)!="KohamiM0"||string.IsNullOrWhiteSpace(r.GetString(1)))throw new InvalidOperationException("SQL_SCOPE_INVALID");}
  await SqlMigrations.ApplyAsync(c,SqlMigrations.Load(root));
  Console.WriteLine("PASS versioned native SQL migrations applied to local KohamiM0; no Owner bootstrap");
 }
 public static async Task MigrateKohamiSqlFixtureAsync(string root) {
  const string endpoint="localhost,14349";
  var local=Path.Combine(root,".local","kohami-sql","m0");
  var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
  var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,endpoint,StringComparison.OrdinalIgnoreCase)||runtime.InitialCatalog!="KohamiM0"||settings.DatasetId!="demo_m0")throw new InvalidOperationException("KOHAMI_SQL_TARGET_INVALID");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");
  var connection=new SqlConnectionStringBuilder(settings.ConnectionString){DataSource=endpoint,InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};connection["Server Certificate"]=settings.SqlCertificatePath;
  await using var c=new SqlConnection(connection.ConnectionString);await c.OpenAsync();
  using(var scope=M0Sql.Command(c,"DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='demo_m0' AND InstanceFingerprint=@fp",null,("@id",fixtureId)))
   if(Convert.ToInt32(await scope.ExecuteScalarAsync())!=1)throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_MISMATCH");
  Console.WriteLine("PASS exact kohami-sql:14349 marker verified before migration");
  await SqlMigrations.ApplyAsync(c,SqlMigrations.Load(root));
  using(var inspect=M0Sql.Command(c,"EXEC ops.Inspect"))using(var reader=await inspect.ExecuteReaderAsync()) {
   if(!await reader.ReadAsync()||reader.GetString(reader.GetOrdinal("DatasetId"))!="demo_m0"||reader.GetString(reader.GetOrdinal("DatabaseName"))!="KohamiM0")throw new InvalidOperationException("KOHAMI_SQL_TARGET_INVALID");
   Console.WriteLine("NATIVE_TARGET_BOUND endpoint=localhost,14349 database=KohamiM0 dataset=demo_m0 fingerprint="+reader.GetString(reader.GetOrdinal("Fingerprint"))+" epoch="+reader.GetInt64(reader.GetOrdinal("Epoch"))+" maintenance="+reader.GetBoolean(reader.GetOrdinal("Maintenance")));
  }
  Console.WriteLine("PASS versioned migrations applied to kohami-sql:14349; no data reset, Owner bootstrap or maintenance change");
 }
 public static async Task InitializeLocalDevelopmentAsync(string root) {
  const string endpoint="localhost,14349";
  const string userId="kohami_local_member";
  const string email="kohami-local-member@example.test";
  const long syntheticCredit=50000;
  if(OperatingSystem.IsWindows())throw new InvalidOperationException("LOCAL_DEVELOPMENT_REQUIRES_UNIX_PRIVATE_FILE_MODE");
  var local=Path.Combine(root,".local","kohami-sql","m0");
  var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
  var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,endpoint,StringComparison.OrdinalIgnoreCase)||runtime.InitialCatalog!="KohamiM0"||settings.DatasetId!="demo_m0")throw new InvalidOperationException("KOHAMI_SQL_TARGET_INVALID");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");
  var target=new SqlConnectionStringBuilder(settings.ConnectionString){DataSource=endpoint,InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};target["Server Certificate"]=settings.SqlCertificatePath;
  var credentialsPath=Path.Combine(local,"local-development-credentials.json");
  if(File.Exists(credentialsPath))throw new InvalidOperationException("LOCAL_DEVELOPMENT_ALREADY_INITIALIZED");
  var generatedPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
  var passwordHash=new PasswordHasher<object>().HashPassword(new object(),generatedPassword);
  await using var connection=new SqlConnection(target.ConnectionString);await connection.OpenAsync();
  await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync();
  var credentialsCreated=false;var commitStarted=false;
  try {
  await using(var seed=new SqlCommand(@"
DECLARE @fingerprint char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));
IF DB_NAME()<>'KohamiM0' OR (SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='demo_m0' AND InstanceFingerprint=@fingerprint)<>1 THROW 51000,'DEDICATED_FIXTURE_MARKER_MISMATCH',1;
IF NOT EXISTS(SELECT 1 FROM m0.SecurityState WITH(UPDLOCK,HOLDLOCK) WHERE Id=1 AND Maintenance=1 AND OwnerPrepared=0 AND BootstrapActivated=0) THROW 51000,'LOCAL_DEVELOPMENT_SECURITY_STATE_INVALID',1;
IF EXISTS(SELECT 1 FROM m0.Users) OR EXISTS(SELECT 1 FROM m0.Grants) THROW 51000,'LOCAL_DEVELOPMENT_REQUIRES_EMPTY_IDENTITY',1;
IF NOT EXISTS(SELECT 1 FROM ops.SchemaMigrations WHERE MigrationId='0014_wallet_payment_api_writes') THROW 51000,'LOCAL_DEVELOPMENT_SCHEMA_NOT_READY',1;
INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion) VALUES(@user,@email,@hash,1,0,'Active',1);
UPDATE m0.SecurityState SET Maintenance=0 WHERE Id=1 AND Maintenance=1;
IF @@ROWCOUNT<>1 THROW 51000,'LOCAL_DEVELOPMENT_GATE_CHANGE_FAILED',1;
EXEC m0commerce.wallet_synthetic_credit @UserId=@user,@SourceId='synthetic-test-credit:local-development-initial',@AmountTwd=@credit;
INSERT m0.SecurityAudit(UserId,Action,At,SecurityVersion) VALUES(@user,'LocalDevelopmentBootstrap',SYSUTCDATETIME(),1);",connection,transaction)) {
   seed.Parameters.AddWithValue("@fixture",fixtureId);seed.Parameters.AddWithValue("@user",userId);seed.Parameters.AddWithValue("@email",email);seed.Parameters.AddWithValue("@hash",passwordHash);seed.Parameters.AddWithValue("@credit",syntheticCredit);await seed.ExecuteNonQueryAsync();
  }
   Directory.CreateDirectory(local);
   var credentialOptions=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None,UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite};
   await using(var credentialFile=new FileStream(credentialsPath,credentialOptions)) {
    credentialsCreated=true;
    await JsonSerializer.SerializeAsync(credentialFile,new{mode="local-development",datasetId="demo_m0",email,password=generatedPassword,initialSyntheticCreditTwd=syntheticCredit});
    await credentialFile.WriteAsync(System.Text.Encoding.UTF8.GetBytes("\n"));
   }
   commitStarted=true;
   await transaction.CommitAsync();
  } catch {
   if(!commitStarted) {
    try { await transaction.RollbackAsync(); } catch { }
    if(credentialsCreated)File.Delete(credentialsPath);
   }
   else throw new InvalidOperationException("LOCAL_DEVELOPMENT_COMMIT_OUTCOME_UNKNOWN; preserve credentials and inspect exact fixture before retry");
   throw;
  }
  Console.WriteLine("PASS local development member created for marker-verified localhost:14349; synthetic credit NT$50,000; credentials stored mode-0600; no Owner or native authority created");
 }
 public static async Task MigrateDedicatedFixtureAsync(string root,int port) {
  if(port<14336||port>14399)throw new InvalidOperationException("FIXTURE_PORT_OUT_OF_RANGE");
  var local=FixtureLocal(root);var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
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
 public static async Task VerifyLocalDemoCatalogAsync(string root,int port) {
  if(!IsLocalDemoPort(port))throw new InvalidOperationException("FIXTURE_PORT_RESERVED");
  var local=FixtureLocal(root);var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
  var fixturePath=Path.Combine(local,"fixture-private.json");var config=JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()??throw new InvalidOperationException("FIXTURE_CONFIG_INVALID");
  if(config["KohamiDemoDedicated"]?.GetValue<bool>()!=true||config["port"]?.GetValue<int>()!=port)throw new InvalidOperationException("DEMO_FIXTURE_PROFILE_MISMATCH");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");
  var admin=new SqlConnectionStringBuilder{DataSource="localhost,"+port,InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};admin["Server Certificate"]=settings.SqlCertificatePath;
  await using var connection=new SqlConnection(admin.ConnectionString);await connection.OpenAsync();
  await using(var marker=new SqlCommand("DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='demo_m0' AND InstanceFingerprint=@fp",connection)){marker.Parameters.AddWithValue("@id",fixtureId);if(Convert.ToInt32(await marker.ExecuteScalarAsync())!=1)throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_MISMATCH");}
  await using var verify=new SqlCommand("""
SELECT (SELECT COUNT(*) FROM m0.CatalogProducts WHERE Dataset='demo_m0' AND State='Published' AND PublicSlug IS NOT NULL),
       (SELECT COUNT(*) FROM m0.CatalogProducts p WHERE Dataset='demo_m0' AND State='Published' AND
          (SELECT COUNT(DISTINCT locale) FROM OPENJSON(p.PublicJson,'$.translations') WITH(locale nvarchar(10) '$.locale') t WHERE t.locale IN('zh-TW','ja','en'))=3),
       (SELECT COUNT(*) FROM m0.CatalogProductSkus s JOIN m0commerce.Inventory i ON i.Dataset=s.Dataset AND i.Sku=s.SkuId WHERE s.Dataset='demo_m0' AND s.PublicIncluded=1 AND s.PublicActive=1 AND i.Active=1),
       (SELECT COUNT(*) FROM m0.Users), (SELECT COUNT(*) FROM m0.Grants)
""",connection);
  await using var result=await verify.ExecuteReaderAsync();await result.ReadAsync();
  if(result.GetInt32(0)!=15||result.GetInt32(1)!=15||result.GetInt32(2)!=45||result.GetInt32(3)!=0||result.GetInt32(4)!=0)throw new InvalidOperationException("DEMO_CATALOG_OR_EMPTY_IDENTITY_ASSERTION_FAILED");
  Console.WriteLine("PASS local demo fixture SQL: 15 published trilingual products, 45 active SKUs, empty identity/grants; marker verified; no protected fixture access");
 }
 public static async Task SeedIssue02OwnerAsync(string root,int port) {
  if(port<14336||port>14399)throw new InvalidOperationException("FIXTURE_PORT_OUT_OF_RANGE");
  var local=FixtureLocal(root);var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
  var endpoint="localhost,"+port;var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,endpoint,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("FIXTURE_RUNTIME_TARGET_MISMATCH");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");
  var admin=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};admin["Server Certificate"]=settings.SqlCertificatePath;
  var generatedPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
  var passwordHash=new PasswordHasher<object>().HashPassword(new object(),generatedPassword);
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
  var path=Path.Combine(local,"fixture-private.json");var config=JsonNode.Parse(File.ReadAllText(path))!.AsObject();config["fixtureOwnerEmail"]=email;config["fixtureOwnerPassword"]=generatedPassword;File.WriteAllText(path,config.ToJsonString());File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);
  Console.WriteLine("PASS dedicated Issue 02 synthetic account provisioned; password stored only in mode-0600 fixture config");
 }
 public static async Task ActivateWallet03HttpAsync(string root,int port) {
  if(!IsWallet03HttpPort(port))throw new InvalidOperationException(port==14349?"FIXTURE_PORT_RESERVED":"FIXTURE_PORT_OUT_OF_RANGE");
  var local=FixtureLocal(root);var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
  var endpoint="localhost,"+port;var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,endpoint,StringComparison.OrdinalIgnoreCase)||runtime.InitialCatalog!="KohamiM0"||settings.DatasetId!="demo_m0")throw new InvalidOperationException("FIXTURE_RUNTIME_TARGET_MISMATCH");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_ID_REQUIRED");
  var admin=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};admin["Server Certificate"]=settings.SqlCertificatePath;
  await using var connection=new SqlConnection(admin.ConnectionString);await connection.OpenAsync();
  await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
  try {
   var applied=new List<(string MigrationId,string SourceHash)>();
   await using(var migrations=new SqlCommand("SELECT MigrationId,SourceHash FROM ops.SchemaMigrations ORDER BY MigrationId",connection,transaction))
   await using(var reader=await migrations.ExecuteReaderAsync()){while(await reader.ReadAsync())applied.Add((reader.GetString(0),reader.GetString(1).Trim()));}
   var current=SqlMigrations.Load(root).Select(x=>(x.Id,x.SourceHash)).ToArray();
   if(!M0SchemaContract.IsCurrent(applied)||!current.SequenceEqual(applied))throw new InvalidOperationException("WALLET03_HTTP_SCHEMA_NOT_CURRENT");
   using(var activate=M0Sql.Command(connection,"""
DECLARE @fingerprint char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));
IF DB_NAME()<>'KohamiM0' OR (SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='demo_m0' AND InstanceFingerprint=@fingerprint)<>1 THROW 51000,'DEDICATED_FIXTURE_MARKER_MISMATCH',1;
IF NOT EXISTS(SELECT 1 FROM m0.SecurityState WITH(UPDLOCK,HOLDLOCK) WHERE Id=1 AND Maintenance=1 AND OwnerPrepared=0 AND BootstrapActivated=0) THROW 51000,'WALLET03_HTTP_SECURITY_STATE_INVALID',1;
IF EXISTS(SELECT 1 FROM m0.Users WITH(UPDLOCK,HOLDLOCK)) OR EXISTS(SELECT 1 FROM m0.Grants WITH(UPDLOCK,HOLDLOCK)) THROW 51000,'WALLET03_HTTP_REQUIRES_EMPTY_IDENTITY',1;
UPDATE m0.SecurityState SET Maintenance=0 WHERE Id=1 AND Maintenance=1;
IF @@ROWCOUNT<>1 THROW 51000,'WALLET03_HTTP_ACTIVATION_FAILED',1;
""",transaction,("@fixture",fixtureId)))await activate.ExecuteNonQueryAsync();
   await transaction.CommitAsync();
  } catch {await transaction.RollbackAsync();throw;}
  Console.WriteLine("PASS wallet03-http writable gate enabled on marker-verified fresh fixture; no user, Owner, or grants created");
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
  var local=FixtureLocal(root);var s=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(s).ValidateTls();var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="master",UserID="sa",Password=secrets["sa"],TrustServerCertificate=false,Encrypt=SqlConnectionEncryptOption.Strict,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();using var cmd=new SqlCommand("SELECT CASE WHEN encrypt_option='TRUE' THEN 1 ELSE 0 END,DB_ID('KohamiM0') FROM sys.dm_exec_connections WHERE session_id=@@SPID",c);using var r=await cmd.ExecuteReaderAsync();await r.ReadAsync();Console.WriteLine("PASS FIXTURE_TLS_STRICT encrypted="+r.GetInt32(0)+" database="+(r.IsDBNull(1)?"ABSENT":"PRESENT"));
 }
 public static void WriteFixtureConfig(string root,int port=14336) {
  var purpose=Environment.GetEnvironmentVariable("KOHAMI_M0_FIXTURE_PURPOSE");if(port<14336||port>14399)throw new InvalidOperationException("FIXTURE_PORT_OUT_OF_RANGE");if(purpose=="local-demo"&&!IsLocalDemoPort(port))throw new InvalidOperationException("FIXTURE_PORT_RESERVED");var endpoint="localhost,"+port;var local=FixtureLocal(root);var json=JsonNode.Parse(File.ReadAllText(Path.Combine(local,"runtime-private.json")))!.AsObject();var certificate=json["sqlCertificatePath"]!.GetValue<string>();var api=new SqlConnectionStringBuilder(json["connectionString"]!.GetValue<string>()){DataSource=endpoint,HostNameInCertificate="localhost"};api["Server Certificate"]=certificate;var workerSettings=JsonNode.Parse(File.ReadAllText(Path.Combine(local,"worker-private.json")))!.AsObject();var worker=new SqlConnectionStringBuilder(workerSettings["workerConnectionString"]!.GetValue<string>()){DataSource=endpoint,HostNameInCertificate="localhost"};worker["Server Certificate"]=certificate;json["connectionString"]=api.ConnectionString;json["workerConnectionString"]=worker.ConnectionString;json["IdentitySqlTestDedicated"]=true;var admin=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="KohamiM0",UserID="sa",Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};var provisionedAdminCredential=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))!["sa"];admin["Password"]=provisionedAdminCredential;admin["Server Certificate"]=certificate;json["MigratorConnectionString"]=admin.ConnectionString;
  if(purpose=="wallet03-http"){json["Wallet03HttpDedicated"]=true;json["port"]=port;}else if(purpose=="local-demo"){json["KohamiDemoDedicated"]=true;json["port"]=port;}
  var output=Path.Combine(local,"fixture-private.json");File.WriteAllText(output,json.ToJsonString());File.SetUnixFileMode(output,UnixFileMode.UserRead|UnixFileMode.UserWrite);Console.WriteLine("FIXTURE_CONFIG_WRITTEN_MODE_0600_IDENTITY_AND_COMMERCE_TARGET_PORT_"+port);
 }
 public static bool IsDedicatedFixtureEndpoint(string endpoint)=>endpoint.StartsWith("localhost,",StringComparison.Ordinal)&&int.TryParse(endpoint.AsSpan("localhost,".Length),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var port)&&port>=14336&&port<=14399;
 public static async Task Execute(SqlConnection c,string sql) {using var cmd=new SqlCommand(sql,c){CommandTimeout=60};await cmd.ExecuteNonQueryAsync();}
}
