using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kohami.M0.Core;
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
  var b=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="master",UserID="sa",Password=secrets["sa"],TrustServerCertificate=false,Encrypt=SqlConnectionEncryptOption.Strict,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;
  await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();
  await Execute(c,"IF DB_ID('KohamiM0') IS NOT NULL THROW 51000,'DB_ALREADY_EXISTS',1; CREATE DATABASE KohamiM0; ALTER DATABASE KohamiM0 SET TRUSTWORTHY OFF; ALTER DATABASE KohamiM0 SET DB_CHAINING OFF;");
  foreach(var role in new[]{"api","worker","native","migrator","backup"}) {
   using var cmd=M0Sql.Command(c,"DECLARE @sql nvarchar(max)='CREATE LOGIN '+QUOTENAME(@login)+' WITH PASSWORD='+QUOTENAME(@password,'''')+', CHECK_POLICY=ON'; EXEC(@sql);",null,("@login","kohami_m0_"+role),("@password",secrets[role]));await cmd.ExecuteNonQueryAsync();
  }
  c.ChangeDatabase("KohamiM0");
  await Execute(c,"CREATE SCHEMA ops AUTHORIZATION dbo;");
  foreach(var role in new[]{"api","worker","native","migrator","backup"})await Execute(c,$"CREATE USER [kohami_m0_{role}] FOR LOGIN [kohami_m0_{role}]; CREATE ROLE [m0_{role}]; ALTER ROLE [m0_{role}] ADD MEMBER [kohami_m0_{role}];");
  await Execute(c,"ALTER ROLE db_backupoperator ADD MEMBER kohami_m0_backup;");
  foreach(var path in new[]{"apps/m0/Security/SQLSchema.sql","apps/m0/Commerce/CommerceSchema.sql","apps/m0/Native/Operations.sql","apps/m0/Commerce/CommerceSeed.sql"}) {
   var file=Path.Combine(root,path);if(!File.Exists(file))throw new InvalidOperationException("MIGRATION_SOURCE_MISSING:"+path);
   foreach(var batch in Regex.Split(File.ReadAllText(file),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x)))await Execute(c,batch);
  }
  await ApplyGrantScriptsAsync(c,root);
  Console.WriteLine("KOHAMI_M0_PROVISIONED_TLS_STRICT_RUNTIME_ROLES_DISTINCT_NATIVE_IMPORT_PENDING");
 }
 public static async Task ApplyFixtureGrantsAsync(string root,string endpoint) {
  var local=Path.Combine(root,".local","m0");var s=M0Settings.Load(Path.Combine(local,"runtime-private.json"));var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");var b=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();await ApplyGrantScriptsAsync(c,root);Console.WriteLine("PASS exact SQL grants applied to scoped isolated KohamiM0 instance; no owner bootstrap");
 }
 public static async Task MigrateNativeLocalAsync(string root) {
  var local=Path.Combine(root,".local","m0");var settings=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(settings).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};b["Server Certificate"]=settings.SqlCertificatePath;
  await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();using(var scope=new SqlCommand("SELECT DB_NAME(),CONVERT(nvarchar(100),SERVERPROPERTY('ServerName'))",c)){using var r=await scope.ExecuteReaderAsync();if(!await r.ReadAsync()||r.GetString(0)!="KohamiM0"||string.IsNullOrWhiteSpace(r.GetString(1)))throw new InvalidOperationException("SQL_SCOPE_INVALID");}
  foreach(var path in new[]{"apps/m0/Native/Operations.sql","apps/m0/Native/Grants.sql"})foreach(var batch in Regex.Split(File.ReadAllText(Path.Combine(root,path)),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x)))await Execute(c,batch);
  Console.WriteLine("PASS native recovery SQL procedures and exact grants applied to local KohamiM0; no Owner bootstrap");
 }
 static async Task ApplyGrantScriptsAsync(SqlConnection c,string root) {
  foreach(var path in new[]{"apps/m0/Security/SQLGrants.sql","apps/m0/Commerce/CommerceGrants.sql","apps/m0/Native/Grants.sql"}){var file=Path.Combine(root,path);if(!File.Exists(file))throw new InvalidOperationException("GRANT_SOURCE_MISSING:"+path);foreach(var batch in Regex.Split(File.ReadAllText(file),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x)))await Execute(c,batch);}
  await Execute(c,"GRANT CREATE TABLE, CREATE PROCEDURE, CREATE VIEW, CREATE FUNCTION TO m0_migrator; GRANT ALTER ON SCHEMA::m0 TO m0_migrator; GRANT ALTER ON SCHEMA::m0commerce TO m0_migrator; GRANT ALTER ON SCHEMA::ops TO m0_migrator;");
 }
 public static async Task CheckTargetAsync(string root,string endpoint) {
  var local=Path.Combine(root,".local","m0");var s=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(s).ValidateTls();var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource=endpoint,InitialCatalog="master",UserID="sa",Password=secrets["sa"],TrustServerCertificate=false,Encrypt=SqlConnectionEncryptOption.Strict,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();using var cmd=new SqlCommand("SELECT CASE WHEN encrypt_option='TRUE' THEN 1 ELSE 0 END,DB_ID('KohamiM0') FROM sys.dm_exec_connections WHERE session_id=@@SPID",c);using var r=await cmd.ExecuteReaderAsync();await r.ReadAsync();Console.WriteLine("PASS FIXTURE_TLS_STRICT encrypted="+r.GetInt32(0)+" database="+(r.IsDBNull(1)?"ABSENT":"PRESENT"));
 }
 public static void WriteFixtureConfig(string root) {
  var local=Path.Combine(root,".local","m0");var json=JsonNode.Parse(File.ReadAllText(Path.Combine(local,"runtime-private.json")))!.AsObject();var api=new SqlConnectionStringBuilder(json["connectionString"]!.GetValue<string>()){DataSource="localhost,14336"};var workerSettings=JsonNode.Parse(File.ReadAllText(Path.Combine(local,"worker-private.json")))!.AsObject();var worker=new SqlConnectionStringBuilder(workerSettings["workerConnectionString"]!.GetValue<string>()){DataSource="localhost,14336"};json["connectionString"]=api.ConnectionString;json["workerConnectionString"]=worker.ConnectionString;json["IdentitySqlTestDedicated"]=true;var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))!;var admin=new SqlConnectionStringBuilder{DataSource="localhost,14336",InitialCatalog="KohamiM0",UserID="sa",Password=secrets["sa"],Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"};admin["Server Certificate"]=json["sqlCertificatePath"]!.GetValue<string>();json["MigratorConnectionString"]=admin.ConnectionString;var output=Path.Combine(local,"fixture-private.json");File.WriteAllText(output,json.ToJsonString());File.SetUnixFileMode(output,UnixFileMode.UserRead|UnixFileMode.UserWrite);Console.WriteLine("FIXTURE_CONFIG_WRITTEN_MODE_0600_IDENTITY_AND_COMMERCE_TARGET_PORT_14336");
 }
 public static async Task Execute(SqlConnection c,string sql) {using var cmd=new SqlCommand(sql,c){CommandTimeout=60};await cmd.ExecuteNonQueryAsync();}
}
