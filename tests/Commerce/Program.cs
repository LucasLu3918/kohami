using System.Text.Json.Nodes;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Kohami.M0.Commerce;
using Kohami.Commerce.Domain;
using Kohami.M0.Core;
using Kohami.Catalog.Tests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

static string SyntheticEmail(string localPart) => localPart + "@" + "example.test";

if (args.Length is >= 3 and <= 6 && args[0] == "fixture-window") {
 await FixtureAcceptanceWindow.RunAsync(args[1], args[2], args.Skip(3).ToArray());
 return;
}
if (args.Length == 6 && args[0] == "m0-ac02-verify") {
 await M0Ac02Acceptance.VerifyAsync(args[1], args[2], args[3], args[4], args[5]);
 return;
}
if (args.Length == 4 && args[0] == "m0-ac02-open") {
 await M0Ac02Acceptance.OpenAsync(args[1], args[2], args[3]);
 return;
}
if (args.Length == 4 && args[0] == "m0-ac02-cleanup") {
 await M0Ac02Acceptance.CleanupAsync(args[1], args[2], args[3]);
 return;
}
if (args.Length is >= 3 and <= 4 && args[0] == "issue06-owner-seed") {
 await SyntheticOwnerAcceptance.SeedAsync(args[1], args[2], args.Length == 4 ? args[3] : null);
 return;
}
if (args.Length == 4 && args[0] == "issue06-owner-capabilities") {
 await SyntheticOwnerAcceptance.AddGuestCapabilitiesAsync(args[1], args[2], args[3]);
 return;
}
if (args.Length == 5 && args[0] == "issue06-owner-verify") {
 await SyntheticOwnerAcceptance.VerifyAftercareAsync(args[1], args[2], args[3], args[4]);
 return;
}
if (args.Length == 5 && args[0] == "issue06-owner-worker-state") {
 await SyntheticOwnerAcceptance.ReportWorkerStateAsync(args[1], args[2], args[3], args[4]);
 return;
}
if (args.Length == 3 && args[0] == "issue06-owner-cleanup") {
 await SyntheticOwnerAcceptance.CleanupAsync(args[1], args[2]);
 return;
}
if (args.Length == 3 && args[0] == "issue08-catalog-owner-grant") {
 await CatalogSqlAcceptance.AddOwnerGrantAsync(args[1], args[2]);
 return;
}
if (args.Length == 3 && args[0] == "issue09-stock-owner-grant") {
 await CatalogSqlAcceptance.AddStockOwnerGrantAsync(args[1], args[2]);
 return;
}
if (args.Length == 3 && args[0] == "issue08-catalog-verify") {
 await CatalogSqlAcceptance.VerifyAndCleanupAsync(args[1], args[2]);
 return;
}
if (args.Length == 3 && args[0] == "issue08-catalog-cleanup") {
 await CatalogSqlAcceptance.CleanupFromEvidenceAsync(args[1], args[2]);
 return;
}
if (args.Length == 3 && args[0] == "issue08-catalog-fault") {
 switch (args[2]) {
  case "save-on": await CatalogSqlAcceptance.SetSaveFailureAsync(args[1], true); break;
  case "save-off": await CatalogSqlAcceptance.SetSaveFailureAsync(args[1], false); break;
  case "read-on": await CatalogSqlAcceptance.SetReadFailureAsync(args[1], true); break;
  case "read-off": await CatalogSqlAcceptance.SetReadFailureAsync(args[1], false); break;
  default: throw new InvalidOperationException("ISSUE08_FAULT_MODE_INVALID");
 }
 return;
}
if (args.Length == 3 && args[0] == "issue08-catalog-save-failure-verify") {
 await CatalogSqlAcceptance.VerifySaveFailureAsync(args[1], args[2]);
 return;
}
if (args.Length == 2 && args[0] == "issue09-stock-sql") {
 await InventoryAdjustmentSqlAcceptance.RunAsync(args[1]);
 return;
}
if (args.Length == 3 && args[0] == "issue10-stock-zero") {
 await CatalogSearchSqlAcceptance.MakeOneShowcaseSkuUnavailableAsync(args[1], args[2]);
 return;
}
if (args.Length == 3 && args[0] == "issue10-stock-restore") {
 await CatalogSearchSqlAcceptance.RestoreShowcaseStockAsync(args[1], args[2]);
 return;
}
if (args.Length == 3 && args[0] == "issue11-cart-cleanup") {
 await Issue11CartAcceptance.CleanupAsync(args[1], args[2]);
 return;
}
if (args.Length == 5 && args[0] == "issue11-inventory-change") {
 await Issue11CartAcceptance.ChangeInventoryAsync(args[1], args[2], args[3], args[4]);
 return;
}
if (args.Length == 3 && args[0] == "issue11-inventory-restore") {
 await Issue11CartAcceptance.RestoreInventoryAsync(args[1], args[2]);
 return;
}
if (args.Length == 4 && args[0] == "issue12-fixture") {
 switch (args[1]) {
  case "prepare": await Issue12SqlAcceptance.PrepareAsync(args[2], args[3]); break;
  case "set-second-zero": await Issue12SqlAcceptance.SetSecondStockAsync(args[2], args[3]); break;
  case "set-one": await Issue12SqlAcceptance.SetStocksAsync(args[2], args[3]); break;
  default: throw new InvalidOperationException("ISSUE12_FIXTURE_COMMAND_INVALID");
 }
 return;
}
if (args.Length == 5 && args[0] == "issue12-fixture" && args[1] == "verify-rollback") {
 await Issue12SqlAcceptance.VerifyRollbackAsync(args[2], args[3], args[4]);
 return;
}
if (args.Length == 5 && args[0] == "issue12-fixture" && args[1] == "cleanup") {
 await Issue12SqlAcceptance.CleanupAsync(args[2], args[3], args[4]);
 return;
}
if (args.Length == 10 && args[0] == "worker-race-child") {
 await TwoWorkerPaymentRace.RunChildAsync(args);
 return;
}
if (args.Length == 5 && args[0] == "worker-lease-child") {
 await WorkerRecoveryMatrix.RunChildAsync(args);
 return;
}

static void Check(bool pass,string name){if(!pass)throw new InvalidOperationException("FAILED: "+name);Console.WriteLine("PASS: "+name);}
CatalogDomainAcceptance.Run();
InventoryAdjustmentRulesAcceptance.Run();
var a=new JsonArray(CommerceWire.Item("sku_demo_01",1,500,1),CommerceWire.Item("sku_demo_03",2,650,10));
var s=CommerceWire.Snapshot(a,60,true);
Check(s.Number("totalTwd")==1590&&s.Number("discountTwd")==270,"integer discount total");
var units=s["lines"]!.AsArray().SelectMany(x=>x!["units"]!.AsArray()).Select(x=>x!.AsObject().Number("paidTwd")).ToArray();
Check(units.SequenceEqual(new long[]{425,552,553}),"stable per-unit remainder allocation");
var reversed=new JsonArray(a[1]!.DeepClone(),a[0]!.DeepClone());Check(CommerceWire.Snapshot(reversed,60,true).ToJsonString()==s.ToJsonString(),"SKU ordering stable across input order");
var supportedHomeFee=MockShippingFeePolicy.Resolve("mock_home","demo_supported");
Check(supportedHomeFee is { AmountTwd: 60, Version: "fee_demo_v1" },"supported home delivery resolves an explicit versioned DEMO fee");
var supportedPickupFee=MockShippingFeePolicy.Resolve("mock_pickup","demo_supported");
Check(supportedPickupFee is { AmountTwd: 0, Version: "fee_demo_v1" },"supported pickup resolves an explicit zero-fee row instead of a fallback");
Check(MockShippingFeePolicy.Resolve("mock_home","demo_unsupported") is null&&MockShippingFeePolicy.Resolve("unsupported","demo_supported") is null,"unsupported delivery methods and regions have no fee row");
var capturedFeeSnapshot=CommerceWire.Snapshot(a,supportedHomeFee!,true);
_ = CommerceWire.Snapshot(a,supportedPickupFee!,true);
Check(capturedFeeSnapshot.Number("shippingTwd")==60&&capturedFeeSnapshot.Text("feeVersion")=="fee_demo_v1","later fee resolution cannot overwrite an earlier quote snapshot");
Check(s["lines"]!.AsArray().All(x=>x!["orderLineId"] is null&&x["units"]!.AsArray().All(u=>u!["unitId"] is null)),"quote has no order identities");
var bad=CommerceWire.Obj(("quantity",1),("amountTwd",1));try{CommerceWire.Fields(bad,["quantity"]);throw new InvalidOperationException("FAILED fields");}catch(M0Exception e){Check(e.Status==422,"reject client amount override");}
Check(CommerceWire.Version("Checkout","demo",1)!=CommerceWire.Version("Checkout","demo",2),"aggregate version changes");
var ctx=new DefaultHttpContext();ctx.Request.Headers.IfMatch='"'+CommerceWire.Version("Checkout","demo",1)+'"';var doc=new CommerceDocument("Checkout","demo","browser",1,new JsonObject());CommerceWire.Expected(ctx,doc,CommerceWire.Obj(("expectedVersion",CommerceWire.Version("Checkout","demo",1))));
try{CommerceWire.Expected(ctx,doc,CommerceWire.Obj(("expectedVersion",CommerceWire.Version("Checkout","demo",2))));throw new InvalidOperationException("FAILED stale");}catch(M0Exception e){Check(e.Status==412,"body and header same aggregate");}
Check(CommerceWire.Version("Checkout","demo",1)!=CommerceWire.Version("Checkout","other",1),"same numeric version cannot cross resources");
Check(CommerceWire.Version("Checkout","demo",1)!=CommerceWire.Version("Order","demo",1),"same id cannot cross aggregate kind");
ctx.Request.Headers.IfMatch='"'+CommerceWire.Version("Checkout","other",1)+'"';try{CommerceWire.Expected(ctx,doc);throw new InvalidOperationException("FAILED cross resource");}catch(M0Exception e){Check(e.Status==412,"cross-resource header rejected");}
ctx.Request.Headers.IfMatch="W/\""+CommerceWire.Version("Checkout","demo",1)+"\"";try{CommerceWire.Expected(ctx,doc);throw new InvalidOperationException("FAILED weak");}catch(M0Exception e){Check(e.Status==412,"weak ETag rejected");}
ctx.Request.Headers.IfMatch="";try{CommerceWire.Expected(ctx,doc);throw new InvalidOperationException("FAILED missing");}catch(M0Exception e){Check(e.Status==428,"missing ETag fails precondition");}
var config=Environment.GetEnvironmentVariable("KOHAMI_M0_TEST_CONFIG");if(config is null){Console.WriteLine("SQL product checks NOT_EXECUTED: KOHAMI_M0_TEST_CONFIG missing");return;}
var settings=M0Settings.Load(config);using var raw=JsonDocument.Parse(File.ReadAllText(config));if(!raw.RootElement.TryGetProperty("IdentitySqlTestDedicated",out var isolated)||!isolated.GetBoolean())throw new InvalidOperationException("DEDICATED_SQL_FIXTURE_REQUIRED");var admin=new SqlConnectionStringBuilder(raw.RootElement.GetProperty("MigratorConnectionString").GetString()){Encrypt=SqlConnectionEncryptOption.Strict,TrustServerCertificate=false,HostNameInCertificate=settings.SqlServerName};admin["Server Certificate"]=settings.SqlCertificatePath;
async Task SetMaintenance(bool value){await using var c=new SqlConnection(admin.ConnectionString);await c.OpenAsync();await using var cmd=new SqlCommand("UPDATE m0.SecurityState SET Maintenance=@m WHERE Id=1",c);cmd.Parameters.AddWithValue("@m",value);if(await cmd.ExecuteNonQueryAsync()!=1)throw new InvalidOperationException("FIXTURE_STATE_INVALID");}
async Task VerifyTransferResumeAfterActivation(){
 var transfer=Guid.NewGuid();var activation=Guid.NewGuid();var activationPermit=Guid.NewGuid();var activationReconcilePermit=Guid.NewGuid();var resumePermit=Guid.NewGuid();var owner="resume_owner_"+Guid.NewGuid().ToString("N");var oldOwner="old_owner_fixture";var session="session_"+Guid.NewGuid().ToString("N");var reauthSession="session_"+Guid.NewGuid().ToString("N");var grant="grant_"+Guid.NewGuid().ToString("N");long originalEpoch=0;bool originalMaintenance=false,originalOwnerPrepared=false,originalBootstrapActivated=false;string? originalTarget=null,originalBootstrapUser=null;Guid? originalBootstrapOperation=null;
 await using(var c=new SqlConnection(admin.ConnectionString)){await c.OpenAsync();await using var seed=new SqlCommand(@"SELECT @oldEpoch=Epoch,@oldMaintenance=Maintenance,@oldTarget=MaintenanceTargetUserId,@oldPrepared=OwnerPrepared,@oldActivated=BootstrapActivated,@oldBootstrapOp=BootstrapOperationId,@oldBootstrapUser=BootstrapUserId FROM m0.SecurityState WHERE Id=1;
 UPDATE m0.SecurityState SET Epoch=@oldEpoch+1,Maintenance=1,MaintenanceTargetUserId=@owner,OwnerPrepared=1,BootstrapActivated=0,BootstrapOperationId=@transfer,BootstrapUserId=@owner WHERE Id=1;
 DECLARE @now datetimeoffset=SYSDATETIMEOFFSET();
 INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion) VALUES(@owner,@email,REPLICATE(N'x',100),1,0,'Active',1);
 INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason) VALUES(@grant,@owner,'Owner','access.manage','Dataset','demo_m0',DATEADD(minute,-1,@now),DATEADD(day,1,@now),'fixture','isolated transfer resume test');
 INSERT m0.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce,AuthenticatedAt) VALUES(@session,@owner,1,'AdminAuthenticated',DATEADD(minute,5,@now),DATEADD(hour,1,@now),'fixture_nonce',@now);
 INSERT ops.Audit(OperationId,Action,Target,PreviewHash,OldEpoch,NewEpoch,AppliedAt,HostReconciled,ReplacementTarget) VALUES(@transfer,'TransferDemoOwner',@oldOwner,@hash,@oldEpoch,@oldEpoch+1,@now,0,@owner);
 SELECT @oldEpoch,@oldMaintenance,@oldTarget,@oldPrepared,@oldActivated,@oldBootstrapOp,@oldBootstrapUser",c);seed.Parameters.AddWithValue("@owner",owner);seed.Parameters.AddWithValue("@email",owner+"@example.test");seed.Parameters.AddWithValue("@grant",grant);seed.Parameters.AddWithValue("@session",session);seed.Parameters.AddWithValue("@transfer",transfer);seed.Parameters.AddWithValue("@oldOwner",oldOwner);seed.Parameters.AddWithValue("@hash",new string('a',64));var oldEpoch=seed.Parameters.Add("@oldEpoch",System.Data.SqlDbType.BigInt);oldEpoch.Direction=System.Data.ParameterDirection.Output;var oldMaintenance=seed.Parameters.Add("@oldMaintenance",System.Data.SqlDbType.Bit);oldMaintenance.Direction=System.Data.ParameterDirection.Output;var oldTarget=seed.Parameters.Add("@oldTarget",System.Data.SqlDbType.NVarChar,100);oldTarget.Direction=System.Data.ParameterDirection.Output;var oldPrepared=seed.Parameters.Add("@oldPrepared",System.Data.SqlDbType.Bit);oldPrepared.Direction=System.Data.ParameterDirection.Output;var oldActivated=seed.Parameters.Add("@oldActivated",System.Data.SqlDbType.Bit);oldActivated.Direction=System.Data.ParameterDirection.Output;var oldBootstrapOp=seed.Parameters.Add("@oldBootstrapOp",System.Data.SqlDbType.UniqueIdentifier);oldBootstrapOp.Direction=System.Data.ParameterDirection.Output;var oldBootstrapUser=seed.Parameters.Add("@oldBootstrapUser",System.Data.SqlDbType.NVarChar,100);oldBootstrapUser.Direction=System.Data.ParameterDirection.Output;await seed.ExecuteNonQueryAsync();originalEpoch=(long)oldEpoch.Value;originalMaintenance=(bool)oldMaintenance.Value;originalTarget=oldTarget.Value is DBNull?null:(string)oldTarget.Value;originalOwnerPrepared=(bool)oldPrepared.Value;originalBootstrapActivated=(bool)oldActivated.Value;originalBootstrapOperation=oldBootstrapOp.Value is DBNull?null:(Guid)oldBootstrapOp.Value;originalBootstrapUser=oldBootstrapUser.Value is DBNull?null:(string)oldBootstrapUser.Value;}
 try{await using var c=new SqlConnection(admin.ConnectionString);await c.OpenAsync();var issued=DateTimeOffset.UtcNow.AddMinutes(-1);var expiry=issued.AddMinutes(10);var activationHash=new string('b',64);
  await using(var apply=new SqlCommand(@"DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));EXEC ops.Apply @PermitId,@OperationId,'InitialOwnerActivate',@Target,'demo_m0',@fp,@PreviewHash,@epoch,@issued,@expires",c)){apply.Parameters.AddWithValue("@PermitId",activationPermit);apply.Parameters.AddWithValue("@OperationId",activation);apply.Parameters.AddWithValue("@Target",owner);apply.Parameters.AddWithValue("@PreviewHash",activationHash);apply.Parameters.AddWithValue("@epoch",originalEpoch+1);apply.Parameters.AddWithValue("@issued",issued);apply.Parameters.AddWithValue("@expires",expiry);await apply.ExecuteNonQueryAsync();}
  await using(var verifyTarget=new SqlCommand("SELECT Maintenance,MaintenanceTargetUserId FROM m0.SecurityState WHERE Id=1",c)){await using var result=await verifyTarget.ExecuteReaderAsync();await result.ReadAsync();Check(result.GetBoolean(0)&&result.GetString(1)==owner,"Owner activation keeps its maintenance login target for account recovery");}
  await using(var reconcileActivation=new SqlCommand(@"DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));EXEC ops.Reconcile @PermitId,@OperationId,'InitialOwnerActivate',@Target,'demo_m0',@fp,@PreviewHash,@permitEpoch,@epoch,@issued,@expires",c)){reconcileActivation.Parameters.AddWithValue("@PermitId",activationReconcilePermit);reconcileActivation.Parameters.AddWithValue("@OperationId",activation);reconcileActivation.Parameters.AddWithValue("@Target",owner);reconcileActivation.Parameters.AddWithValue("@PreviewHash",activationHash);reconcileActivation.Parameters.AddWithValue("@permitEpoch",originalEpoch+1);reconcileActivation.Parameters.AddWithValue("@epoch",originalEpoch+2);reconcileActivation.Parameters.AddWithValue("@issued",issued);reconcileActivation.Parameters.AddWithValue("@expires",expiry);await reconcileActivation.ExecuteNonQueryAsync();}
  await using(var expire=new SqlCommand("UPDATE m0.Sessions SET ExpiresAt=DATEADD(second,-1,SYSDATETIMEOFFSET()),AuthenticatedAt=DATEADD(minute,-6,SYSDATETIMEOFFSET()) WHERE SessionId=@id",c)){expire.Parameters.AddWithValue("@id",session);await expire.ExecuteNonQueryAsync();}
  await using(var reauth=new SqlCommand(@"BEGIN TRANSACTION;DECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='kohami.m0.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;IF @lock<0 THROW 51000,'TEST_LOCK_FAILED',1;DECLARE @now datetimeoffset=SYSDATETIMEOFFSET();EXEC m0.security_create_session @UserId,@SecurityVersion,@SessionId,@Nonce,@now;SELECT Stage FROM m0.Sessions WHERE SessionId=@SessionId;COMMIT",c)){reauth.Parameters.AddWithValue("@UserId",owner);reauth.Parameters.AddWithValue("@SecurityVersion",1L);reauth.Parameters.AddWithValue("@SessionId",reauthSession);reauth.Parameters.AddWithValue("@Nonce",Guid.NewGuid().ToString("N"));var stage=(string?)await reauth.ExecuteScalarAsync();Check(stage=="AdminAuthenticated","maintenance target Owner can reauthenticate after session expiry");}
  await using(var refresh=new SqlCommand("UPDATE m0.Sessions SET Stage='AdminAuthenticated',ExpiresAt=DATEADD(minute,5,SYSDATETIMEOFFSET()),AbsoluteUntil=DATEADD(hour,1,SYSDATETIMEOFFSET()),AuthenticatedAt=SYSDATETIMEOFFSET() WHERE SessionId=@id",c)){refresh.Parameters.AddWithValue("@id",reauthSession);await refresh.ExecuteNonQueryAsync();}
  await using(var reconcileTransfer=new SqlCommand(@"DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));EXEC ops.Reconcile @PermitId,@OperationId,'TransferDemoOwner',@Target,'demo_m0',@fp,@PreviewHash,@epoch,@epoch,@issued,@expires",c)){reconcileTransfer.Parameters.AddWithValue("@PermitId",resumePermit);reconcileTransfer.Parameters.AddWithValue("@OperationId",transfer);reconcileTransfer.Parameters.AddWithValue("@Target",oldOwner);reconcileTransfer.Parameters.AddWithValue("@PreviewHash",new string('a',64));reconcileTransfer.Parameters.AddWithValue("@epoch",originalEpoch+2);reconcileTransfer.Parameters.AddWithValue("@issued",issued);reconcileTransfer.Parameters.AddWithValue("@expires",expiry);await reconcileTransfer.ExecuteNonQueryAsync();}
  await using var verify=new SqlCommand("SELECT a.HostReconciled,st.Maintenance FROM ops.Audit a CROSS JOIN m0.SecurityState st WHERE a.OperationId=@id AND st.Id=1",c);verify.Parameters.AddWithValue("@id",transfer);await using var result2=await verify.ExecuteReaderAsync();await result2.ReadAsync();Check(result2.GetBoolean(0)&&!result2.GetBoolean(1),"Owner transfer resume after reauthentication reconciles and clears maintenance");
 }finally{await using var cleanup=new SqlConnection(admin.ConnectionString);await cleanup.OpenAsync();await using var remove=new SqlCommand(@"DELETE ops.ReconcileUses WHERE OperationId IN(@transfer,@activation);DELETE ops.PermitUses WHERE OperationId IN(@transfer,@activation);DELETE ops.Audit WHERE OperationId IN(@transfer,@activation);DELETE m0.SecurityAudit WHERE OperationId=@activation;DELETE m0.Sessions WHERE SessionId IN(@session,@reauthSession);DELETE m0.Grants WHERE GrantId=@grant;DELETE m0.Users WHERE UserId=@owner;UPDATE m0.SecurityState SET Epoch=@epoch,Maintenance=@maintenance,MaintenanceTargetUserId=@target,OwnerPrepared=@ownerPrepared,BootstrapActivated=@bootstrapActivated,BootstrapOperationId=@bootstrapOperation,BootstrapUserId=@bootstrapUser WHERE Id=1",cleanup);remove.Parameters.AddWithValue("@transfer",transfer);remove.Parameters.AddWithValue("@activation",activation);remove.Parameters.AddWithValue("@session",session);remove.Parameters.AddWithValue("@reauthSession",reauthSession);remove.Parameters.AddWithValue("@grant",grant);remove.Parameters.AddWithValue("@owner",owner);remove.Parameters.AddWithValue("@epoch",originalEpoch);remove.Parameters.AddWithValue("@maintenance",originalMaintenance);remove.Parameters.AddWithValue("@target",(object?)originalTarget??DBNull.Value);remove.Parameters.AddWithValue("@ownerPrepared",originalOwnerPrepared);remove.Parameters.AddWithValue("@bootstrapActivated",originalBootstrapActivated);remove.Parameters.AddWithValue("@bootstrapOperation",(object?)originalBootstrapOperation??DBNull.Value);remove.Parameters.AddWithValue("@bootstrapUser",(object?)originalBootstrapUser??DBNull.Value);await remove.ExecuteNonQueryAsync();}
}
var store=new CommerceStore(new M0Sql(settings));var worker=new MockWorker(store,TimeProvider.System,NullLogger<MockWorker>.Instance);
var ephemeralProtection=new EphemeralDataProtectionProvider();
await SetMaintenance(false);
await DatasetForeignKeyAcceptance.RunAsync(admin.ConnectionString);
await DatasetReadIsolationAcceptance.RunAsync(store,settings,admin.ConnectionString);
await PaymentDeadlineAcceptance.RunAsync(config,admin.ConnectionString);
await RefundAcceptance.RunAsync(config,admin.ConnectionString);
await TwoWorkerPaymentRace.RunAsync(config,admin.ConnectionString);
await WorkerRecoveryMatrix.RunAsync(config,admin.ConnectionString);
var orderReadId=M0Http.Id("order");
var orderReadBody=CommerceWire.Obj(("id",orderReadId),("orderState","Confirmed"),("placedAt",DateTimeOffset.UtcNow),("snapshot",CommerceWire.Obj(("lines",new JsonArray()))),("paymentState","Succeeded"),("compensationRequired",false),("email",SyntheticEmail("buyer")));
await store.Transaction(async tx=>{await tx.Save(new("Order",orderReadId,"browser_acceptance_owner",1,orderReadBody),true);return true;});
try {
 var orderReadContext=new DefaultHttpContext();orderReadContext.RequestServices=new ReconciliationResultServices();orderReadContext.Response.Body=new MemoryStream();
 var orderReadService=new CommerceService(store,new M0Crypto(ephemeralProtection),new OrderReadFixtureSecurity(true),TimeProvider.System);
 var orderReadResult=await orderReadService.Order(orderReadContext,orderReadId);await orderReadResult.ExecuteAsync(orderReadContext);
 orderReadContext.Response.Body.Position=0;var orderReadJson=await System.Text.Json.JsonDocument.ParseAsync(orderReadContext.Response.Body);
 Check(orderReadContext.Response.StatusCode==200&&orderReadJson.RootElement.GetProperty("maskedContact").GetString()=="***@example.test","dataset-authorized Owner reads masked guest order for aftercare");
 var deniedOrderContext=new DefaultHttpContext();
 try { await new CommerceService(store,new M0Crypto(ephemeralProtection),new OrderReadFixtureSecurity(false),TimeProvider.System).Order(deniedOrderContext,orderReadId);throw new InvalidOperationException("FAILED: order read without orders.read was permitted"); }
 catch(M0Exception e) when(e.Status==403){Check(true,"guest order read without capability and staff without orders.read is denied");}
} finally {await using var removeOrderRead=new SqlConnection(admin.ConnectionString);await removeOrderRead.OpenAsync();await using var remove=new SqlCommand("DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@id",removeOrderRead);remove.Parameters.AddWithValue("@id",orderReadId);await remove.ExecuteNonQueryAsync();}
// Exercise the manual reconciliation read through the real SQL-backed service. The
// fixture security adapter grants only this read permission; account login is covered by
// Identity tests and is not claimed as evidence by this business-path check.
var reconciliationService=new CommerceService(store,new M0Crypto(ephemeralProtection),new ReconciliationFixtureSecurity(),TimeProvider.System);
async Task<(string collections,string receipts)> ReconciliationRows(){await using var c=new SqlConnection(settings.ConnectionString);await c.OpenAsync();await using var q=new SqlCommand(@"SELECT COALESCE((SELECT Id,OrderId,Kind,Collected,Pending,Refunded,ShippingPaid,ShippingSlot FROM m0commerce.Collections WHERE Dataset='demo_m0' ORDER BY Id FOR JSON PATH),N'[]'),COALESCE((SELECT Id,Kind,Body FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind IN('DemoReceipt','ReceiptAdjustment') ORDER BY Id FOR JSON PATH),N'[]')",c);await using var reader=await q.ExecuteReaderAsync();await reader.ReadAsync();return(reader.GetString(0),reader.GetString(1));}
// Restore only the disposable synthetic fixture rows used by this check; never target the demo database.
await using(var reset=new SqlConnection(admin.ConnectionString)){await reset.OpenAsync();await using var command=new SqlCommand(@"UPDATE m0commerce.Inventory SET Stock=1 WHERE Dataset='demo_m0' AND Sku='sku_demo_01'; DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId LIKE 'race\_%' ESCAPE '\'; DELETE m0commerce.Inbox WHERE Dataset='demo_m0' AND SourceId LIKE 'maintenance_started_%_terminal'; DELETE m0commerce.Jobs WHERE Dataset='demo_m0' AND Actor='fixture'; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND OwnerId='fixture' AND Id LIKE 'maintenance_started_%';",reset);await command.ExecuteNonQueryAsync();}
var nativeBatches=System.Text.RegularExpressions.Regex.Split(File.ReadAllText("tools/native-operations/PasswordOnlyOperations.sql"),@"^\s*GO\s*$",System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x));
await using(var setup=new SqlConnection(admin.ConnectionString)){await setup.OpenAsync();foreach(var batch in nativeBatches){await using var deploy=new SqlCommand(batch,setup){CommandTimeout=30};await deploy.ExecuteNonQueryAsync();}}
var pendingResumeId=Guid.NewGuid();var pendingResumeHash=new string('a',64);await SetMaintenance(true);
await using(var pending=new SqlConnection(admin.ConnectionString)){await pending.OpenAsync();await using(var seed=new SqlCommand("DECLARE @epoch bigint=(SELECT Epoch FROM m0.SecurityState WHERE Id=1);INSERT ops.Audit(OperationId,Action,Target,PreviewHash,OldEpoch,NewEpoch,AppliedAt,HostReconciled) VALUES(@id,'CredentialRecovery','missing_rebind_fixture',@hash,@epoch-1,@epoch,SYSDATETIMEOFFSET(),0)",pending)){seed.Parameters.AddWithValue("@id",pendingResumeId);seed.Parameters.AddWithValue("@hash",pendingResumeHash);await seed.ExecuteNonQueryAsync();}var now=DateTimeOffset.UtcNow;try{await using var reconcile=new SqlCommand("DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));EXEC ops.Reconcile @PermitId,@OperationId,'CredentialRecovery','missing_rebind_fixture','demo_m0',@fp,@PreviewHash,@epoch,@epoch,@issued,@expires",pending);reconcile.Parameters.AddWithValue("@PermitId",Guid.NewGuid());reconcile.Parameters.AddWithValue("@OperationId",pendingResumeId);reconcile.Parameters.AddWithValue("@PreviewHash",pendingResumeHash);reconcile.Parameters.AddWithValue("@epoch",await new SqlCommand("SELECT Epoch FROM m0.SecurityState WHERE Id=1",pending).ExecuteScalarAsync());reconcile.Parameters.AddWithValue("@issued",now);reconcile.Parameters.AddWithValue("@expires",now.AddMinutes(10));await reconcile.ExecuteNonQueryAsync();throw new InvalidOperationException("FAILED: reconciliation must wait for current account session");}catch(SqlException e) when(e.Number==51000){}await using var verify=new SqlCommand("SELECT HostReconciled,(SELECT Maintenance FROM m0.SecurityState WHERE Id=1) FROM ops.Audit WHERE OperationId=@id",pending);verify.Parameters.AddWithValue("@id",pendingResumeId);await using var result=await verify.ExecuteReaderAsync();await result.ReadAsync();Check(!result.GetBoolean(0)&&result.GetBoolean(1),"native resume cannot clear maintenance before target current account session");}
await using(var cleanup=new SqlConnection(admin.ConnectionString)){await cleanup.OpenAsync();await using var remove=new SqlCommand("DELETE ops.Audit WHERE OperationId=@id",cleanup);remove.Parameters.AddWithValue("@id",pendingResumeId);await remove.ExecuteNonQueryAsync();}await SetMaintenance(false);await VerifyTransferResumeAfterActivation();
var reconciliationBefore=await ReconciliationRows();var reportContext=new DefaultHttpContext();reportContext.RequestServices=new ReconciliationResultServices();await using(var response=new MemoryStream()){reportContext.Response.Body=response;var reportResult=await reconciliationService.ReconciliationReport(reportContext);await reportResult.ExecuteAsync(reportContext);response.Position=0;using var report=await JsonDocument.ParseAsync(response);var root=report.RootElement;Check(reportContext.Response.StatusCode==200&&root.GetProperty("mode").GetString()=="mock"&&root.GetProperty("readOnly").GetBoolean()&&root.GetProperty("scheduleStatus").GetString()=="ManualOnly","manual reconciliation returns a mock read-only report without enabling a schedule");}
Check(await ReconciliationRows()==reconciliationBefore,"manual reconciliation report leaves collection and DEMO receipt SQL rows unchanged");
await VerifyAftercareSqlJourney();
var aftercareTestResidue=Convert.ToInt32(await store.Transaction(tx=>tx.Scalar("SELECT (SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND ((Kind='Cart' AND Id LIKE 'aftercare_cart_%') OR (Kind='Quote' AND JSON_VALUE(Body,'$.cartId') LIKE 'aftercare_cart_%') OR JSON_VALUE(Body,'$.orderId') LIKE 'aftercare_order_%'))+(SELECT COUNT(*) FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND (Actor LIKE 'aftercare_owner_%' OR Actor LIKE 'aftercare_browser_%'))+(SELECT COUNT(*) FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor LIKE 'aftercare_browser_%')")));
Check(aftercareTestResidue==0,"Issue05 SQL journey removes its synthetic carts, quotes, work, and idempotency rows");

async Task VerifyAftercareSqlJourney(){
 var actor=M0Http.Id("aftercare_owner");var browser=M0Http.Id("aftercare_browser");var cartId=M0Http.Id("aftercare_cart");var orderId=M0Http.Id("aftercare_order");var lineId=M0Http.Id("aftercare_line");var unitId=M0Http.Id("aftercare_unit");var collectionId=M0Http.Id("aftercare_collection");var readCapability=M0Http.Id("aftercare_cap");var returnCapability=M0Http.Id("aftercare_cap");var demoControlGrant=M0Http.Id("grant");var receiptId="DEMO-"+collectionId;var retryReceiptId="DEMO-RETRY-"+collectionId;int? stockBefore=null;
 await using(var priorCleanup=new SqlConnection(admin.ConnectionString)){await priorCleanup.OpenAsync();await using var remove=new SqlCommand("DELETE m0commerce.Jobs WHERE Dataset='demo_m0' AND (Actor LIKE 'aftercare_owner_%' OR Actor LIKE 'aftercare_browser_%');DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor LIKE 'aftercare_browser_%';DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND ((Kind='Quote' AND JSON_VALUE(Body,'$.cartId') LIKE 'aftercare_cart_%') OR (Kind='Cart' AND Id LIKE 'aftercare_cart_%'))",priorCleanup);await remove.ExecuteNonQueryAsync();}
 var protection=new EphemeralDataProtectionProvider();var fixtureSecurity=new CommerceJourneyFixtureSecurity(actor);var service=new CommerceService(store,new M0Crypto(protection),fixtureSecurity,TimeProvider.System);
 string Cookie(string name,string value){var payload=CommerceWire.Obj(("value",value),("until",DateTimeOffset.UtcNow.AddHours(1).ToString("O"))).ToJsonString();return name+"="+protection.CreateProtector(name).Protect(payload);}
 HttpContext Request(JsonObject? body,string? version,string key){var h=new DefaultHttpContext();if(body is not null){h.Request.Body=new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body.ToJsonString()));h.Request.ContentType="application/json";}if(version is not null)h.Request.Headers.IfMatch='"'+version+'"';h.Request.Headers["Idempotency-Key"]=key;h.Request.Headers.Cookie=string.Join("; ",new[]{Cookie("kohami.browser",browser),Cookie("kohami.cart",cartId),Cookie("kohami.guest.read",readCapability),Cookie("kohami.guest.action",returnCapability)});return h;}
 JsonObject Body(IResult result)=>result is IValueHttpResult value?JsonSerializer.SerializeToNode(value.Value)?.AsObject()??throw new InvalidOperationException("HTTP_RESULT_BODY_INVALID"):throw new InvalidOperationException("HTTP_RESULT_NOT_JSON");
 try{
  var snapshot=CommerceWire.Snapshot(new JsonArray(CommerceWire.Item("sku_demo_01",1,500,1)),60,false);var line=snapshot["lines"]![0]!.AsObject();line["orderLineId"]=lineId;line["units"]![0]!["unitId"]=unitId;var now=DateTimeOffset.UtcNow;var order=CommerceWire.Obj(("id",orderId),("orderId",orderId),("checkoutId",M0Http.Id("aftercare_checkout")),("mode","mock"),("orderState","Confirmed"),("paymentState","Succeeded"),("compensationRequired",false),("email",SyntheticEmail("buyer")),("placedAt",now),("browserId",browser),("snapshot",snapshot));
  stockBefore=Convert.ToInt32(await store.Transaction(tx=>tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_01'")));
  await store.Transaction(async tx=>{await tx.Save(new("Cart",cartId,browser,1,CommerceWire.Obj(("id",cartId),("mode","mock"),("items",new JsonArray(CommerceWire.Item("sku_demo_01",1,500,1))),("promotionCode",null))),true);await tx.Save(new("Order",orderId,browser,1,order),true);await tx.Save(new("DemoReceipt",receiptId,browser,1,CommerceWire.Obj(("id",receiptId),("orderId",orderId),("collectionId",collectionId),("kind","Receipt"),("mode","mock"),("state","Issued"),("amountTwd",560),("currency","TWD"),("taxEffect","none"),("displayNotice","DEMO only; no tax effect"),("createdAt",now))),true);await tx.Save(new("DemoReceipt",retryReceiptId,browser,1,CommerceWire.Obj(("id",retryReceiptId),("orderId",orderId),("collectionId",collectionId),("kind","Receipt"),("mode","mock"),("state","Processing"),("amountTwd",560),("currency","TWD"),("taxEffect","none"),("displayNotice","DEMO only; no tax effect"),("createdAt",now))),true);await tx.Job("DemoReceipt",retryReceiptId,actor,0,null,"Order",orderId,CommerceWire.Obj(("outcome","declined")),now);return true;});
  await using(var c=new SqlConnection(admin.ConnectionString)){await c.OpenAsync();await using var seedTx=await c.BeginTransactionAsync();await using(var seed=new SqlCommand("INSERT m0commerce.Collections(Dataset,Id,OrderId,Kind,Collected,ShippingPaid) VALUES('demo_m0',@c,@o,'Primary',560,60);INSERT m0commerce.RefundUnits(Dataset,UnitId,OrderId,LineId,CollectionId,Ordinal,Paid) VALUES('demo_m0',@unit,@o,@l,@c,0,500);INSERT m0commerce.GuestCapabilities(Dataset,Id,OrderId,BrowserId,Intent,ExpiresAt,Consumed) VALUES('demo_m0',@read,@o,@b,'read',DATEADD(hour,1,SYSDATETIMEOFFSET()),0),('demo_m0',@ret,@o,@b,'return',DATEADD(hour,1,SYSDATETIMEOFFSET()),0);INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion) VALUES(@user,@email,REPLICATE(N'x',100),1,'Active',1)",c,(SqlTransaction)seedTx)){seed.Parameters.AddWithValue("@c",collectionId);seed.Parameters.AddWithValue("@o",orderId);seed.Parameters.AddWithValue("@unit",unitId);seed.Parameters.AddWithValue("@l",lineId);seed.Parameters.AddWithValue("@read",readCapability);seed.Parameters.AddWithValue("@ret",returnCapability);seed.Parameters.AddWithValue("@b",browser);seed.Parameters.AddWithValue("@email",actor+"@example.test");seed.Parameters.AddWithValue("@user",actor);await seed.ExecuteNonQueryAsync();}foreach(var permission in new[]{"shipment.manage","support.respond","return.inspect","refund.execute","refund.check","reconciliation.read","orders.read"}){await using var grant=new SqlCommand("INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason) VALUES(@id,@u,'Owner',@p,'Dataset','demo_m0',DATEADD(minute,-1,SYSDATETIMEOFFSET()),DATEADD(hour,1,SYSDATETIMEOFFSET()),'fixture','dedicated Commerce SQL journey')",c,(SqlTransaction)seedTx);grant.Parameters.AddWithValue("@id",M0Http.Id("grant"));grant.Parameters.AddWithValue("@u",actor);grant.Parameters.AddWithValue("@p",permission);await grant.ExecuteNonQueryAsync();}await seedTx.CommitAsync();}
  await using(var grantConnection=new SqlConnection(admin.ConnectionString)){await grantConnection.OpenAsync();await using var grant=new SqlCommand("INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason) VALUES(@id,@u,'Owner','demo.control','Dataset','demo_m0',DATEADD(minute,-1,SYSDATETIMEOFFSET()),DATEADD(hour,1,SYSDATETIMEOFFSET()),'fixture','dedicated Commerce SQL journey retry')",grantConnection);grant.Parameters.AddWithValue("@id",demoControlGrant);grant.Parameters.AddWithValue("@u",actor);await grant.ExecuteNonQueryAsync();}
  var cartVersion=CommerceWire.Version("Cart",cartId,1);
  var homeQuoteRequest=CommerceWire.Obj(("cartVersion",cartVersion),("deliveryMethod","mock_home"),("region","demo_supported"));
  var homeQuote=Body(await service.Quote(Request(homeQuoteRequest,cartVersion,M0Http.Id("key"))));
  var pickupQuoteRequest=CommerceWire.Obj(("cartVersion",cartVersion),("deliveryMethod","mock_pickup"),("region","demo_supported"));
  var pickupQuote=Body(await service.Quote(Request(pickupQuoteRequest,cartVersion,M0Http.Id("key"))));
  Check(homeQuote["snapshot"]!.AsObject().Number("shippingTwd")==60&&homeQuote["snapshot"]!.AsObject().Text("feeVersion")=="fee_demo_v1"&&pickupQuote["snapshot"]!.AsObject().Number("shippingTwd")==0,"Quote persists the resolved fee and version for home and pickup methods");
  try{await service.Quote(Request(CommerceWire.Obj(("cartVersion",cartVersion),("deliveryMethod","mock_home"),("region","demo_unsupported")),cartVersion,M0Http.Id("key")));throw new InvalidOperationException("FAILED unsupported quote region");}catch(M0Exception e){Check(e.Status==409&&e.Code=="FEE_UNAVAILABLE","Quote rejects an unsupported delivery region without a fee fallback");}
  var storedHomeQuote=await store.Transaction(tx=>tx.Get("Quote",homeQuote.Text("id")));Check(storedHomeQuote.Body["snapshot"]!.AsObject().Number("shippingTwd")==60&&storedHomeQuote.Body["snapshot"]!.AsObject().Text("feeVersion")=="fee_demo_v1","a later pickup Quote does not overwrite the earlier home fee snapshot");
  var orderVersion=CommerceWire.Version("Order",orderId,1);var created=Body(await service.CreateShipment(Request(CommerceWire.Obj(("expectedVersion",orderVersion)),orderVersion,M0Http.Id("key")),orderId));var shipmentId=created.Text("id");Check(created.Text("state")=="Pending"&&created.Text("mode")=="mock"&&created.Text("displayNotice").Contains("沒有真實包裹"),"paid order creates a pending DEMO shipment without real carrier tracking");
  var failureJob=Convert.ToString(await store.Transaction(tx=>tx.Scalar("SELECT Id FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='DemoReceipt' AND TargetId=@id",("@id",retryReceiptId))))??throw new InvalidOperationException("DEMO_RECEIPT_JOB_NOT_FOUND");Check(await worker.DispatchSpecificOnce(failureJob)==1,"mock worker processes a synthetic DEMO receipt failure");var failedReceipt=await store.Transaction(tx=>tx.Get("DemoReceipt",retryReceiptId));Check(failedReceipt.Body.Text("state")=="Failed","receipt failure remains visible independently from payment/refund state");
  var failureView=Body(await service.Receipts(Request(null,null,M0Http.Id("key")),orderId));Check(failureView.Text("taxEffect")=="none"&&failureView["receipts"]!.AsArray().Any(x=>x!.AsObject().Text("id")==retryReceiptId&&x.AsObject().Text("state")=="Failed"&&x.AsObject().Number("amountTwd")==560),"customer receipt query exposes full-amount DEMO failure without tax effect");
  var failedReceiptVersion=CommerceWire.Version("DemoReceipt",retryReceiptId,failedReceipt.Version);
  var receiptRetryRequest=CommerceWire.Obj(("targetKind","DemoReceipt"),("targetId",retryReceiptId),("scenario","success"),("expectedVersion",failedReceiptVersion));
  var receiptRetry=Body(await service.Scenario(Request(receiptRetryRequest,failedReceiptVersion,M0Http.Id("key"))));
  var receiptRetryJob=Convert.ToString(await store.Transaction(tx=>tx.Scalar("SELECT Id FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='Scenario' AND TargetId=@id",("@id",receiptRetry.Text("id")))))??throw new InvalidOperationException("DEMO_RECEIPT_RETRY_JOB_NOT_FOUND");
  var retryQueueState=await store.Transaction(async tx=>((string)(await tx.Scalar("SELECT State FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Id=@id",("@id",receiptRetryJob)))!,(string)(await tx.Scalar("SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='DemoReceipt' AND Id=@id",("@id",retryReceiptId)))!));
  Check(receiptRetry.Text("state")=="Processing"&&retryQueueState==("NotStarted","Failed"),"enqueueDemoScenario returns a queued result without claiming the worker started or changing the target");
  Check(await worker.DispatchSpecificOnce(receiptRetryJob)==1,"authorized query retry resolves the same failed DEMO receipt");
  var issuedReceipt=await store.Transaction(tx=>tx.Get("DemoReceipt",retryReceiptId));
  var issuedReceiptVersion=CommerceWire.Version("DemoReceipt",retryReceiptId,issuedReceipt.Version);
  var staleReceiptRequest=CommerceWire.Obj(("targetKind","DemoReceipt"),("targetId",retryReceiptId),("scenario","declined"),("expectedVersion",issuedReceiptVersion));
  var staleReceipt=Body(await service.Scenario(Request(staleReceiptRequest,issuedReceiptVersion,M0Http.Id("key"))));
  var staleReceiptJob=Convert.ToString(await store.Transaction(tx=>tx.Scalar("SELECT Id FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='Scenario' AND TargetId=@id",("@id",staleReceipt.Text("id")))))??throw new InvalidOperationException("DEMO_RECEIPT_STALE_JOB_NOT_FOUND");
  Check(await worker.DispatchSpecificOnce(staleReceiptJob)==1&&((await store.Transaction(tx=>tx.Get("DemoReceipt",retryReceiptId))).Body.Text("state")=="Issued"),"late receipt failure cannot downgrade an already issued DEMO receipt");
  var issuedForRevocation=await store.Transaction(tx=>tx.Get("DemoReceipt",retryReceiptId));var issuedForRevocationVersion=CommerceWire.Version("DemoReceipt",retryReceiptId,issuedForRevocation.Version);
  var revokedScenario=Body(await service.Scenario(Request(CommerceWire.Obj(("targetKind","DemoReceipt"),("targetId",retryReceiptId),("scenario","declined"),("expectedVersion",issuedForRevocationVersion)),issuedForRevocationVersion,M0Http.Id("key"))));
  var revokedScenarioJob=Convert.ToString(await store.Transaction(tx=>tx.Scalar("SELECT TOP(1) Id FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='Scenario' AND TargetId=@id ORDER BY CreatedAt DESC",("@id",revokedScenario.Text("id")))))??throw new InvalidOperationException("DEMO_REVOKED_SCENARIO_JOB_NOT_FOUND");
  var beforeRevokedDispatch=await store.Transaction(async tx=>((string)(await tx.Scalar("SELECT State FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Id=@id",("@id",revokedScenarioJob)))!,(string)(await tx.Scalar("SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='DemoReceipt' AND Id=@id",("@id",retryReceiptId)))!));
  Check(revokedScenario.Text("state")=="Processing"&&beforeRevokedDispatch==("NotStarted","Issued"),"a second demo scenario remains queued while its already-issued receipt stays unchanged");
  await using(var revoke=new SqlConnection(admin.ConnectionString)){await revoke.OpenAsync();await using var command=new SqlCommand("UPDATE m0.Grants SET ValidUntil=DATEADD(second,-1,SYSDATETIMEOFFSET()) WHERE GrantId=@id AND Permission='demo.control'",revoke);command.Parameters.AddWithValue("@id",demoControlGrant);Check(await command.ExecuteNonQueryAsync()==1,"fixture revokes demo.control after scenario enqueue");}
  var revokedDispatchCount=await worker.DispatchSpecificOnce(revokedScenarioJob);var afterRevokedDispatch=await store.Transaction(async tx=>((string)(await tx.Scalar("SELECT State FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Id=@id",("@id",revokedScenarioJob)))!,(string)(await tx.Scalar("SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='DemoReceipt' AND Id=@id",("@id",retryReceiptId)))!));
  Check(revokedDispatchCount==0&&afterRevokedDispatch==("Blocked","Issued"),"worker rechecks current permission and blocks an unstarted scenario after grant revocation");
  await using(var restoreGrant=new SqlConnection(admin.ConnectionString)){await restoreGrant.OpenAsync();await using var command=new SqlCommand("UPDATE m0.Grants SET ValidUntil=DATEADD(hour,1,SYSDATETIMEOFFSET()) WHERE GrantId=@id AND Permission='demo.control'",restoreGrant);command.Parameters.AddWithValue("@id",demoControlGrant);Check(await command.ExecuteNonQueryAsync()==1,"fixture restores demo.control for subsequent authorized acceptance cases");}
  async Task<JsonObject> MoveShipment(string state,string expected,string eventId,string key,string? reason=null){var body=CommerceWire.Obj(("state",state),("eventId",eventId),("expectedVersion",expected));if(reason is not null)body["reason"]=reason;return Body(await service.TransitionShipment(Request(body,expected,key),shipmentId));}
  var shipmentEventId=M0Http.Id("event");var shipped=await MoveShipment("Shipped",created.Text("version"),shipmentEventId,M0Http.Id("key"));var duplicate=await MoveShipment("Shipped",created.Text("version"),shipmentEventId,M0Http.Id("key"));
  var inTransit=await MoveShipment("InTransit",shipped.Text("version"),M0Http.Id("event"),M0Http.Id("key"));var exception=await MoveShipment("Exception",inTransit.Text("version"),M0Http.Id("event"),M0Http.Id("key"),"synthetic carrier issue");
  try{await MoveShipment("Shipped",created.Text("version"),M0Http.Id("event"),M0Http.Id("key"));throw new InvalidOperationException("FAILED out-of-order shipment event");}catch(M0Exception e){Check(e.Status==412&&e.Code=="VERSION_CHANGED","out-of-order shipment transition cannot move the current version backward");}
  var recovered=await MoveShipment("InTransit",exception.Text("version"),M0Http.Id("event"),M0Http.Id("key"));var delivered=await MoveShipment("Delivered",recovered.Text("version"),M0Http.Id("event"),M0Http.Id("key"));Check(duplicate.Text("version")==shipped.Text("version")&&delivered.Text("state")=="Delivered"&&delivered["history"]!.AsArray().Count==6,"shipment replay, out-of-order rejection, and exception recovery preserve one ordered history");
  var customerOrder=Body(await service.Order(Request(null,null,M0Http.Id("key")),orderId));Check(customerOrder.Text("id")==orderId&&customerOrder["shipmentSummary"]!.AsObject().Count==1&&customerOrder["shipmentSummary"]!["state"]!.GetValue<string>()=="Delivered","guest order capability reads its own order using the public shipment summary contract");
  var requested=Body(await service.RequestReturn(Request(CommerceWire.Obj(("orderLineId",lineId),("quantity",1),("reason","synthetic return test")),null,M0Http.Id("key")),orderId));var returnId=requested.Text("id");Check(requested.Text("state")=="Requested","delivered guest order accepts an eligible return request");
  var orderAfterReturn=Body(await service.Order(Request(null,null,M0Http.Id("key")),orderId));var publicReturn=orderAfterReturn["returns"]!.AsArray().Single()!.AsObject();var publicReturnLine=publicReturn["lines"]!.AsArray().Single()!.AsObject();Check(publicReturn.ContainsKey("lines")&&!publicReturn.ContainsKey("orderLineId")&&publicReturnLine.Text("orderLineId")==lineId&&publicReturnLine.Number("requestedQuantity")==1,"order return summary follows the adopted CommerceReturnSummary contract");
  async Task<JsonObject> TransitionReturn(string state,string expected,string key)=>Body(await service.TransitionReturn(Request(CommerceWire.Obj(("state",state),("expectedVersion",expected)),expected,key),returnId));
  var accepted=await TransitionReturn("Accepted",requested.Text("version"),M0Http.Id("key"));var received=await TransitionReturn("Received",accepted.Text("version"),M0Http.Id("key"));var inspected=Body(await service.InspectReturn(Request(CommerceWire.Obj(("resellableQuantity",1),("reason","synthetic inspection"),("expectedVersion",received.Text("version"))),received.Text("version"),M0Http.Id("key")),returnId));
  var refundCaseId=inspected["inspection"]!.AsObject().Text("refundCaseId");var refundCaseVersion=inspected["inspection"]!.AsObject().Text("refundCaseVersion");Check(inspected.Text("state")=="Inspected"&&inspected["inspection"]!["refundUnits"]![0]!["paidTwd"]!.GetValue<long>()==500&&Convert.ToInt32(await store.Transaction(tx=>tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_01'")))==stockBefore+1,"resellable receipt creates one inventory movement and refund case using the original unit snapshot");
  var refund=Body(await service.Refund(Request(CommerceWire.Obj(("lines",new[]{CommerceWire.Obj(("orderLineId",lineId),("quantity",1))}),("refundShipping",false),("amountTwd",500),("reason","synthetic refund test"),("expectedVersion",refundCaseVersion)),refundCaseVersion,M0Http.Id("key")),refundCaseId,"Merchandise"));var refundId=refund.Text("id");var pending=Convert.ToInt64(await store.Transaction(tx=>tx.Scalar("SELECT Pending FROM m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@id",("@id",collectionId))));Check(refund.Number("amountTwd")==500&&pending==500,"refund reserves the snapshot-derived amount atomically in the collection ledger");
  var refundJob=Convert.ToString(await store.Transaction(tx=>tx.Scalar("SELECT Id FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='Refund' AND TargetId=@id",("@id",refundId))))??throw new InvalidOperationException("REFUND_JOB_NOT_FOUND");Check(await worker.DispatchSpecificOnce(refundJob)==1,"mock worker claims the authorized refund job by id");var unknown=await store.Transaction(async tx=>((string)(await tx.Scalar("SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND Id=@id",("@id",refundId)))!,(long)(await tx.Scalar("SELECT Pending FROM m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@id",("@id",collectionId)))!,(long)(await tx.Scalar("SELECT Refunded FROM m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@id",("@id",collectionId)))!));Check(unknown==("Unknown",500L,0L),"unknown mock refund retains the full collection reservation without claiming success");await worker.RecordTrustedResult("RefundOperation",refundId,"success");var settled=await store.Transaction(async tx=>((long)(await tx.Scalar("SELECT Pending FROM m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@id",("@id",collectionId)))!,(long)(await tx.Scalar("SELECT Refunded FROM m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@id",("@id",collectionId)))!));Check(settled==(0L,500L),"trusted query of the original unknown refund settles the reserved amount exactly once");
  var adjustmentId="DEMO-ADJ-"+refundId;var adjustmentJob=Convert.ToString(await store.Transaction(tx=>tx.Scalar("SELECT Id FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='ReceiptAdjustment' AND TargetId=@id",("@id",adjustmentId))))??throw new InvalidOperationException("RECEIPT_ADJUSTMENT_JOB_NOT_FOUND");await store.Transaction(async tx=>{await tx.Execute("UPDATE m0commerce.Jobs SET Payload=N'{\"outcome\":\"unknown\"}' WHERE Dataset='demo_m0' AND Id=@id AND State='NotStarted'",("@id",adjustmentJob));return true;});Check(await worker.DispatchSpecificOnce(adjustmentJob)==1,"mock worker processes the targeted DEMO receipt adjustment job");var unknownAdjustment=await store.Transaction(async tx=>((string)(await tx.Scalar("SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='ReceiptAdjustment' AND Id=@id",("@id",adjustmentId)))!,(string)(await tx.Scalar("SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND Id=@id",("@id",refundId)))!,(long)(await tx.Scalar("SELECT Refunded FROM m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@id",("@id",collectionId)))!));Check(unknownAdjustment==("Unknown","Succeeded",500L),"unknown DEMO adjustment remains independent from the successful refund ledger");
  var adjustment=await store.Transaction(tx=>tx.Get("ReceiptAdjustment",adjustmentId));var adjustmentVersion=CommerceWire.Version("ReceiptAdjustment",adjustmentId,adjustment.Version);var retry=Body(await service.Scenario(Request(CommerceWire.Obj(("targetKind","ReceiptAdjustment"),("targetId",adjustmentId),("scenario","success"),("expectedVersion",adjustmentVersion)),adjustmentVersion,M0Http.Id("key"))));var retryJobResult=await store.Transaction(tx=>tx.Scalar("SELECT Id FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='Scenario' AND TargetId=@id",("@id",retry.Text("id"))));var retryJob=Convert.ToString(retryJobResult)??throw new InvalidOperationException("RECEIPT_RETRY_JOB_NOT_FOUND");Check(await worker.DispatchSpecificOnce(retryJob)==1,"authorized mock scenario retries the same unknown DEMO adjustment");var receipts=Body(await service.Receipts(Request(null,null,M0Http.Id("key")),orderId));Check(receipts.Text("taxEffect")=="none"&&receipts["adjustments"]!.AsArray().Single()!["amountTwd"]!.GetValue<long>()==500&&receipts["adjustments"]!.AsArray().Single()!["state"]!.GetValue<string>()=="Adjusted","retried receipt adjustment settles once without tax effect");var refundState=Convert.ToString(await store.Transaction(tx=>tx.Scalar("SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND Id=@id",("@id",refundId))));Check(refundState=="Succeeded"&&Convert.ToInt32(await store.Transaction(tx=>tx.Scalar("SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='ReceiptAdjustment' AND JSON_VALUE(Body,'$.refundOperationId')=@id",("@id",refundId))))==1,"retrying a DEMO receipt adjustment does not create or re-execute a refund");
 }finally{await using var c=new SqlConnection(admin.ConnectionString);await c.OpenAsync();await using var cleanup=new SqlCommand(@"DELETE m0commerce.Jobs WHERE Dataset='demo_m0' AND Actor IN(@u,@b);DELETE m0commerce.Inbox WHERE Dataset='demo_m0' AND SourceId IN(SELECT Id+'_terminal' FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND JSON_VALUE(Body,'$.orderId')=@o);DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor IN(@u,@b);DELETE m0commerce.GuestCapabilities WHERE Dataset='demo_m0' AND Id IN(@read,@ret);DELETE m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId=@o;DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o;DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND (JSON_VALUE(Body,'$.orderId')=@o OR (Kind='Quote' AND JSON_VALUE(Body,'$.cartId')=@cart) OR (Kind='Cart' AND Id=@cart));DELETE m0.Grants WHERE UserId=@u;DELETE m0.Users WHERE UserId=@u;IF @stock IS NOT NULL UPDATE m0commerce.Inventory SET Stock=@stock WHERE Dataset='demo_m0' AND Sku='sku_demo_01'",c);cleanup.Parameters.AddWithValue("@u",actor);cleanup.Parameters.AddWithValue("@b",browser);cleanup.Parameters.AddWithValue("@read",readCapability);cleanup.Parameters.AddWithValue("@ret",returnCapability);cleanup.Parameters.AddWithValue("@o",orderId);cleanup.Parameters.AddWithValue("@cart",cartId);cleanup.Parameters.AddWithValue("@stock",(object?)stockBefore??DBNull.Value);await cleanup.ExecuteNonQueryAsync();}
}
try {
await using(var lockConnection=new SqlConnection(admin.ConnectionString)){await lockConnection.OpenAsync();await using var lockTx=await lockConnection.BeginTransactionAsync();await using(var acquire=new SqlCommand("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource='kohami.m0.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000;IF @r<0 THROW 51000,'LOCK_TEST_FAILED',1",lockConnection,(SqlTransaction)lockTx))await acquire.ExecuteNonQueryAsync();var resolver=new CommerceResourceResolver(new M0Sql(settings));var resolved=await resolver.ResolveScopesAsync("RefundCase","refund_case_demo").WaitAsync(TimeSpan.FromSeconds(3));Check(resolved.Any(x=>x==new CommerceScope("Order","order_refund_demo")),"scope resolution reads under existing exclusive security lock without self-wait");await lockTx.RollbackAsync();}
await using(var clearOldWorkerFixture=new SqlConnection(admin.ConnectionString)){await clearOldWorkerFixture.OpenAsync();await using var clear=new SqlCommand("DELETE m0commerce.Inbox WHERE SourceId IN(SELECT Id+'_terminal' FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND Id LIKE 'maintenance_started_%');DELETE m0commerce.Jobs WHERE Dataset='demo_m0' AND Actor='fixture' AND Id LIKE 'job_maintenance_%';DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND Id LIKE 'maintenance_started_%';",clearOldWorkerFixture);await clear.ExecuteNonQueryAsync();}
await SetMaintenance(true);var startedId="maintenance_started_"+Guid.NewGuid().ToString("N");var pendingId="maintenance_pending_"+Guid.NewGuid().ToString("N");var startedJob="job_maintenance_"+Guid.NewGuid().ToString("N");var pendingJob="job_maintenance_"+Guid.NewGuid().ToString("N");
await using(var seedJobs=new SqlConnection(admin.ConnectionString)){await seedJobs.OpenAsync();await using var seed=new SqlCommand("INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES('demo_m0','RefundOperation',@started,'fixture',1,N'{\"state\":\"Succeeded\",\"dispatchState\":\"Started\",\"orderId\":\"order_maintenance_fixture\",\"amountTwd\":1,\"currency\":\"TWD\"}');INSERT m0commerce.Jobs(Dataset,Id,Kind,TargetId,Actor,SecurityVersion,Permission,ResourceKind,ResourceId,State,Payload,CreatedAt) VALUES('demo_m0',@startedJob,'Refund',@started,'fixture',1,'refund.execute','Case','fixture','Started',N'{\"outcome\":\"success\"}',SYSUTCDATETIME()),('demo_m0',@pendingJob,'Scenario',@pending,'fixture',1,'demo.control','Dataset','demo_m0','NotStarted',N'{\"targetKind\":\"PaymentAttempt\",\"targetId\":\"missing\",\"scenario\":\"success\"}',SYSUTCDATETIME())",seedJobs);seed.Parameters.AddWithValue("@started",startedId);seed.Parameters.AddWithValue("@startedJob",startedJob);seed.Parameters.AddWithValue("@pendingJob",pendingJob);seed.Parameters.AddWithValue("@pending",pendingId);await seed.ExecuteNonQueryAsync();}
Check(await worker.DispatchOnce()==1,"maintenance resumes already-started worker job only");
await using(var checkJobs=new SqlConnection(admin.ConnectionString)){await checkJobs.OpenAsync();await using var check=new SqlCommand("SELECT (SELECT State FROM m0commerce.Jobs WHERE Id=@startedJob),(SELECT State FROM m0commerce.Jobs WHERE Id=@pendingJob),(SELECT Applied FROM m0commerce.Inbox WHERE SourceId=@source)",checkJobs);check.Parameters.AddWithValue("@startedJob",startedJob);check.Parameters.AddWithValue("@pendingJob",pendingJob);check.Parameters.AddWithValue("@source",startedJob);await using var result=await check.ExecuteReaderAsync();await result.ReadAsync();Check(result.GetString(0)=="Completed"&&result.GetString(1)=="NotStarted"&&result.GetBoolean(2),"maintenance tracks started job completion without dispatching new work");}
await using(var cleanupJobs=new SqlConnection(admin.ConnectionString)){await cleanupJobs.OpenAsync();await using var cleanup=new SqlCommand("DELETE m0commerce.Inbox WHERE SourceId=@source;DELETE m0commerce.Jobs WHERE Id IN(@startedJob,@pendingJob);DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND Id=@started",cleanupJobs);cleanup.Parameters.AddWithValue("@source",startedJob);cleanup.Parameters.AddWithValue("@startedJob",startedJob);cleanup.Parameters.AddWithValue("@pendingJob",pendingJob);cleanup.Parameters.AddWithValue("@started",startedId);await cleanup.ExecuteNonQueryAsync();}await SetMaintenance(false);
// Initial fixture has a single last item. Serialized conditional decrements must produce exactly one committed order.
var prefix=M0Http.Id("race");var results=await Task.WhenAll(Enumerable.Range(0,20).Select(async i=>{try{return await store.Transaction(async tx=>{var count=await tx.Execute("UPDATE m0commerce.Inventory SET Stock=Stock-1 WHERE Dataset='demo_m0' AND Sku='sku_demo_01' AND Stock>=1");if(count!=1)throw new M0Exception(409,"STOCK_CHANGED");await tx.Execute("INSERT m0commerce.Reservations VALUES('demo_m0',@o,'sku_demo_01',1,'Held',@e)",("@o",prefix+i),("@e",DateTimeOffset.UtcNow.AddMinutes(15)));return true;});}catch(M0Exception){return false;}}));
Check(results.Count(x=>x)==1,"SQL twenty requests last item one commit");
var raceSnapshot=await store.Transaction(async tx=>{
 var stock=Convert.ToInt64(await tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_01'"));
 var held=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId LIKE @prefix+'%' AND Sku='sku_demo_01' AND Quantity=1 AND State='Held'",("@prefix",prefix)));
 return(stock,held);
});
Check(raceSnapshot==(0,1),"SQL last-item stock and held-reservation snapshot matches the single winner");
// A later SKU shortage must roll back an earlier SKU decrement in the same transaction.
var before=await store.Transaction(tx=>tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_02'"));try{await store.Transaction<bool>(async tx=>{await tx.Execute("UPDATE m0commerce.Inventory SET Stock=Stock-1 WHERE Dataset='demo_m0' AND Sku='sku_demo_02' AND Stock>=1");var later=await tx.Execute("UPDATE m0commerce.Inventory SET Stock=Stock-1 WHERE Dataset='demo_m0' AND Sku='sku_demo_01' AND Stock>=1");if(later!=1)throw new M0Exception(409,"STOCK_CHANGED");return true;});}catch(M0Exception){}
var after=await store.Transaction(tx=>tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_02'"));var lastStock=await store.Transaction(tx=>tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_01'"));Check(Equals(before,after)&&Convert.ToInt64(lastStock)==0,"SQL multi SKU rollback");

// Idempotency is persisted with the operation in the dedicated SQL fixture. A retry
// returns the original response; reusing its key for changed input is rejected.
var retryActor=M0Http.Id("fixture");var retryScope="createCheckout/fixture";var retryKey=M0Http.Id("retry");
var retryInput=CommerceWire.Obj(("quoteId",M0Http.Id("quote")),("confirmed",true));var retryHash=CommerceWire.RequestHash(retryInput);
var retryResponse=CommerceWire.Obj(("checkoutId",M0Http.Id("checkout")));
try {
await store.Transaction(async tx=>{Check(await tx.Replay(retryActor,retryScope,retryKey,retryHash) is null,"idempotency key starts unused");await tx.Record(retryActor,retryScope,retryKey,retryHash,new(retryResponse,201,1));return true;});
var replay=await store.Transaction(tx=>tx.Replay(retryActor,retryScope,retryKey,retryHash));
Check(replay is not null&&replay.Status==201&&replay.Body.Text("checkoutId")==retryResponse.Text("checkoutId"),"same checkout request replays original response");
try{await store.Transaction(tx=>tx.Replay(retryActor,retryScope,retryKey,CommerceWire.RequestHash(CommerceWire.Obj(("quoteId",M0Http.Id("different")),("confirmed",true)))));throw new InvalidOperationException("FAILED: changed request reused idempotency key");}catch(M0Exception e) when(e.Status==409&&e.Code=="KEY_CONFLICT"){Check(true,"changed checkout request with same key is rejected");}
} finally {
 await using var cleanupRetry=new SqlConnection(admin.ConnectionString);await cleanupRetry.OpenAsync();await using var removeRetry=new SqlCommand("DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@actor AND Scope=@scope AND OperationKey=@key",cleanupRetry);removeRetry.Parameters.AddWithValue("@actor",retryActor);removeRetry.Parameters.AddWithValue("@scope",retryScope);removeRetry.Parameters.AddWithValue("@key",retryKey);await removeRetry.ExecuteNonQueryAsync();
}

// Unknown payment after reservation expiry must not create a collection. A later
// trusted success creates one compensation collection without reviving the order;
// repeating that source result has no second inventory or money effect.
var lateAttemptId=M0Http.Id("pay");var lateCheckoutId=M0Http.Id("checkout");var lateOrderId=M0Http.Id("order");var lateOwner=M0Http.Id("fixture");
var mismatchEventId="evt_mismatch_"+lateAttemptId;var delayedDeclineEventId="evt_old_decline_"+lateAttemptId;
var lateExpires=DateTimeOffset.UtcNow.AddMinutes(-1);var lateSnapshot=CommerceWire.Snapshot(new JsonArray(CommerceWire.Item("sku_demo_03",1,650,10)),60,true);
var lateLine=lateSnapshot["lines"]!.AsArray()[0]!.AsObject();lateLine["orderLineId"]=M0Http.Id("line");lateLine["units"]!.AsArray()[0]!.AsObject()["unitId"]=M0Http.Id("unit");
var lateStockBefore=Convert.ToInt64(await store.Transaction(tx=>tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_03'")));
try {
var lateCheckout=CommerceWire.Obj(("checkoutId",lateCheckoutId),("orderId",lateOrderId),("mode","mock"),("expiresAt",lateExpires),("snapshot",lateSnapshot),("orderState","AwaitingPayment"),("paymentState","Processing"),("compensationRequired",false));
var lateAttempt=CommerceWire.Obj(("id",lateAttemptId),("checkoutId",lateCheckoutId),("mode","mock"),("state","Processing"),("amountTwd",lateSnapshot.Number("totalTwd")));
await store.Transaction(async tx=>{
 var reserved=await tx.Execute("UPDATE m0commerce.Inventory SET Stock=Stock-1 WHERE Dataset='demo_m0' AND Sku='sku_demo_03' AND Stock>=1");Check(reserved==1,"late-payment fixture reserves one synthetic unit");
 await tx.Execute("INSERT m0commerce.Reservations VALUES('demo_m0',@o,'sku_demo_03',1,'Held',@e)",("@o",lateOrderId),("@e",lateExpires));
 await tx.Save(new("Checkout",lateCheckoutId,lateOwner,1,lateCheckout),true);
 await tx.Save(new("Order",lateOrderId,lateOwner,1,(JsonObject)lateCheckout.DeepClone()),true);
 await tx.Save(new("PaymentAttempt",lateAttemptId,lateOwner,1,lateAttempt),true);return true;
});
await worker.RecordTrustedResult("PaymentAttempt",lateAttemptId,"unknown");
var unknownState=await store.Transaction(async tx=>{var checkout=await tx.Get("Checkout",lateCheckoutId);var count=await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o",("@o",lateOrderId));return(checkout.Body.Text("orderState"),checkout.Body.Text("paymentState"),Convert.ToInt32(count));});
Check(unknownState==("Expired","Unknown",0),"expired unknown payment releases stock without collection or order revival");
await worker.RecordTrustedResult("PaymentAttempt",lateAttemptId,"late_success");
var lateState=await store.Transaction(async tx=>{
 var checkout=await tx.Get("Checkout",lateCheckoutId);var order=await tx.Get("Order",lateOrderId);var attempt=await tx.Get("PaymentAttempt",lateAttemptId);
 var reservation=(string)(await tx.Scalar("SELECT State FROM m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@o",("@o",lateOrderId)))!;
 var stock=Convert.ToInt64(await tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_03'"));
 var collections=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o",("@o",lateOrderId)));
 var compensation=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundCase' AND Id=@id",("@id","comp_"+lateAttemptId)));
 return(checkout.Body.Text("orderState"),checkout.Body.Text("paymentState"),checkout.Body["compensationRequired"]!.GetValue<bool>(),order.Body.Text("orderState"),attempt.Body.Text("state"),reservation,stock,collections,compensation);
});
Check(lateState==("Expired","Succeeded",true,"Expired","Succeeded","Released",lateStockBefore,1,1),"late trusted success creates one compensation without restoring expired order or stock");
var lateAttemptDocument=await store.Transaction(tx=>tx.Get("PaymentAttempt",lateAttemptId));
var lateAmount=lateAttemptDocument.Body.Number("amountTwd");
var mismatchEvent=new MockMoneyEvent(mismatchEventId,"mock","demo_m0",lateOrderId,lateAttemptId,lateAmount+1,"TWD",1,"success");
await worker.RecordMockMoneyEvent(mismatchEvent);
var mismatchState=await store.Transaction(async tx=>((string)(await tx.Scalar("SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='PaymentAttempt' AND Id=@id",("@id",lateAttemptId)))!,Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o",("@o",lateOrderId)))));
await using(var inspectMismatch=new SqlConnection(admin.ConnectionString)){await inspectMismatch.OpenAsync();await using var query=new SqlCommand("SELECT COUNT(*) FROM m0commerce.Inbox WHERE Dataset='demo_m0' AND EventId=@id AND Applied=0 AND JSON_VALUE(Body,'$.validationState')='Quarantined' AND JSON_VALUE(Body,'$.quarantineReason')='AMOUNT_MISMATCH' AND JSON_VALUE(Body,'$.mode')='mock' AND JSON_VALUE(Body,'$.dataset')='demo_m0' AND JSON_VALUE(Body,'$.orderId')=@order AND JSON_VALUE(Body,'$.operationId')=@op AND JSON_VALUE(Body,'$.currency')='TWD' AND JSON_VALUE(Body,'$.contractVersion')='1'",inspectMismatch);query.Parameters.AddWithValue("@id",mismatchEvent.EventId);query.Parameters.AddWithValue("@order",lateOrderId);query.Parameters.AddWithValue("@op",lateAttemptId);Check(mismatchState==("Succeeded",1)&&Convert.ToInt32(await query.ExecuteScalarAsync())==1,"amount mismatch is quarantined with complete mock event identity and does not alter the ledger");}
var delayedDecline=new MockMoneyEvent(delayedDeclineEventId,"mock","demo_m0",lateOrderId,lateAttemptId,lateAmount,"TWD",1,"declined");
await worker.RecordMockMoneyEvent(delayedDecline);
await worker.RecordMockMoneyEvent(delayedDecline);
var replayState=await store.Transaction(async tx=>((string)(await tx.Scalar("SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='PaymentAttempt' AND Id=@id",("@id",lateAttemptId)))!,Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o",("@o",lateOrderId)))));
await using(var inspectReplay=new SqlConnection(admin.ConnectionString)){await inspectReplay.OpenAsync();await using var query=new SqlCommand("SELECT COUNT(*) FROM m0commerce.Inbox WHERE Dataset='demo_m0' AND EventId=@id AND Applied=1",inspectReplay);query.Parameters.AddWithValue("@id",delayedDecline.EventId);Check(replayState==("Succeeded",1)&&Convert.ToInt32(await query.ExecuteScalarAsync())==1,"out-of-order decline and duplicate provider event cannot downgrade or duplicate a successful payment");}
try{await worker.RecordMockMoneyEvent(new MockMoneyEvent(delayedDecline.EventId,"mock","demo_m0",lateOrderId,lateAttemptId,lateAmount,"TWD",1,"success"));throw new InvalidOperationException("FAILED: reused provider event ID with different content must conflict");}catch(M0Exception e) when(e.Status==409&&e.Code=="KEY_CONFLICT"){Check(true,"same provider event ID with a different payload hash is rejected");}
await using(var inspectCollision=new SqlConnection(admin.ConnectionString)){await inspectCollision.OpenAsync();await using var query=new SqlCommand(@"SELECT
 (SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='PaymentAttempt' AND Id=@attempt),
 (SELECT COUNT(*) FROM m0commerce.Inbox WHERE Dataset='demo_m0' AND EventId=@event),
 (SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@order)",inspectCollision);query.Parameters.AddWithValue("@attempt",lateAttemptId);query.Parameters.AddWithValue("@event",delayedDecline.EventId);query.Parameters.AddWithValue("@order",lateOrderId);await using var result=await query.ExecuteReaderAsync();await result.ReadAsync();Check(result.GetString(0)=="Succeeded"&&result.GetInt32(1)==1&&result.GetInt32(2)==1,"event hash collision retains the original inbox record and exactly one successful collection");}
await worker.RecordTrustedResult("PaymentAttempt",lateAttemptId,"success");
var duplicateEffects=await store.Transaction(async tx=>{
 var collections=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o",("@o",lateOrderId)));
 var compensation=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundCase' AND Id=@id",("@id","comp_"+lateAttemptId)));
 var stock=Convert.ToInt64(await tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_03'"));return(collections,compensation,stock);
});
Check(duplicateEffects==(1,1,lateStockBefore),"duplicate trusted success has no additional collection, compensation or stock effect");
} finally {
await using(var cleanup=new SqlConnection(admin.ConnectionString)){
 await cleanup.OpenAsync();await using var remove=new SqlCommand(@"DELETE m0commerce.Inbox WHERE Dataset='demo_m0' AND SourceId IN(@unknown,@terminal);
 DELETE m0commerce.Inbox WHERE Dataset='demo_m0' AND EventId IN(@mismatch,@decline);
 DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@collection;
 DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind IN('RefundCase','PaymentAttempt','Order','Checkout') AND Id IN(@compensation,@attempt,@order,@checkout);
 DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@order;
 UPDATE m0commerce.Inventory SET Stock=@stock WHERE Dataset='demo_m0' AND Sku='sku_demo_03'",cleanup);
 remove.Parameters.AddWithValue("@unknown",lateAttemptId+"_unknown");remove.Parameters.AddWithValue("@terminal",lateAttemptId+"_terminal");remove.Parameters.AddWithValue("@mismatch",mismatchEventId);remove.Parameters.AddWithValue("@decline",delayedDeclineEventId);remove.Parameters.AddWithValue("@collection","collection_"+lateAttemptId);remove.Parameters.AddWithValue("@compensation","comp_"+lateAttemptId);remove.Parameters.AddWithValue("@attempt",lateAttemptId);remove.Parameters.AddWithValue("@order",lateOrderId);remove.Parameters.AddWithValue("@checkout",lateCheckoutId);remove.Parameters.AddWithValue("@stock",lateStockBefore);await remove.ExecuteNonQueryAsync();
}
}
// A trusted decline keeps a valid reservation available for a new attempt. A
// second successful attempt confirms once; another distinct success is recorded
// as an extra collection with a compensation case and no stock/order replay.
var retryCheckoutId=M0Http.Id("checkout");var retryOrderId=M0Http.Id("order");var retryOwner=M0Http.Id("fixture");
var failedAttemptId=M0Http.Id("pay");var successfulAttemptId=M0Http.Id("pay");var extraAttemptId=M0Http.Id("pay");
var retryExpires=DateTimeOffset.UtcNow.AddMinutes(15);var retrySnapshot=CommerceWire.Snapshot(new JsonArray(CommerceWire.Item("sku_demo_03",1,650,10)),60,true);
var retryLine=retrySnapshot["lines"]!.AsArray()[0]!.AsObject();retryLine["orderLineId"]=M0Http.Id("line");retryLine["units"]!.AsArray()[0]!.AsObject()["unitId"]=M0Http.Id("unit");
var retryStockBefore=Convert.ToInt64(await store.Transaction(tx=>tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_03'")));
JsonObject PaymentAttempt(string id)=>CommerceWire.Obj(("id",id),("checkoutId",retryCheckoutId),("mode","mock"),("state","Processing"),("amountTwd",retrySnapshot.Number("totalTwd")));
try {
 var retryCheckout=CommerceWire.Obj(("checkoutId",retryCheckoutId),("orderId",retryOrderId),("mode","mock"),("expiresAt",retryExpires),("snapshot",retrySnapshot),("orderState","AwaitingPayment"),("paymentState","Processing"),("compensationRequired",false));
 await store.Transaction(async tx=>{
  var reserved=await tx.Execute("UPDATE m0commerce.Inventory SET Stock=Stock-1 WHERE Dataset='demo_m0' AND Sku='sku_demo_03' AND Stock>=1");Check(reserved==1,"payment-retry fixture reserves one synthetic unit");
  await tx.Execute("INSERT m0commerce.Reservations VALUES('demo_m0',@o,'sku_demo_03',1,'Held',@e)",("@o",retryOrderId),("@e",retryExpires));
  await tx.Save(new("Checkout",retryCheckoutId,retryOwner,1,retryCheckout),true);
  await tx.Save(new("Order",retryOrderId,retryOwner,1,(JsonObject)retryCheckout.DeepClone()),true);
  await tx.Save(new("PaymentAttempt",failedAttemptId,retryOwner,1,PaymentAttempt(failedAttemptId)),true);return true;
 });
 await worker.RecordTrustedResult("PaymentAttempt",failedAttemptId,"declined");
 var failedState=await store.Transaction(async tx=>{
  var checkout=await tx.Get("Checkout",retryCheckoutId);var order=await tx.Get("Order",retryOrderId);var attempt=await tx.Get("PaymentAttempt",failedAttemptId);
  var reservation=(string)(await tx.Scalar("SELECT State FROM m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@o",("@o",retryOrderId)))!;
  var stock=Convert.ToInt64(await tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_03'"));
  var collections=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o",("@o",retryOrderId)));
  return(attempt.Body.Text("state"),checkout.Body.Text("orderState"),checkout.Body.Text("paymentState"),order.Body.Text("orderState"),reservation,stock,collections);
 });
 Check(failedState==("Failed","AwaitingPayment","Failed","AwaitingPayment","Held",retryStockBefore-1,0),"trusted decline keeps order pending and reservation held without collection");
 await store.Transaction(async tx=>{
  var checkout=await tx.Get("Checkout",retryCheckoutId);checkout.Body["paymentState"]="Processing";
  await CommerceLifecycle.SaveOrderAndCheckout(tx,checkout with {Version=checkout.Version+1});
  await tx.Save(new("PaymentAttempt",successfulAttemptId,retryOwner,1,PaymentAttempt(successfulAttemptId)),true);return true;
 });
 await worker.RecordTrustedResult("PaymentAttempt",successfulAttemptId,"success");
 var confirmed=await store.Transaction(async tx=>{
  var checkout=await tx.Get("Checkout",retryCheckoutId);var reservation=(string)(await tx.Scalar("SELECT State FROM m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@o",("@o",retryOrderId)))!;
  var primary=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o AND Kind='Primary'",("@o",retryOrderId)));
  var stock=Convert.ToInt64(await tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_03'"));return(checkout.Body.Text("orderState"),checkout.Body.Text("paymentState"),reservation,primary,stock);
 });
 Check(confirmed==("Confirmed","Succeeded","Consumed",1,retryStockBefore-1),"retry success consumes the held reservation and creates one primary collection");
 await store.Transaction(async tx=>{await tx.Save(new("PaymentAttempt",extraAttemptId,retryOwner,1,PaymentAttempt(extraAttemptId)),true);return true;});
 await worker.RecordTrustedResult("PaymentAttempt",extraAttemptId,"success");
 var extra=await store.Transaction(async tx=>{
  var checkout=await tx.Get("Checkout",retryCheckoutId);
  var primary=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o AND Kind='Primary'",("@o",retryOrderId)));
  var extras=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o AND Kind='Extra'",("@o",retryOrderId)));
  var compensation=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundCase' AND Id=@id",("@id","comp_"+extraAttemptId)));
  var stock=Convert.ToInt64(await tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_03'"));return(checkout.Body.Text("orderState"),checkout.Body["compensationRequired"]!.GetValue<bool>(),primary,extras,compensation,stock);
 });
 Check(extra==("Confirmed",true,1,1,1,retryStockBefore-1),"additional trusted success is isolated for compensation without a second reservation or confirmation");
} finally {
 await using var cleanupRetryPayment=new SqlConnection(admin.ConnectionString);await cleanupRetryPayment.OpenAsync();
 await using var removeRetryPayment=new SqlCommand(@"DELETE m0commerce.Inbox WHERE Dataset='demo_m0' AND SourceId IN(@failed,@success,@extra);
 DELETE m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId=@order;
 DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@order;
 DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@order;
 DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind IN('RefundCase','PaymentAttempt','Order','Checkout') AND Id IN(@case,@failedId,@successId,@extraId,@order,@checkout);
 UPDATE m0commerce.Inventory SET Stock=@stock WHERE Dataset='demo_m0' AND Sku='sku_demo_03'",cleanupRetryPayment);
 removeRetryPayment.Parameters.AddWithValue("@failed",failedAttemptId+"_terminal");removeRetryPayment.Parameters.AddWithValue("@success",successfulAttemptId+"_terminal");removeRetryPayment.Parameters.AddWithValue("@extra",extraAttemptId+"_terminal");removeRetryPayment.Parameters.AddWithValue("@case","comp_"+extraAttemptId);removeRetryPayment.Parameters.AddWithValue("@failedId",failedAttemptId);removeRetryPayment.Parameters.AddWithValue("@successId",successfulAttemptId);removeRetryPayment.Parameters.AddWithValue("@extraId",extraAttemptId);removeRetryPayment.Parameters.AddWithValue("@order",retryOrderId);removeRetryPayment.Parameters.AddWithValue("@checkout",retryCheckoutId);removeRetryPayment.Parameters.AddWithValue("@stock",retryStockBefore);await removeRetryPayment.ExecuteNonQueryAsync();
}
await worker.ExpireReservations();Console.WriteLine("SQL tests complete; synthetic last-item reservation remains fixture evidence");
} finally { await SetMaintenance(true); }

sealed class ReconciliationFixtureSecurity:IM0Security {
 public Task<M0Principal?> ResolveAsync(HttpContext context,CancellationToken ct=default)=>Task.FromResult<M0Principal?>(null);
 public Task<M0Principal> RequirePermissionAsync(HttpContext context,string permission,string resourceKind,string resourceId,CancellationToken ct=default)=>permission=="reconciliation.read"?Task.FromResult(new M0Principal("synthetic_report_reader","fixture-session",1,"AdminAuthenticated",[permission])):Task.FromException<M0Principal>(new M0Exception(403,"FORBIDDEN"));
 public Task<bool> CanDispatchAsync(string actorUserId,long securityVersion,string permission,string resourceKind,string resourceId,CancellationToken ct=default)=>Task.FromResult(false);
}
sealed class CommerceJourneyFixtureSecurity(string actor):IM0Security {
 static readonly string[] Allowed=["shipment.manage","support.respond","return.inspect","refund.execute","refund.check","reconciliation.read","orders.read","demo.control"];
 public Task<M0Principal?> ResolveAsync(HttpContext context,CancellationToken ct=default)=>Task.FromResult<M0Principal?>(null);
 public Task<M0Principal> RequirePermissionAsync(HttpContext context,string permission,string resourceKind,string resourceId,CancellationToken ct=default)=>Allowed.Contains(permission,StringComparer.Ordinal)?Task.FromResult(new M0Principal(actor,"synthetic-fixture-session",1,"AdminAuthenticated",[permission])):Task.FromException<M0Principal>(new M0Exception(403,"FORBIDDEN"));
 public Task<bool> CanDispatchAsync(string actorUserId,long securityVersion,string permission,string resourceKind,string resourceId,CancellationToken ct=default)=>Task.FromResult(actorUserId==actor&&Allowed.Contains(permission,StringComparer.Ordinal));
}
sealed class OrderReadFixtureSecurity(bool allowRead):IM0Security {
 public Task<M0Principal?> ResolveAsync(HttpContext context,CancellationToken ct=default)=>Task.FromResult<M0Principal?>(new M0Principal("fixture_owner","fixture-session",1,"AdminAuthenticated",allowRead?["orders.read"]:Array.Empty<string>()));
 public Task<M0Principal> RequirePermissionAsync(HttpContext context,string permission,string resourceKind,string resourceId,CancellationToken ct=default)=>allowRead&&permission=="orders.read"&&resourceKind=="Order"?Task.FromResult(new M0Principal("fixture_owner","fixture-session",1,"AdminAuthenticated",["orders.read"])):Task.FromException<M0Principal>(new M0Exception(403,"PERMISSION_CHANGED"));
 public Task<bool> CanDispatchAsync(string actorUserId,long securityVersion,string permission,string resourceKind,string resourceId,CancellationToken ct=default)=>Task.FromResult(false);
}
sealed class ReconciliationResultServices:IServiceProvider {
 public object? GetService(Type serviceType)=>serviceType==typeof(IOptions<JsonOptions>)?Options.Create(new JsonOptions()):serviceType==typeof(Microsoft.Extensions.Logging.ILoggerFactory)?NullLoggerFactory.Instance:null;
}
