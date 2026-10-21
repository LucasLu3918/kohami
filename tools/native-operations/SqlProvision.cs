using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Kohami.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
namespace Kohami.Native;
public static class SqlProvision {
 public static bool IsWallet03HttpPort(int port)=>port>=14336&&port<=14399&&port!=14349;
 public static bool IsLocalDemoPort(int port)=>port>=14336&&port<=14399&&port is not (14349 or 14350);
 static string FixtureLocal(string root) {
  var repo=Path.GetFullPath(root);var localRoot=Path.Combine(repo,".local");
  if(new DirectoryInfo(localRoot).LinkTarget is not null||!Directory.Exists(localRoot))throw new InvalidOperationException("LOCAL_ROOT_INVALID");
  var configured=Environment.GetEnvironmentVariable("KOHAMI_LOCAL_DIR");var local=Path.GetFullPath(string.IsNullOrWhiteSpace(configured)?Path.Combine(localRoot,"identity"):Path.IsPathRooted(configured)?configured:Path.Combine(repo,configured));
  var relative=Path.GetRelativePath(localRoot,local);if(relative=="."||relative==".."||relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidOperationException("LOCAL_PROFILE_INVALID");
  var current=localRoot;foreach(var part in relative.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)){current=Path.Combine(current,part);if(new DirectoryInfo(current).LinkTarget is not null||!Directory.Exists(current))throw new InvalidOperationException("LOCAL_PROFILE_INVALID");}
  return local;
 }
 public static async Task ProbeAsync(string root) {
  var local=FixtureLocal(root);var s=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(s).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="master",UserID="sa",Password=secrets["sa"],TrustServerCertificate=false,Encrypt=SqlConnectionEncryptOption.Strict,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;
  await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();using var cmd=new SqlCommand("SELECT CASE WHEN encrypt_option='TRUE' THEN 1 ELSE 0 END FROM sys.dm_exec_connections WHERE session_id=@@SPID",c);if(Convert.ToInt32(await cmd.ExecuteScalarAsync())!=1)throw new InvalidOperationException("SQL_TLS_NOT_ENCRYPTED");
  Console.WriteLine("PASS SQL_TLS_STRICT_CUSTOM_CHAIN_SAN_LOCALHOST_EXACT_LEAF_PIN_ENCRYPTED; DBA probe is not runtime permission evidence");
 }
 public static async Task RunAsync(string root,string endpoint="localhost,14334") {
  var local=FixtureLocal(root);var s=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(s).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(endpoint!="localhost,14334"&&!IsDedicatedFixtureEndpoint(endpoint))throw new InvalidOperationException("PROVISIONING_ENDPOINT_REJECTED");
  var fixture=IsDedicatedFixtureEndpoint(endpoint);
  var roles=new Dictionary<string,string>{{"api","api"},{"worker","worker"},{"native","native"},{"migrator","migrator"},{"backup","backup"}};

  var b=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="master",UserID="sa",Password=secrets["sa"],TrustServerCertificate=false,Encrypt=SqlConnectionEncryptOption.Strict,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;
  await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();
  await Execute(c,"IF DB_ID('Kohami') IS NOT NULL THROW 51000,'DB_ALREADY_EXISTS',1; CREATE DATABASE Kohami; ALTER DATABASE Kohami SET TRUSTWORTHY OFF; ALTER DATABASE Kohami SET DB_CHAINING OFF;");
  foreach(var role in roles) {
   using var cmd=SqlRuntime.Command(c,"DECLARE @sql nvarchar(max)='CREATE LOGIN '+QUOTENAME(@login)+' WITH PASSWORD='+QUOTENAME(@password,'''')+', CHECK_POLICY=ON'; EXEC(@sql);",null,("@login","kohami_"+role.Key),("@password",secrets[role.Value]));await cmd.ExecuteNonQueryAsync();
  }
  c.ChangeDatabase("Kohami");
  await Execute(c,"CREATE SCHEMA ops AUTHORIZATION dbo;");
  foreach(var role in roles.Keys)await Execute(c,$"CREATE USER [kohami_{role}] FOR LOGIN [kohami_{role}];");
  var grantTargets=SqlMigrations.Load(root).SelectMany(m=>m.Batches)
   .SelectMany(batch=>Regex.Matches(batch.Sql,@"(?im)^\s*GRANT\b[^\r\n]*?\bTO\s+\[?([A-Za-z0-9_]+)\]?")
    .Select(match=>match.Groups[1].Value))
   .Distinct(StringComparer.OrdinalIgnoreCase);
  foreach(var principal in grantTargets) {
   using var ensure=SqlRuntime.Command(c,"IF DATABASE_PRINCIPAL_ID(@name) IS NULL BEGIN DECLARE @sql nvarchar(max)=N'CREATE ROLE '+QUOTENAME(@name)+N' AUTHORIZATION dbo';EXEC sys.sp_executesql @sql;END",null,("@name",principal));
   await ensure.ExecuteNonQueryAsync();
   var suffix=roles.Keys.FirstOrDefault(role=>principal.EndsWith("_"+role,StringComparison.OrdinalIgnoreCase));
   if(suffix is not null)await Execute(c,$"ALTER ROLE [{principal}] ADD MEMBER [kohami_{suffix}];");
  }
  await Execute(c,"ALTER ROLE db_backupoperator ADD MEMBER kohami_backup;");
  await SqlMigrations.ApplyAsync(c,SqlMigrations.Load(root));
  if(fixture){
   if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_ID_REQUIRED");
   await Execute(c,"IF OBJECT_ID('ops.DevelopmentFixtureMarker','U') IS NULL CREATE TABLE ops.DevelopmentFixtureMarker(FixtureId uniqueidentifier NOT NULL PRIMARY KEY,DatasetId nvarchar(40) NOT NULL CHECK(DatasetId='kohami_local'),InstanceFingerprint char(64) NOT NULL,CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME());");
   using var marker=SqlRuntime.Command(c,"DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2));INSERT ops.DevelopmentFixtureMarker(FixtureId,DatasetId,InstanceFingerprint) VALUES(@id,'kohami_local',@fp)",null,("@id",fixtureId));await marker.ExecuteNonQueryAsync();
   Console.WriteLine("DEDICATED_DEVELOPMENT_FIXTURE_PROVISIONED");
  }else Console.WriteLine("KOHAMI_PROVISIONED_TLS_STRICT_RUNTIME_ROLES_DISTINCT_NATIVE_IMPORT_PENDING");
 }
 public static async Task ApplyFixtureGrantsAsync(string root,string endpoint) {
  var local=Path.Combine(root,".local","identity");var s=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");var b=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="Kohami",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();await ApplyGrantScriptsAsync(c,root);Console.WriteLine("PASS exact SQL grants applied to scoped isolated Kohami instance");
 }
 public static async Task MigrateNativeLocalAsync(string root) {
  var local=FixtureLocal(root);var settings=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(settings).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="Kohami",UserID="kohami_migrator",Password=secrets["migrator"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};b["Server Certificate"]=settings.SqlCertificatePath;
  await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();using(var scope=new SqlCommand("SELECT DB_NAME(),CONVERT(nvarchar(100),SERVERPROPERTY('ServerName'))",c)){using var r=await scope.ExecuteReaderAsync();if(!await r.ReadAsync()||r.GetString(0)!="Kohami"||string.IsNullOrWhiteSpace(r.GetString(1)))throw new InvalidOperationException("SQL_SCOPE_INVALID");}
  await SqlMigrations.ApplyAsync(c,SqlMigrations.Load(root));
  Console.WriteLine("PASS versioned native SQL migrations applied to local Kohami; no Owner bootstrap");
 }
 public static async Task MigrateKohamiSqlFixtureAsync(string root) {
  const string endpoint="localhost,14349";
  var local=Path.Combine(root,".local","kohami-sql","identity");
  var settings=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(settings).ValidateTls();
  var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,endpoint,StringComparison.OrdinalIgnoreCase)||runtime.InitialCatalog!="Kohami"||settings.DatasetId!="kohami_local")throw new InvalidOperationException("KOHAMI_SQL_TARGET_INVALID");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var sourceDatabase=secrets.GetValueOrDefault("sourceDatabase");
  if(string.IsNullOrWhiteSpace(sourceDatabase)||sourceDatabase=="Kohami"||!Regex.IsMatch(sourceDatabase,@"^[A-Za-z][A-Za-z0-9_]{0,127}$",RegexOptions.CultureInvariant))throw new InvalidOperationException("LEGACY_SOURCE_DATABASE_REQUIRED");
  var admin=new SqlConnectionStringBuilder(settings.ConnectionString){DataSource=endpoint,InitialCatalog="master",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};admin["Server Certificate"]=settings.SqlCertificatePath;
  await using var c=new SqlConnection(admin.ConnectionString);await c.OpenAsync();
  var createdTarget=false;var migrationApplied=false;
  try {
   var targetExists=Convert.ToInt32(await new SqlCommand("SELECT CASE WHEN DB_ID(N'Kohami') IS NULL THEN 0 ELSE 1 END",c).ExecuteScalarAsync())==1;
   if(targetExists) {
    c.ChangeDatabase("Kohami");
    var plan=SqlMigrations.Load(root);
    using(var history=SqlRuntime.Command(c,"SELECT COUNT(*) FROM ops.SchemaMigrations"))
     if(Convert.ToInt32(await history.ExecuteScalarAsync())==plan.Count) {
      using(var readyAdminInspect=SqlRuntime.Command(c,"EXEC ops.Inspect"))using(var readyAdminResult=await readyAdminInspect.ExecuteReaderAsync())
       if(!await readyAdminResult.ReadAsync()||readyAdminResult.GetString(readyAdminResult.GetOrdinal("DatasetId"))!="kohami_local"||readyAdminResult.GetString(readyAdminResult.GetOrdinal("DatabaseName"))!="Kohami")throw new InvalidOperationException("KOHAMI_SQL_TARGET_INVALID");
      await c.CloseAsync();await using var readyRuntime=await new SqlRuntime(settings).OpenAsync();
      if(!await SchemaContract.IsReadyAsync(new SqlRuntime(settings)))throw new InvalidOperationException("RENAMED_DATABASE_NOT_READY");
      Console.WriteLine("PASS local Kohami database already current and runtime-readiness verified");return;
     }
     else if(Convert.ToInt32(await history.ExecuteScalarAsync())!=plan.Count-1)throw new InvalidOperationException("RENAMED_DATABASE_ALREADY_EXISTS");
    await SqlMigrations.ApplyAsync(c,plan);
    await c.CloseAsync();
    if(!await SchemaContract.IsReadyAsync(new SqlRuntime(settings)))throw new InvalidOperationException("RENAMED_DATABASE_NOT_READY");
    await c.OpenAsync();
    c.ChangeDatabase("Kohami");
    using var resumedInspect=SqlRuntime.Command(c,"EXEC ops.Inspect");
    using var resumedResult=await resumedInspect.ExecuteReaderAsync();
    if(!await resumedResult.ReadAsync()||resumedResult.GetString(resumedResult.GetOrdinal("DatasetId"))!="kohami_local"||resumedResult.GetString(resumedResult.GetOrdinal("DatabaseName"))!="Kohami")throw new InvalidOperationException("KOHAMI_SQL_TARGET_INVALID");
    Console.WriteLine("PASS local Kohami database migration resumed and runtime-readiness verified");
    return;
   }
   using var databaseState=SqlRuntime.Command(c,"SELECT DB_ID(@source),DB_ID(N'Kohami')",null,("@source",sourceDatabase));
   await using(var reader=await databaseState.ExecuteReaderAsync()) {
    if(!await reader.ReadAsync()||reader.IsDBNull(0))throw new InvalidOperationException("LEGACY_DATABASE_REQUIRED");
    if(!reader.IsDBNull(1))throw new InvalidOperationException("RENAMED_DATABASE_ALREADY_EXISTS");
   }
   using(var sessions=SqlRuntime.Command(c,"SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID(@source) AND session_id<>@@SPID",null,("@source",sourceDatabase)))
    if(Convert.ToInt32(await sessions.ExecuteScalarAsync())!=0)throw new InvalidOperationException("LEGACY_DATABASE_HAS_ACTIVE_SESSIONS");
   using var file=SqlRuntime.Command(c,"SELECT TOP(1) physical_name FROM sys.master_files WHERE database_id=DB_ID(@source) AND type=0 ORDER BY file_id",null,("@source",sourceDatabase));
   var sourceFile=Convert.ToString(await file.ExecuteScalarAsync())??throw new InvalidOperationException("LEGACY_DATABASE_FILE_LAYOUT_INVALID");
   var slash=sourceFile.LastIndexOf('/');var backslash=sourceFile.LastIndexOf('\\');var split=Math.Max(slash,backslash);
   if(split<0)throw new InvalidOperationException("LEGACY_DATABASE_FILE_LAYOUT_INVALID");
   var dataDirectory=sourceFile[..(split+1)];var token=Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
   var backupPath=dataDirectory+"KohamiRename_"+token+".bak";
   var sourceIdentifier="["+sourceDatabase.Replace("]","]]",StringComparison.Ordinal)+"]";
   await Execute(c,$"BACKUP DATABASE {sourceIdentifier} TO DISK=N'{EscapeSqlLiteral(backupPath)}' WITH COPY_ONLY,INIT,COMPRESSION,CHECKSUM;");
   await Execute(c,$"RESTORE VERIFYONLY FROM DISK=N'{EscapeSqlLiteral(backupPath)}' WITH CHECKSUM;");
   var files=new List<(string LogicalName,string Type)>();
   await using(var list=new SqlCommand($"RESTORE FILELISTONLY FROM DISK=N'{EscapeSqlLiteral(backupPath)}'",c))
   await using(var reader=await list.ExecuteReaderAsync()) {
    var logical=reader.GetOrdinal("LogicalName");var type=reader.GetOrdinal("Type");
    while(await reader.ReadAsync())files.Add((reader.GetString(logical),reader.GetString(type)));
   }
   if(files.Count==0||files.Any(f=>f.Type is not ("D" or "L")))throw new InvalidOperationException("DATABASE_FILE_LAYOUT_UNSUPPORTED");
   var moves=new List<string>();var dataIndex=0;var logIndex=0;
   foreach(var entry in files) {
    var ext=entry.Type=="L"?".ldf":dataIndex++==0?".mdf":".ndf";
    if(entry.Type=="L")logIndex++;
    var destination=dataDirectory+"Kohami_"+token+(entry.Type=="L"&&logIndex>1?"_"+logIndex:entry.Type=="D"&&dataIndex>1?"_"+dataIndex:"")+ext;
    moves.Add($"MOVE N'{EscapeSqlLiteral(entry.LogicalName)}' TO N'{EscapeSqlLiteral(destination)}'");
   }
   await Execute(c,$"RESTORE DATABASE [Kohami] FROM DISK=N'{EscapeSqlLiteral(backupPath)}' WITH FILE=1,{string.Join(',',moves)},RECOVERY,STATS=5;");
   createdTarget=true;
   c.ChangeDatabase("master");
   foreach(var role in new[]{"api","worker","native","migrator","backup"}) {
    if(!secrets.TryGetValue(role,out var password)||string.IsNullOrWhiteSpace(password))throw new InvalidOperationException("PROVISIONER_CREDENTIAL_REQUIRED:"+role);
    using var login=SqlRuntime.Command(c,"IF SUSER_ID(@name) IS NULL BEGIN DECLARE @create nvarchar(max)=N'CREATE LOGIN '+QUOTENAME(@name)+N' WITH PASSWORD='+QUOTENAME(@password,'''')+N',CHECK_POLICY=ON';EXEC sys.sp_executesql @create;END ELSE BEGIN DECLARE @alter nvarchar(max)=N'ALTER LOGIN '+QUOTENAME(@name)+N' WITH PASSWORD='+QUOTENAME(@password,'''');EXEC sys.sp_executesql @alter;END",null,("@name","kohami_"+role),("@password",password));
    await login.ExecuteNonQueryAsync();
   }
   c.ChangeDatabase("Kohami");
   foreach(var role in new[]{"api","worker","native","migrator","backup"})
    await Execute(c,$"IF DATABASE_PRINCIPAL_ID(N'kohami_{role}') IS NULL CREATE USER [kohami_{role}] FOR LOGIN [kohami_{role}];");
   await SqlMigrations.ApplyAsync(c,SqlMigrations.Load(root));
   migrationApplied=true;
   await using(var runtimeConnection=await new SqlRuntime(settings).OpenAsync()) {
    if(!await SchemaContract.IsReadyAsync(new SqlRuntime(settings)))throw new InvalidOperationException("RENAMED_DATABASE_NOT_READY");
    using var inspect=SqlRuntime.Command(c,"EXEC ops.Inspect");
    using var result=await inspect.ExecuteReaderAsync();
    if(!await result.ReadAsync()||result.GetString(result.GetOrdinal("DatasetId"))!="kohami_local"||result.GetString(result.GetOrdinal("DatabaseName"))!="Kohami")throw new InvalidOperationException("KOHAMI_SQL_TARGET_INVALID");
    Console.WriteLine("NATIVE_TARGET_BOUND endpoint=localhost,14349 database=Kohami dataset=kohami_local fingerprint="+result.GetString(result.GetOrdinal("Fingerprint"))+" epoch="+result.GetInt64(result.GetOrdinal("Epoch"))+" maintenance="+result.GetBoolean(result.GetOrdinal("Maintenance")));
   }
   Console.WriteLine("PASS local Kohami database copied, migrated and runtime-readiness verified; source database preserved");
  } catch {
   if(createdTarget&&!migrationApplied){if(c.State==System.Data.ConnectionState.Open&&c.Database!="master")c.ChangeDatabase("master");await Execute(c,"IF DB_ID('Kohami') IS NOT NULL BEGIN ALTER DATABASE [Kohami] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [Kohami]; END;");}
   throw;
  }
 }
 public static async Task InitializeLocalDevelopmentAsync(string root) {
  const string endpoint="localhost,14349";
  const string userId="kohami_local_member";
  const string email="kohami-local-member@example.test";
  const long syntheticCredit=50000;
  if(OperatingSystem.IsWindows())throw new InvalidOperationException("LOCAL_DEVELOPMENT_REQUIRES_UNIX_PRIVATE_FILE_MODE");
  var local=Path.Combine(root,".local","kohami-sql","identity");
  var settings=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(settings).ValidateTls();
  var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,endpoint,StringComparison.OrdinalIgnoreCase)||runtime.InitialCatalog!="Kohami"||settings.DatasetId!="kohami_local")throw new InvalidOperationException("KOHAMI_SQL_TARGET_INVALID");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");
  var target=new SqlConnectionStringBuilder(settings.ConnectionString){DataSource=endpoint,InitialCatalog="Kohami",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};target["Server Certificate"]=settings.SqlCertificatePath;
  var credentialsPath=Path.Combine(local,"local-development-credentials.json");
  if(File.Exists(credentialsPath))throw new InvalidOperationException("LOCAL_DEVELOPMENT_ALREADY_INITIALIZED");
  var generatedPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
  var passwordHash=new PasswordHasher<object>().HashPassword(new object(),generatedPassword);
  await using var connection=new SqlConnection(target.ConnectionString);await connection.OpenAsync();
  await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync();
  var credentialsCreated=false;var commitStarted=false;
  try {
  await using(var seed=new SqlCommand(@"
DECLARE @fingerprint char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2));
IF DB_NAME()<>'Kohami' OR (SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='kohami_local' AND InstanceFingerprint=@fingerprint)<>1 THROW 51000,'DEDICATED_FIXTURE_MARKER_MISMATCH',1;
IF NOT EXISTS(SELECT 1 FROM identity_access.SecurityState WITH(UPDLOCK,HOLDLOCK) WHERE Id=1 AND Maintenance=1 AND OwnerPrepared=0 AND BootstrapActivated=0) THROW 51000,'LOCAL_DEVELOPMENT_SECURITY_STATE_INVALID',1;
IF EXISTS(SELECT 1 FROM identity_access.Users) OR EXISTS(SELECT 1 FROM identity_access.Grants) THROW 51000,'LOCAL_DEVELOPMENT_REQUIRES_EMPTY_IDENTITY',1;
IF NOT EXISTS(SELECT 1 FROM ops.SchemaMigrations WHERE MigrationId='0014_wallet_payment_api_writes') THROW 51000,'LOCAL_DEVELOPMENT_SCHEMA_NOT_READY',1;
INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion) VALUES(@user,@email,@hash,1,0,'Active',1);
UPDATE identity_access.SecurityState SET Maintenance=0 WHERE Id=1 AND Maintenance=1;
IF @@ROWCOUNT<>1 THROW 51000,'LOCAL_DEVELOPMENT_GATE_CHANGE_FAILED',1;
EXEC commerce.wallet_synthetic_credit @UserId=@user,@SourceId='synthetic-test-credit:local-development-initial',@AmountTwd=@credit;
INSERT identity_access.SecurityAudit(UserId,Action,At,SecurityVersion) VALUES(@user,'LocalDevelopmentBootstrap',SYSUTCDATETIME(),1);",connection,transaction)) {
   seed.Parameters.AddWithValue("@fixture",fixtureId);seed.Parameters.AddWithValue("@user",userId);seed.Parameters.AddWithValue("@email",email);seed.Parameters.AddWithValue("@hash",passwordHash);seed.Parameters.AddWithValue("@credit",syntheticCredit);await seed.ExecuteNonQueryAsync();
  }
   Directory.CreateDirectory(local);
   var credentialOptions=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None,UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite};
   await using(var credentialFile=new FileStream(credentialsPath,credentialOptions)) {
    credentialsCreated=true;
    await JsonSerializer.SerializeAsync(credentialFile,new{mode="local-development",datasetId="kohami_local",email,password=generatedPassword,initialSyntheticCreditTwd=syntheticCredit});
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
 public static async Task SeedLocalAccountsAsync(string root) {
  const string endpoint="localhost,14349";
  const string memberId="kohami_demo_member";
  const string memberEmail="demo@test.com";
  const string adminId="kohami_demo_admin";
  const string adminEmail="admin@test.com";
  const string password="1234";
  if(OperatingSystem.IsWindows())throw new InvalidOperationException("LOCAL_ACCOUNTS_REQUIRE_UNIX_PRIVATE_FILE_MODE");
  var local=Path.GetFullPath(Path.Combine(root,".local","kohami-sql","identity"));
  var localRoot=Path.GetFullPath(Path.Combine(root,".local"));
  var kohamiLocal=Path.Combine(localRoot,"kohami-sql");
  if(new DirectoryInfo(localRoot).LinkTarget is not null||new DirectoryInfo(kohamiLocal).LinkTarget is not null||new DirectoryInfo(local).LinkTarget is not null||!Directory.Exists(local))throw new InvalidOperationException("LOCAL_PROFILE_INVALID");
  var runtimePath=Path.Combine(local,"runtime-private.json");
  var provisionerPath=Path.Combine(local,"provisioner-private.json");
  var memberMarkerPath=Path.Combine(local,"local-development-credentials.json");
  foreach(var path in new[]{runtimePath,provisionerPath,memberMarkerPath})
   if(!File.Exists(path)||new FileInfo(path).LinkTarget is not null)throw new InvalidOperationException("LOCAL_PROFILE_MATERIAL_REQUIRED");
  foreach(var path in new[]{runtimePath,provisionerPath,memberMarkerPath})
   if((File.GetUnixFileMode(path)&(UnixFileMode.GroupRead|UnixFileMode.GroupWrite|UnixFileMode.GroupExecute|UnixFileMode.OtherRead|UnixFileMode.OtherWrite|UnixFileMode.OtherExecute))!=0)throw new InvalidOperationException("LOCAL_PROFILE_FILE_MODE_INVALID");
  var settings=RuntimeSettings.Load(runtimePath);new SqlRuntime(settings).ValidateTls();
  var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,endpoint,StringComparison.OrdinalIgnoreCase)||runtime.InitialCatalog!="Kohami"||settings.DatasetId!="kohami_local")throw new InvalidOperationException("KOHAMI_SQL_TARGET_INVALID");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(provisionerPath))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("LOCAL_FIXTURE_MARKER_REQUIRED");
  var admin=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="Kohami",UserID="sa",Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};
  admin["Password"]=secrets["sa"];admin["Server Certificate"]=settings.SqlCertificatePath;
  await using var connection=new SqlConnection(admin.ConnectionString);await connection.OpenAsync();
  var migrations=SqlMigrations.Load(root);
  var applied=new List<(string MigrationId,string SourceHash)>();
  await using(var migration=new SqlCommand("SELECT MigrationId,SourceHash FROM ops.SchemaMigrations ORDER BY MigrationId",connection))
  await using(var reader=await migration.ExecuteReaderAsync())while(await reader.ReadAsync())applied.Add((reader.GetString(0),reader.GetString(1).Trim()));
  if(!SchemaContract.IsCurrent(applied)||!migrations.Select(item=>(item.Id,item.SourceHash)).SequenceEqual(applied))throw new InvalidOperationException("LOCAL_ACCOUNTS_SCHEMA_NOT_CURRENT");
  var passwordHasher=new PasswordHasher<object>();
  var memberHash=passwordHasher.HashPassword(new object(),password);
  var adminHash=passwordHasher.HashPassword(new object(),password);
  await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
  try {
   await using(var inspect=new SqlCommand("SELECT UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status FROM identity_access.Users WITH(UPDLOCK,HOLDLOCK) WHERE Email IN(@memberEmail,@adminEmail)",connection,transaction)) {
    inspect.Parameters.AddWithValue("@memberEmail",memberEmail);inspect.Parameters.AddWithValue("@adminEmail",adminEmail);
    await using var rows=await inspect.ExecuteReaderAsync();
    while(await rows.ReadAsync()) {
     var email=rows.GetString(1);var expectedId=email==memberEmail?memberId:adminId;var expectedConfirmed=email==adminEmail;
     if(rows.GetString(0)!=expectedId||rows.GetBoolean(3)!=expectedConfirmed||rows.GetBoolean(4)||rows.GetString(5)!="Active"||passwordHasher.VerifyHashedPassword(new object(),rows.GetString(2),password)==PasswordVerificationResult.Failed)
      throw new InvalidOperationException(email==memberEmail?"LOCAL_DEMO_MEMBER_ACCOUNT_CONFLICT":"LOCAL_DEMO_ADMIN_ACCOUNT_CONFLICT");
    }
   }
   await using(var seed=new SqlCommand(@"
DECLARE @fingerprint char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2));
IF DB_NAME()<>'Kohami' OR (SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='kohami_local' AND InstanceFingerprint=@fingerprint)<>1 THROW 51000,'DEDICATED_FIXTURE_MARKER_MISMATCH',1;
IF NOT EXISTS(SELECT 1 FROM identity_access.SecurityState WITH(UPDLOCK,HOLDLOCK) WHERE Id=1 AND Maintenance=0) THROW 51000,'LOCAL_ACCOUNTS_SECURITY_STATE_INVALID',1;
IF NOT EXISTS(SELECT 1 FROM ops.SchemaMigrations WHERE MigrationId='0020_member_registration') THROW 51000,'LOCAL_ACCOUNTS_SCHEMA_NOT_READY',1;
DECLARE @member nvarchar(100)=(SELECT UserId FROM identity_access.Users WITH(UPDLOCK,HOLDLOCK) WHERE Email=@memberEmail);
IF @member IS NULL
BEGIN
 INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion) VALUES(@memberId,@memberEmail,@memberHash,0,0,'Active',1);
 INSERT identity_access.MemberProfiles(UserId,DisplayName,Phone) VALUES(@memberId,N'KOHAMI 測試會員',NULL);
 INSERT identity_access.SecurityAudit(UserId,Action,At,SecurityVersion) VALUES(@memberId,'LocalDemoMemberCreated',SYSUTCDATETIME(),1);
END
ELSE IF @member<>@memberId OR NOT EXISTS(SELECT 1 FROM identity_access.Users WHERE UserId=@member AND Status='Active' AND SecurityHold=0 AND EmailConfirmed=0)
 THROW 51000,'LOCAL_DEMO_MEMBER_ACCOUNT_CONFLICT',1;

DECLARE @admin nvarchar(100)=(SELECT UserId FROM identity_access.Users WITH(UPDLOCK,HOLDLOCK) WHERE Email=@adminEmail);
IF @admin IS NULL
BEGIN
 INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion) VALUES(@adminId,@adminEmail,@adminHash,1,0,'Active',1);
 INSERT identity_access.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
  VALUES('local_demo_catalog_reader',@adminId,'Merchandiser','catalog.read','Dataset','kohami_local',SYSUTCDATETIME(),DATEADD(year,1,SYSUTCDATETIME()),'local-demo-bootstrap','read-only catalog access');
 INSERT identity_access.SecurityAudit(UserId,Action,At,SecurityVersion) VALUES(@adminId,'LocalDemoAdminCreated',SYSUTCDATETIME(),1);
END
ELSE IF @admin<>@adminId OR NOT EXISTS(SELECT 1 FROM identity_access.Users WHERE UserId=@admin AND Status='Active' AND SecurityHold=0 AND EmailConfirmed=1)
 THROW 51000,'LOCAL_DEMO_ADMIN_ACCOUNT_CONFLICT',1;
IF (SELECT COUNT(*) FROM identity_access.Grants WHERE UserId=@adminId AND RevokedAt IS NULL AND Role='Merchandiser' AND Permission='catalog.read' AND ScopeKind='Dataset' AND ScopeId='kohami_local')<>1
 THROW 51000,'LOCAL_DEMO_ADMIN_GRANT_CONFLICT',1;
IF EXISTS(SELECT 1 FROM identity_access.Grants WHERE UserId=@adminId AND RevokedAt IS NULL AND NOT(Role='Merchandiser' AND Permission='catalog.read' AND ScopeKind='Dataset' AND ScopeId='kohami_local'))
 THROW 51000,'LOCAL_DEMO_ADMIN_GRANT_CONFLICT',1;",connection,transaction)) {
    seed.Parameters.AddWithValue("@fixture",fixtureId);seed.Parameters.AddWithValue("@memberEmail",memberEmail);seed.Parameters.AddWithValue("@memberId",memberId);seed.Parameters.AddWithValue("@memberHash",memberHash);seed.Parameters.AddWithValue("@adminEmail",adminEmail);seed.Parameters.AddWithValue("@adminId",adminId);seed.Parameters.AddWithValue("@adminHash",adminHash);await seed.ExecuteNonQueryAsync();
   }
   await transaction.CommitAsync();
  } catch { try{await transaction.RollbackAsync();}catch{} throw; }
  Console.WriteLine("PASS loopback Kohami synthetic member and read-only catalog admin accounts verified; credentials were hashed; existing identities and database contents preserved");
 }
 public static async Task MigrateDedicatedFixtureAsync(string root,int port) {
  if(port<14336||port>14399)throw new InvalidOperationException("FIXTURE_PORT_OUT_OF_RANGE");
  var local=FixtureLocal(root);var settings=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(settings).ValidateTls();
  var expectedEndpoint="localhost,"+port;var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,expectedEndpoint,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("FIXTURE_RUNTIME_TARGET_MISMATCH");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");
  // Read the marker with the private provisioner credential because the application
  // migrator intentionally has no access to fixture identification metadata or GRANT authority.
  // Both the runtime endpoint and SQL instance fingerprint must match before migration.
  var connection=new SqlConnectionStringBuilder(settings.ConnectionString){DataSource=expectedEndpoint,InitialCatalog="Kohami",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};connection["Server Certificate"]=settings.SqlCertificatePath;
  await using var c=new SqlConnection(connection.ConnectionString);await c.OpenAsync();
  using(var scope=SqlRuntime.Command(c,"DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2));SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='kohami_local' AND InstanceFingerprint=@fp",null,("@id",fixtureId)))
   if(Convert.ToInt32(await scope.ExecuteScalarAsync())!=1)throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_MISMATCH");
  Console.WriteLine("PASS dedicated fixture marker verified before versioned migration");
  await SqlMigrations.ApplyAsync(c,SqlMigrations.Load(root));
  Console.WriteLine("PASS versioned migrations applied to marker-verified dedicated fixture; no reset or Owner bootstrap");
 }
 public static async Task VerifyLocalDemoCatalogAsync(string root,int port) {
  if(!IsLocalDemoPort(port))throw new InvalidOperationException("FIXTURE_PORT_RESERVED");
  var local=FixtureLocal(root);var settings=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(settings).ValidateTls();
  var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,"localhost,"+port,StringComparison.OrdinalIgnoreCase)||runtime.InitialCatalog!="Kohami"||settings.DatasetId!="kohami_local"||settings.SqlServerName!="localhost")throw new InvalidOperationException("FIXTURE_RUNTIME_TARGET_MISMATCH");
  var fixturePath=Path.Combine(local,"fixture-private.json");var config=JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()??throw new InvalidOperationException("FIXTURE_CONFIG_INVALID");
  if(config["KohamiDemoDedicated"]?.GetValue<bool>()!=true||config["port"]?.GetValue<int>()!=port)throw new InvalidOperationException("DEMO_FIXTURE_PROFILE_MISMATCH");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");
  var admin=new SqlConnectionStringBuilder{DataSource=runtime.DataSource,InitialCatalog="Kohami",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};admin["Server Certificate"]=settings.SqlCertificatePath;
  await using var connection=new SqlConnection(admin.ConnectionString);await connection.OpenAsync();
  await using(var marker=new SqlCommand("DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2));SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='kohami_local' AND InstanceFingerprint=@fp",connection)){marker.Parameters.AddWithValue("@id",fixtureId);if(Convert.ToInt32(await marker.ExecuteScalarAsync())!=1)throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_MISMATCH");}
  await using var verify=new SqlCommand("""
SELECT (SELECT COUNT(*) FROM identity_access.CatalogProducts WHERE Dataset='kohami_local' AND State='Published' AND PublicSlug IS NOT NULL),
       (SELECT COUNT(*) FROM identity_access.CatalogProducts p WHERE Dataset='kohami_local' AND State='Published' AND
          (SELECT COUNT(DISTINCT locale) FROM OPENJSON(p.PublicJson,'$.translations') WITH(locale nvarchar(10) '$.locale') t WHERE t.locale IN('zh-TW','ja','en'))=3),
       (SELECT COUNT(*) FROM identity_access.CatalogProductSkus s JOIN commerce.Inventory i ON i.Dataset=s.Dataset AND i.Sku=s.SkuId WHERE s.Dataset='kohami_local' AND s.PublicIncluded=1 AND s.PublicActive=1 AND i.Active=1),
       (SELECT COUNT(*) FROM identity_access.Grants),
       (SELECT COUNT(*) FROM identity_access.Users WHERE UserId='kohami_demo_admin' AND Email='demo-admin@example.test' AND Status='Active'),
       (SELECT COUNT(*) FROM identity_access.Grants g JOIN identity_access.Users u ON u.UserId=g.UserId WHERE g.Role='Merchandiser' AND g.Permission='catalog.read' AND g.ScopeKind='Dataset' AND g.ScopeId='kohami_local' AND g.RevokedAt IS NULL AND u.UserId='kohami_demo_admin' AND u.Email='demo-admin@example.test' AND u.Status='Active'),
       (SELECT COUNT(*) FROM identity_access.Grants g WHERE NOT(g.Role='Merchandiser' AND g.Permission='catalog.read' AND g.ScopeKind='Dataset' AND g.ScopeId='kohami_local' AND g.RevokedAt IS NULL AND g.UserId='kohami_demo_admin'))
""",connection);
  await using var result=await verify.ExecuteReaderAsync();await result.ReadAsync();
  var grantCount=result.GetInt32(3);var adminUserCount=result.GetInt32(4);var adminGrantCount=result.GetInt32(5);var unexpectedGrantCount=result.GetInt32(6);
  if(result.GetInt32(0)!=15||result.GetInt32(1)!=15||result.GetInt32(2)!=45||adminUserCount!=1||grantCount!=1||adminGrantCount!=1||unexpectedGrantCount!=0)
   throw new InvalidOperationException("DEMO_CATALOG_OR_MINIMAL_ADMIN_ASSERTION_FAILED");
  Console.WriteLine("PASS local demo fixture SQL: 15 published trilingual products, 45 active SKUs, exactly one active synthetic admin and one dataset-scoped catalog.read grant; marker verified; no protected fixture access");
 }
 public static async Task SeedLocalDemoAdminAsync(string root,int port) {
  if(!IsLocalDemoPort(port))throw new InvalidOperationException("FIXTURE_PORT_RESERVED");
  var local=FixtureLocal(root);var settings=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(settings).ValidateTls();
  var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,"localhost,"+port,StringComparison.OrdinalIgnoreCase)||runtime.InitialCatalog!="Kohami"||settings.DatasetId!="kohami_local")throw new InvalidOperationException("FIXTURE_RUNTIME_TARGET_MISMATCH");
  var fixturePath=Path.Combine(local,"fixture-private.json");var config=JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()??throw new InvalidOperationException("FIXTURE_CONFIG_INVALID");
  if(config["KohamiDemoDedicated"]?.GetValue<bool>()!=true||config["port"]?.GetValue<int>()!=port)throw new InvalidOperationException("DEMO_FIXTURE_PROFILE_MISMATCH");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");
  var credentialsPath=Path.Combine(local,"local-demo-admin-credentials.json");
  if(File.Exists(credentialsPath))throw new InvalidOperationException("LOCAL_DEMO_ADMIN_ALREADY_INITIALIZED");
  var bootstrapToken=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
  var loginVerifier=new PasswordHasher<object>().HashPassword(new object(),bootstrapToken);
  var admin=new SqlConnectionStringBuilder{DataSource="localhost,"+port,InitialCatalog="Kohami",UserID="sa",Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};admin["Password"]=secrets["sa"];admin["Server Certificate"]=settings.SqlCertificatePath;
  await using var connection=new SqlConnection(admin.ConnectionString);await connection.OpenAsync();
  await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
  var credentialsCreated=false;var commitStarted=false;
  try {
   var applied=new List<(string MigrationId,string SourceHash)>();
   await using(var migration=new SqlCommand("SELECT MigrationId,SourceHash FROM ops.SchemaMigrations ORDER BY MigrationId",connection,transaction))
   await using(var reader=await migration.ExecuteReaderAsync()){while(await reader.ReadAsync())applied.Add((reader.GetString(0),reader.GetString(1).Trim()));}
   var planned=SqlMigrations.Load(root).Select(item=>(item.Id,item.SourceHash));
   if(!SchemaContract.IsCurrent(applied)||!planned.SequenceEqual(applied))throw new InvalidOperationException("LOCAL_DEMO_ADMIN_SCHEMA_NOT_CURRENT");
   await using(var seed=new SqlCommand(@"
DECLARE @fingerprint char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2));
IF DB_NAME()<>'Kohami' OR (SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='kohami_local' AND InstanceFingerprint=@fingerprint)<>1 THROW 51000,'DEDICATED_FIXTURE_MARKER_MISMATCH',1;
IF NOT EXISTS(SELECT 1 FROM identity_access.SecurityState WITH(UPDLOCK,HOLDLOCK) WHERE Id=1 AND Maintenance=1 AND OwnerPrepared=0 AND BootstrapActivated=0) THROW 51000,'LOCAL_DEMO_SECURITY_STATE_INVALID',1;
IF EXISTS(SELECT 1 FROM identity_access.Users) OR EXISTS(SELECT 1 FROM identity_access.Grants) THROW 51000,'LOCAL_DEMO_ADMIN_FIXTURE_NOT_EMPTY',1;
INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion) VALUES('kohami_demo_admin','demo-admin@example.test',@hash,1,0,'Active',1);
INSERT identity_access.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason) VALUES('local_demo_catalog_reader','kohami_demo_admin','Merchandiser','catalog.read','Dataset','kohami_local',SYSUTCDATETIME(),DATEADD(year,1,SYSUTCDATETIME()),'local-demo-fixture','read-only demo catalog');
UPDATE identity_access.SecurityState SET Maintenance=0 WHERE Id=1 AND Maintenance=1;
IF @@ROWCOUNT<>1 THROW 51000,'LOCAL_DEMO_GATE_CHANGE_FAILED',1;
INSERT identity_access.SecurityAudit(UserId,Action,At,SecurityVersion) VALUES('kohami_demo_admin','LocalDemoAdminBootstrap',SYSUTCDATETIME(),1);",connection,transaction)) {
   seed.Parameters.AddWithValue("@fixture",fixtureId);seed.Parameters.AddWithValue("@hash",loginVerifier);await seed.ExecuteNonQueryAsync();
   }
   var fileOptions=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None,UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite};
   await using(var file=new FileStream(credentialsPath,fileOptions)){
    credentialsCreated=true;
    await JsonSerializer.SerializeAsync(file,new{mode="local-demo",datasetId="kohami_local",email="demo-admin@example.test",password=bootstrapToken});
    await file.WriteAsync(System.Text.Encoding.UTF8.GetBytes("\n"));
   }
   commitStarted=true;await transaction.CommitAsync();
  } catch {
   if(!commitStarted){try{await transaction.RollbackAsync();}catch{} if(credentialsCreated)File.Delete(credentialsPath);}
   else throw new InvalidOperationException("LOCAL_DEMO_ADMIN_COMMIT_OUTCOME_UNKNOWN; preserve credential and inspect exact fixture before retry");
   throw;
  }
  Console.WriteLine("PASS marker-verified local-demo Merchandiser catalog.read admin created; Maintenance=0 enables this synthetic demo fixture; credential stored mode-0600; protected SQL fixtures untouched");
 }
 public static async Task SeedIssue02OwnerAsync(string root,int port) {
  if(port<14336||port>14399)throw new InvalidOperationException("FIXTURE_PORT_OUT_OF_RANGE");
  var local=FixtureLocal(root);var settings=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(settings).ValidateTls();
  var endpoint="localhost,"+port;var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,endpoint,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("FIXTURE_RUNTIME_TARGET_MISMATCH");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");
  var admin=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="Kohami",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};admin["Server Certificate"]=settings.SqlCertificatePath;
  var generatedPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
  var passwordHash=new PasswordHasher<object>().HashPassword(new object(),generatedPassword);
  const string email="issue02-owner@example.test";const string userId="issue02_owner";
  await using var connection=new SqlConnection(admin.ConnectionString);await connection.OpenAsync();
  await using var transaction=await connection.BeginTransactionAsync();
  await using(var seed=new SqlCommand(@"DECLARE @fingerprint char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2));
IF NOT EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='kohami_local' AND InstanceFingerprint=@fingerprint) THROW 51000,'DEDICATED_FIXTURE_MARKER_MISMATCH',1;
IF EXISTS(SELECT 1 FROM identity_access.Grants WHERE Role='Owner' AND RevokedAt IS NULL) OR EXISTS(SELECT 1 FROM identity_access.Users WHERE UserId=@user OR Email=@email) THROW 51000,'FIXTURE_OWNER_ALREADY_EXISTS',1;
UPDATE identity_access.SecurityState SET Maintenance=0 WHERE Id=1 AND Maintenance=1;
IF @@ROWCOUNT<>1 THROW 51000,'FIXTURE_SECURITY_STATE_INVALID',1;
INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion) VALUES(@user,@email,@hash,1,0,'Active',1);
INSERT identity_access.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
SELECT 'fixture_'+CONVERT(nvarchar(36),NEWID()),@user,'Owner',p.Permission,'Dataset','kohami_local',SYSUTCDATETIME(),DATEADD(hour,2,SYSUTCDATETIME()),'issue02_fixture','dedicated password-login acceptance'
FROM (VALUES('orders.read'),('shipment.manage'),('support.respond'),('return.inspect'),('refund.execute'),('refund.check'),('demo.control')) p(Permission);
INSERT identity_access.SecurityAudit(UserId,Action,At,SecurityVersion) VALUES(@user,'DedicatedFixtureAccountProvisioned',SYSUTCDATETIME(),1);",connection,(SqlTransaction)transaction)) {
   seed.Parameters.AddWithValue("@fixture",fixtureId);seed.Parameters.AddWithValue("@user",userId);seed.Parameters.AddWithValue("@email",email);seed.Parameters.AddWithValue("@hash",passwordHash);await seed.ExecuteNonQueryAsync();
  }
  await transaction.CommitAsync();
  var path=Path.Combine(local,"fixture-private.json");var config=JsonNode.Parse(File.ReadAllText(path))!.AsObject();config["fixtureOwnerEmail"]=email;config["fixtureOwnerPassword"]=generatedPassword;File.WriteAllText(path,config.ToJsonString());File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);
  Console.WriteLine("PASS dedicated Issue 02 synthetic account provisioned; password stored only in mode-0600 fixture config");
 }
 public static async Task ActivateWallet03HttpAsync(string root,int port) {
  if(!IsWallet03HttpPort(port))throw new InvalidOperationException(port==14349?"FIXTURE_PORT_RESERVED":"FIXTURE_PORT_OUT_OF_RANGE");
  var local=FixtureLocal(root);var settings=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(settings).ValidateTls();
  var endpoint="localhost,"+port;var runtime=new SqlConnectionStringBuilder(settings.ConnectionString);
  if(!string.Equals(runtime.DataSource,endpoint,StringComparison.OrdinalIgnoreCase)||runtime.InitialCatalog!="Kohami"||settings.DatasetId!="kohami_local")throw new InvalidOperationException("FIXTURE_RUNTIME_TARGET_MISMATCH");
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  if(!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"),out var fixtureId))throw new InvalidOperationException("DEDICATED_FIXTURE_ID_REQUIRED");
  var admin=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="Kohami",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};admin["Server Certificate"]=settings.SqlCertificatePath;
  await using var connection=new SqlConnection(admin.ConnectionString);await connection.OpenAsync();
  await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
  try {
   var applied=new List<(string MigrationId,string SourceHash)>();
   await using(var migrations=new SqlCommand("SELECT MigrationId,SourceHash FROM ops.SchemaMigrations ORDER BY MigrationId",connection,transaction))
   await using(var reader=await migrations.ExecuteReaderAsync()){while(await reader.ReadAsync())applied.Add((reader.GetString(0),reader.GetString(1).Trim()));}
   var current=SqlMigrations.Load(root).Select(x=>(x.Id,x.SourceHash)).ToArray();
   if(!SchemaContract.IsCurrent(applied)||!current.SequenceEqual(applied))throw new InvalidOperationException("WALLET03_HTTP_SCHEMA_NOT_CURRENT");
   using(var activate=SqlRuntime.Command(connection,"""
DECLARE @fingerprint char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2));
IF DB_NAME()<>'Kohami' OR (SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='kohami_local' AND InstanceFingerprint=@fingerprint)<>1 THROW 51000,'DEDICATED_FIXTURE_MARKER_MISMATCH',1;
IF NOT EXISTS(SELECT 1 FROM identity_access.SecurityState WITH(UPDLOCK,HOLDLOCK) WHERE Id=1 AND Maintenance=1 AND OwnerPrepared=0 AND BootstrapActivated=0) THROW 51000,'WALLET03_HTTP_SECURITY_STATE_INVALID',1;
IF EXISTS(SELECT 1 FROM identity_access.Users WITH(UPDLOCK,HOLDLOCK)) OR EXISTS(SELECT 1 FROM identity_access.Grants WITH(UPDLOCK,HOLDLOCK)) THROW 51000,'WALLET03_HTTP_REQUIRES_EMPTY_IDENTITY',1;
UPDATE identity_access.SecurityState SET Maintenance=0 WHERE Id=1 AND Maintenance=1;
IF @@ROWCOUNT<>1 THROW 51000,'WALLET03_HTTP_ACTIVATION_FAILED',1;
""",transaction,("@fixture",fixtureId)))await activate.ExecuteNonQueryAsync();
   await transaction.CommitAsync();
  } catch {await transaction.RollbackAsync();throw;}
  Console.WriteLine("PASS wallet03-http writable gate enabled on marker-verified fresh fixture; no user, Owner, or grants created");
 }
 public static async Task AdoptNativeLocalBaselineAsync(string root) {
  var local=Path.Combine(root,".local","identity");var settings=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(settings).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var admin=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="Kohami",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName,ApplicationName="KOHAMI-BaselineAdoptionMetadata"};admin["Server Certificate"]=settings.SqlCertificatePath;
  var migrator=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="Kohami",UserID="kohami_migrator",Password=secrets["migrator"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName,ApplicationName="KOHAMI-BaselineAdoptionWriter"};migrator["Server Certificate"]=settings.SqlCertificatePath;
  await using var metadata=new SqlConnection(admin.ConnectionString);await metadata.OpenAsync();
  await using var writer=new SqlConnection(migrator.ConnectionString);await writer.OpenAsync();
  if(metadata.DataSource!="localhost,14334"||metadata.Database!="Kohami"||writer.DataSource!=metadata.DataSource||writer.Database!=metadata.Database)throw new InvalidOperationException("SQL_BASELINE_ADOPTION_TARGET_INVALID");
  await SqlBaselineAdoption.AdoptAsync(metadata,writer,SqlMigrations.Load(root));
  Console.WriteLine("PASS exact local SQL baseline fingerprint adopted; migration hashes recorded atomically");
 }
 static async Task ApplyGrantScriptsAsync(SqlConnection c,string root) {
  foreach(var path in new[]{"src/Modules/IdentityAccess/Infrastructure/Persistence/SQLGrants.sql","src/Modules/Commerce/Infrastructure/Persistence/CommerceGrants.sql","tools/native-operations/Grants.sql","tools/native-operations/MigratorGrants.sql"}){var file=Path.Combine(root,path);if(!File.Exists(file))throw new InvalidOperationException("GRANT_SOURCE_MISSING:"+path);foreach(var batch in Regex.Split(File.ReadAllText(file),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x)))await Execute(c,batch);}
 }
 public static async Task CheckTargetAsync(string root,string endpoint) {
  var local=FixtureLocal(root);var s=RuntimeSettings.Load(Path.Combine(local,"runtime-private.json"));new SqlRuntime(s).ValidateTls();var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="master",UserID="sa",Password=secrets["sa"],TrustServerCertificate=false,Encrypt=SqlConnectionEncryptOption.Strict,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();using var cmd=new SqlCommand("SELECT CASE WHEN encrypt_option='TRUE' THEN 1 ELSE 0 END,DB_ID('Kohami') FROM sys.dm_exec_connections WHERE session_id=@@SPID",c);using var r=await cmd.ExecuteReaderAsync();await r.ReadAsync();Console.WriteLine("PASS FIXTURE_TLS_STRICT encrypted="+r.GetInt32(0)+" database="+(r.IsDBNull(1)?"ABSENT":"PRESENT"));
 }
 public static void WriteFixtureConfig(string root,int port=14336) {
  var purpose=Environment.GetEnvironmentVariable("KOHAMI_FIXTURE_PURPOSE");if(port<14336||port>14399)throw new InvalidOperationException("FIXTURE_PORT_OUT_OF_RANGE");if(purpose=="local-demo"&&!IsLocalDemoPort(port))throw new InvalidOperationException("FIXTURE_PORT_RESERVED");var endpoint="localhost,"+port;var local=FixtureLocal(root);var json=JsonNode.Parse(File.ReadAllText(Path.Combine(local,"runtime-private.json")))!.AsObject();var certificate=json["sqlCertificatePath"]!.GetValue<string>();var api=new SqlConnectionStringBuilder(json["connectionString"]!.GetValue<string>()){DataSource=endpoint,HostNameInCertificate="localhost"};api["Server Certificate"]=certificate;var workerSettings=JsonNode.Parse(File.ReadAllText(Path.Combine(local,"worker-private.json")))!.AsObject();var worker=new SqlConnectionStringBuilder(workerSettings["workerConnectionString"]!.GetValue<string>()){DataSource=endpoint,HostNameInCertificate="localhost"};worker["Server Certificate"]=certificate;json["connectionString"]=api.ConnectionString;json["workerConnectionString"]=worker.ConnectionString;json["IdentitySqlTestDedicated"]=true;var admin=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="Kohami",UserID="sa",Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};var provisionedAdminCredential=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))!["sa"];admin["Password"]=provisionedAdminCredential;admin["Server Certificate"]=certificate;json["MigratorConnectionString"]=admin.ConnectionString;
  if(purpose=="wallet03-http"){json["Wallet03HttpDedicated"]=true;json["port"]=port;}else if(purpose=="local-demo"){json["KohamiDemoDedicated"]=true;json["port"]=port;}
  var output=Path.Combine(local,"fixture-private.json");File.WriteAllText(output,json.ToJsonString());File.SetUnixFileMode(output,UnixFileMode.UserRead|UnixFileMode.UserWrite);Console.WriteLine("FIXTURE_CONFIG_WRITTEN_MODE_0600_IDENTITY_AND_COMMERCE_TARGET_PORT_"+port);
 }
 public static bool IsDedicatedFixtureEndpoint(string endpoint)=>endpoint.StartsWith("localhost,",StringComparison.Ordinal)&&int.TryParse(endpoint.AsSpan("localhost,".Length),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var port)&&port>=14336&&port<=14399;
 static string EscapeSqlLiteral(string value)=>value.Replace("'","''",StringComparison.Ordinal);
 public static async Task Execute(SqlConnection c,string sql) {using var cmd=new SqlCommand(sql,c){CommandTimeout=60};await cmd.ExecuteNonQueryAsync();}
}
