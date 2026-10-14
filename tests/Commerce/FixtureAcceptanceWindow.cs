using System.Text.Json;
using Kohami.M0.Core;
using Microsoft.Data.SqlClient;

internal static class FixtureAcceptanceWindow {
 const string FixtureConfig = ".local/m0/fixture-private.json";
 const string SqlPath = "tests/Commerce/Issue01Acceptance.sql";

 public static async Task RunAsync(string action,string root,string[] options) {
  root=Path.GetFullPath(root);
  if(action is not ("open" or "close" or "price"))throw new InvalidOperationException("ISSUE01_WINDOW_ACTION_INVALID");
  var configured=Environment.GetEnvironmentVariable("KOHAMI_M0_TEST_CONFIG");
  var expected=Path.GetFullPath(Path.Combine(root,FixtureConfig));
  if(configured is null||Path.GetFullPath(configured)!=expected||File.ResolveLinkTarget(expected,false) is not null)
   throw new InvalidOperationException("DEDICATED_14336_FIXTURE_CONFIG_REQUIRED");
  var json=JsonDocument.Parse(await File.ReadAllTextAsync(expected));
  var document=json.RootElement;
  if(!document.TryGetProperty("IdentitySqlTestDedicated",out var dedicated)||!dedicated.GetBoolean())
   throw new InvalidOperationException("DEDICATED_14336_FIXTURE_REQUIRED");
  var settings=M0Settings.Load(expected);new M0Sql(settings).ValidateTls();
  var api=new SqlConnectionStringBuilder(document.GetProperty("connectionString").GetString());
  var worker=new SqlConnectionStringBuilder(document.GetProperty("workerConnectionString").GetString());
  var admin=new SqlConnectionStringBuilder(document.GetProperty("MigratorConnectionString").GetString()) {
   Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate="localhost"
  };
  admin["Server Certificate"]=settings.SqlCertificatePath;
  if(api.DataSource!="localhost,14336"||worker.DataSource!="localhost,14336"||admin.DataSource!="localhost,14336"||
     api.InitialCatalog!="KohamiM0"||worker.InitialCatalog!="KohamiM0"||admin.InitialCatalog!="KohamiM0"||
     settings.DatasetId!="demo_m0"||settings.Origin!="https://localhost:3000")
   throw new InvalidOperationException("ISSUE01_FIXTURE_TARGET_MISMATCH");
  if(!File.Exists(Path.Combine(root,SqlPath)))throw new InvalidOperationException("ISSUE01_FIXTURE_SQL_MISSING");
  await using var connection=new SqlConnection(admin.ConnectionString);await connection.OpenAsync();
  await VerifyTarget(connection);
  foreach(var batch in System.Text.RegularExpressions.Regex.Split(await File.ReadAllTextAsync(Path.Combine(root,SqlPath)),@"^\s*GO\s*$",System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x))) {
   await using var deploy=new SqlCommand(batch,connection){CommandTimeout=30};await deploy.ExecuteNonQueryAsync();
  }
  if(action=="open") {
   if(options.Length!=0)throw new InvalidOperationException("ISSUE01_WINDOW_ID_NOT_ALLOWED_ON_OPEN");
   var openedWindowId=Guid.NewGuid();
   await using var open=new SqlCommand("EXEC ops.OpenIssue01AcceptanceWindow @WindowId,@Dataset,@Fingerprint",connection);
   open.Parameters.AddWithValue("@WindowId",openedWindowId);open.Parameters.AddWithValue("@Dataset",settings.DatasetId);
   open.Parameters.AddWithValue("@Fingerprint",await Fingerprint(connection));
   await open.ExecuteNonQueryAsync();
   Console.WriteLine("ISSUE01_FIXTURE_WINDOW_OPEN "+openedWindowId.ToString("D"));
   return;
  }
  if(!Guid.TryParse(options.ElementAtOrDefault(0),out var windowId))throw new InvalidOperationException("ISSUE01_WINDOW_ID_REQUIRED");
  var fingerprint=await Fingerprint(connection);
  if(action=="close") {
   if(options.Length!=1)throw new InvalidOperationException("ISSUE01_CLOSE_ARGUMENTS_INVALID");
   await using var close=new SqlCommand("EXEC ops.CloseIssue01AcceptanceWindow @WindowId,@Dataset,@Fingerprint",connection);
   close.Parameters.AddWithValue("@WindowId",windowId);close.Parameters.AddWithValue("@Dataset",settings.DatasetId);close.Parameters.AddWithValue("@Fingerprint",fingerprint);
   await close.ExecuteNonQueryAsync();Console.WriteLine("ISSUE01_FIXTURE_WINDOW_CLOSED "+windowId.ToString("D"));return;
  }
  if(options.Length!=3||options[1] is not ("sku_demo_01" or "sku_demo_02" or "sku_demo_03")||!int.TryParse(options[2],out var price)||price is <1 or >1000000)
   throw new InvalidOperationException("ISSUE01_FIXTURE_PRICE_ARGUMENTS_INVALID");
  await using var setPrice=new SqlCommand("EXEC ops.SetIssue01FixturePrice @WindowId,@Dataset,@Fingerprint,@Sku,@Price",connection);
  setPrice.Parameters.AddWithValue("@WindowId",windowId);setPrice.Parameters.AddWithValue("@Dataset",settings.DatasetId);setPrice.Parameters.AddWithValue("@Fingerprint",fingerprint);
  setPrice.Parameters.AddWithValue("@Sku",options[1]);setPrice.Parameters.AddWithValue("@Price",price);await setPrice.ExecuteNonQueryAsync();
  Console.WriteLine("ISSUE01_FIXTURE_SYNTHETIC_PRICE_SET "+options[1]+" "+price);
 }

 static async Task VerifyTarget(SqlConnection connection) {
  await using var command=new SqlCommand("SELECT DB_NAME(),CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),Maintenance FROM m0.SecurityState WHERE Id=1",connection);
  await using var result=await command.ExecuteReaderAsync();
  if(!await result.ReadAsync()||result.GetString(0)!="KohamiM0"||string.IsNullOrWhiteSpace(result.GetString(1)))
   throw new InvalidOperationException("ISSUE01_SQL_TARGET_MISMATCH");
 }
 static async Task<string> Fingerprint(SqlConnection connection) {
  await using var command=new SqlCommand("SELECT LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))",connection);
  return (string)(await command.ExecuteScalarAsync()??throw new InvalidOperationException("ISSUE01_SQL_FINGERPRINT_MISSING"));
 }
}
