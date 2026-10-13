using System.Text.Json;
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
 public static async Task RunAsync(string root) {
  var local=Path.Combine(root,".local","m0");var s=M0Settings.Load(Path.Combine(local,"runtime-private.json"));new M0Sql(s).ValidateTls();
  var secrets=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(local,"provisioner-private.json")))??throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
  var b=new SqlConnectionStringBuilder{DataSource="localhost,14334",InitialCatalog="master",UserID="sa",Password=secrets["sa"],TrustServerCertificate=false,Encrypt=SqlConnectionEncryptOption.Strict,HostNameInCertificate="localhost"};b["Server Certificate"]=s.SqlCertificatePath;
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
  foreach(var path in new[]{"apps/m0/Security/SQLGrants.sql","apps/m0/Commerce/CommerceGrants.sql","apps/m0/Native/Grants.sql"}) {
   var file=Path.Combine(root,path);if(!File.Exists(file))throw new InvalidOperationException("GRANT_SOURCE_MISSING:"+path);await Execute(c,File.ReadAllText(file));
  }
  // No db_owner membership and no permanent server-level restore principal is created.
  await Execute(c,"GRANT CREATE TABLE, CREATE PROCEDURE, CREATE VIEW, CREATE FUNCTION TO m0_migrator; GRANT ALTER ON SCHEMA::m0 TO m0_migrator; GRANT ALTER ON SCHEMA::m0commerce TO m0_migrator; GRANT ALTER ON SCHEMA::ops TO m0_migrator;");
  Console.WriteLine("KOHAMI_M0_PROVISIONED_TLS_STRICT_RUNTIME_ROLES_DISTINCT_NATIVE_IMPORT_PENDING");
 }
 public static async Task Execute(SqlConnection c,string sql) {using var cmd=new SqlCommand(sql,c){CommandTimeout=60};await cmd.ExecuteNonQueryAsync();}
}
