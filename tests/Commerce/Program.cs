using System.Text.Json.Nodes;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Kohami.M0.Commerce;
using Kohami.M0.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

if (args.Length is >= 3 and <= 6 && args[0] == "fixture-window") {
 await FixtureAcceptanceWindow.RunAsync(args[1], args[2], args.Skip(3).ToArray());
 return;
}

static void Check(bool pass,string name){if(!pass)throw new InvalidOperationException("FAILED: "+name);Console.WriteLine("PASS: "+name);}
var a=new JsonArray(CommerceWire.Item("sku_demo_01",1,500,1),CommerceWire.Item("sku_demo_03",2,650,10));
var s=CommerceWire.Snapshot(a,60,true);
Check(s.Number("totalTwd")==1590&&s.Number("discountTwd")==270,"integer discount total");
var units=s["lines"]!.AsArray().SelectMany(x=>x!["units"]!.AsArray()).Select(x=>x!.AsObject().Number("paidTwd")).ToArray();
Check(units.SequenceEqual(new long[]{425,552,553}),"stable per-unit remainder allocation");
var reversed=new JsonArray(a[1]!.DeepClone(),a[0]!.DeepClone());Check(CommerceWire.Snapshot(reversed,60,true).ToJsonString()==s.ToJsonString(),"SKU ordering stable across input order");
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
 INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion,EncryptedSeed) VALUES(@owner,@email,REPLICATE(N'x',100),1,0,'SetupOnly',1,N'synthetic-encrypted-seed');
 INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason) VALUES(@grant,@owner,'Owner','access.manage','Dataset','demo_m0',DATEADD(minute,-1,@now),DATEADD(day,1,@now),'fixture','isolated transfer resume test');
 INSERT m0.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce,MfaVerifiedAt) VALUES(@session,@owner,1,'AdminMfaVerified',DATEADD(minute,5,@now),DATEADD(hour,1,@now),'fixture_nonce',@now);
 INSERT ops.Audit(OperationId,Action,Target,PreviewHash,OldEpoch,NewEpoch,AppliedAt,HostReconciled,ReplacementTarget) VALUES(@transfer,'TransferDemoOwner',@oldOwner,@hash,@oldEpoch,@oldEpoch+1,@now,0,@owner);
 SELECT @oldEpoch,@oldMaintenance,@oldTarget,@oldPrepared,@oldActivated,@oldBootstrapOp,@oldBootstrapUser",c);seed.Parameters.AddWithValue("@owner",owner);seed.Parameters.AddWithValue("@email",owner+"@example.test");seed.Parameters.AddWithValue("@grant",grant);seed.Parameters.AddWithValue("@session",session);seed.Parameters.AddWithValue("@transfer",transfer);seed.Parameters.AddWithValue("@oldOwner",oldOwner);seed.Parameters.AddWithValue("@hash",new string('a',64));var oldEpoch=seed.Parameters.Add("@oldEpoch",System.Data.SqlDbType.BigInt);oldEpoch.Direction=System.Data.ParameterDirection.Output;var oldMaintenance=seed.Parameters.Add("@oldMaintenance",System.Data.SqlDbType.Bit);oldMaintenance.Direction=System.Data.ParameterDirection.Output;var oldTarget=seed.Parameters.Add("@oldTarget",System.Data.SqlDbType.NVarChar,100);oldTarget.Direction=System.Data.ParameterDirection.Output;var oldPrepared=seed.Parameters.Add("@oldPrepared",System.Data.SqlDbType.Bit);oldPrepared.Direction=System.Data.ParameterDirection.Output;var oldActivated=seed.Parameters.Add("@oldActivated",System.Data.SqlDbType.Bit);oldActivated.Direction=System.Data.ParameterDirection.Output;var oldBootstrapOp=seed.Parameters.Add("@oldBootstrapOp",System.Data.SqlDbType.UniqueIdentifier);oldBootstrapOp.Direction=System.Data.ParameterDirection.Output;var oldBootstrapUser=seed.Parameters.Add("@oldBootstrapUser",System.Data.SqlDbType.NVarChar,100);oldBootstrapUser.Direction=System.Data.ParameterDirection.Output;await seed.ExecuteNonQueryAsync();originalEpoch=(long)oldEpoch.Value;originalMaintenance=(bool)oldMaintenance.Value;originalTarget=oldTarget.Value is DBNull?null:(string)oldTarget.Value;originalOwnerPrepared=(bool)oldPrepared.Value;originalBootstrapActivated=(bool)oldActivated.Value;originalBootstrapOperation=oldBootstrapOp.Value is DBNull?null:(Guid)oldBootstrapOp.Value;originalBootstrapUser=oldBootstrapUser.Value is DBNull?null:(string)oldBootstrapUser.Value;}
 try{await using var c=new SqlConnection(admin.ConnectionString);await c.OpenAsync();var issued=DateTimeOffset.UtcNow.AddMinutes(-1);var expiry=issued.AddMinutes(10);var activationHash=new string('b',64);
  await using(var apply=new SqlCommand(@"DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));EXEC ops.Apply @PermitId,@OperationId,'InitialOwnerActivate',@Target,'demo_m0',@fp,@PreviewHash,@epoch,@issued,@expires",c)){apply.Parameters.AddWithValue("@PermitId",activationPermit);apply.Parameters.AddWithValue("@OperationId",activation);apply.Parameters.AddWithValue("@Target",owner);apply.Parameters.AddWithValue("@PreviewHash",activationHash);apply.Parameters.AddWithValue("@epoch",originalEpoch+1);apply.Parameters.AddWithValue("@issued",issued);apply.Parameters.AddWithValue("@expires",expiry);await apply.ExecuteNonQueryAsync();}
  await using(var verifyTarget=new SqlCommand("SELECT Maintenance,MaintenanceTargetUserId FROM m0.SecurityState WHERE Id=1",c)){await using var result=await verifyTarget.ExecuteReaderAsync();await result.ReadAsync();Check(result.GetBoolean(0)&&result.GetString(1)==owner,"Owner activation keeps its maintenance login target for MFA rebind");}
  await using(var reconcileActivation=new SqlCommand(@"DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));EXEC ops.Reconcile @PermitId,@OperationId,'InitialOwnerActivate',@Target,'demo_m0',@fp,@PreviewHash,@permitEpoch,@epoch,@issued,@expires",c)){reconcileActivation.Parameters.AddWithValue("@PermitId",activationReconcilePermit);reconcileActivation.Parameters.AddWithValue("@OperationId",activation);reconcileActivation.Parameters.AddWithValue("@Target",owner);reconcileActivation.Parameters.AddWithValue("@PreviewHash",activationHash);reconcileActivation.Parameters.AddWithValue("@permitEpoch",originalEpoch+1);reconcileActivation.Parameters.AddWithValue("@epoch",originalEpoch+2);reconcileActivation.Parameters.AddWithValue("@issued",issued);reconcileActivation.Parameters.AddWithValue("@expires",expiry);await reconcileActivation.ExecuteNonQueryAsync();}
  await using(var expire=new SqlCommand("UPDATE m0.Sessions SET ExpiresAt=DATEADD(second,-1,SYSDATETIMEOFFSET()),MfaVerifiedAt=DATEADD(minute,-6,SYSDATETIMEOFFSET()) WHERE SessionId=@id",c)){expire.Parameters.AddWithValue("@id",session);await expire.ExecuteNonQueryAsync();}
  await using(var reauth=new SqlCommand(@"BEGIN TRANSACTION;DECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='kohami.m0.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;IF @lock<0 THROW 51000,'TEST_LOCK_FAILED',1;DECLARE @now datetimeoffset=SYSDATETIMEOFFSET();EXEC m0.security_create_session @UserId,@SecurityVersion,@SessionId,@Nonce,@now;SELECT Stage FROM m0.Sessions WHERE SessionId=@SessionId;COMMIT",c)){reauth.Parameters.AddWithValue("@UserId",owner);reauth.Parameters.AddWithValue("@SecurityVersion",1L);reauth.Parameters.AddWithValue("@SessionId",reauthSession);reauth.Parameters.AddWithValue("@Nonce",Guid.NewGuid().ToString("N"));var stage=(string?)await reauth.ExecuteScalarAsync();Check(stage=="AdminPasswordVerified","maintenance target Owner can reauthenticate after MFA session expiry");}
  await using(var mfa=new SqlCommand("UPDATE m0.Sessions SET Stage='AdminMfaVerified',ExpiresAt=DATEADD(minute,5,SYSDATETIMEOFFSET()),AbsoluteUntil=DATEADD(hour,1,SYSDATETIMEOFFSET()),MfaVerifiedAt=SYSDATETIMEOFFSET() WHERE SessionId=@id",c)){mfa.Parameters.AddWithValue("@id",reauthSession);await mfa.ExecuteNonQueryAsync();}
  await using(var reconcileTransfer=new SqlCommand(@"DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));EXEC ops.Reconcile @PermitId,@OperationId,'TransferDemoOwner',@Target,'demo_m0',@fp,@PreviewHash,@epoch,@epoch,@issued,@expires",c)){reconcileTransfer.Parameters.AddWithValue("@PermitId",resumePermit);reconcileTransfer.Parameters.AddWithValue("@OperationId",transfer);reconcileTransfer.Parameters.AddWithValue("@Target",oldOwner);reconcileTransfer.Parameters.AddWithValue("@PreviewHash",new string('a',64));reconcileTransfer.Parameters.AddWithValue("@epoch",originalEpoch+2);reconcileTransfer.Parameters.AddWithValue("@issued",issued);reconcileTransfer.Parameters.AddWithValue("@expires",expiry);await reconcileTransfer.ExecuteNonQueryAsync();}
  await using var verify=new SqlCommand("SELECT a.HostReconciled,st.Maintenance FROM ops.Audit a CROSS JOIN m0.SecurityState st WHERE a.OperationId=@id AND st.Id=1",c);verify.Parameters.AddWithValue("@id",transfer);await using var result2=await verify.ExecuteReaderAsync();await result2.ReadAsync();Check(result2.GetBoolean(0)&&!result2.GetBoolean(1),"Owner transfer resume after reauthentication reconciles and clears maintenance");
 }finally{await using var cleanup=new SqlConnection(admin.ConnectionString);await cleanup.OpenAsync();await using var remove=new SqlCommand(@"DELETE ops.ReconcileUses WHERE OperationId IN(@transfer,@activation);DELETE ops.PermitUses WHERE OperationId IN(@transfer,@activation);DELETE ops.Audit WHERE OperationId IN(@transfer,@activation);DELETE m0.SecurityAudit WHERE OperationId=@activation;DELETE m0.Sessions WHERE SessionId IN(@session,@reauthSession);DELETE m0.Grants WHERE GrantId=@grant;DELETE m0.Users WHERE UserId=@owner;UPDATE m0.SecurityState SET Epoch=@epoch,Maintenance=@maintenance,MaintenanceTargetUserId=@target,OwnerPrepared=@ownerPrepared,BootstrapActivated=@bootstrapActivated,BootstrapOperationId=@bootstrapOperation,BootstrapUserId=@bootstrapUser WHERE Id=1",cleanup);remove.Parameters.AddWithValue("@transfer",transfer);remove.Parameters.AddWithValue("@activation",activation);remove.Parameters.AddWithValue("@session",session);remove.Parameters.AddWithValue("@reauthSession",reauthSession);remove.Parameters.AddWithValue("@grant",grant);remove.Parameters.AddWithValue("@owner",owner);remove.Parameters.AddWithValue("@epoch",originalEpoch);remove.Parameters.AddWithValue("@maintenance",originalMaintenance);remove.Parameters.AddWithValue("@target",(object?)originalTarget??DBNull.Value);remove.Parameters.AddWithValue("@ownerPrepared",originalOwnerPrepared);remove.Parameters.AddWithValue("@bootstrapActivated",originalBootstrapActivated);remove.Parameters.AddWithValue("@bootstrapOperation",(object?)originalBootstrapOperation??DBNull.Value);remove.Parameters.AddWithValue("@bootstrapUser",(object?)originalBootstrapUser??DBNull.Value);await remove.ExecuteNonQueryAsync();}
}
var store=new CommerceStore(new M0Sql(settings));var worker=new MockWorker(store,TimeProvider.System,NullLogger<MockWorker>.Instance);
// Restore only the disposable synthetic fixture rows used by this check; never target the demo database.
await using(var reset=new SqlConnection(admin.ConnectionString)){await reset.OpenAsync();await using var command=new SqlCommand(@"UPDATE m0commerce.Inventory SET Stock=1 WHERE Dataset='demo_m0' AND Sku='sku_demo_01'; DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId LIKE 'race\_%' ESCAPE '\'; DELETE m0commerce.Inbox WHERE Dataset='demo_m0' AND SourceId LIKE 'maintenance_started_%_terminal'; DELETE m0commerce.Jobs WHERE Dataset='demo_m0' AND Actor='fixture'; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND OwnerId='fixture' AND Id LIKE 'maintenance_started_%';",reset);await command.ExecuteNonQueryAsync();}
var nativeBatches=System.Text.RegularExpressions.Regex.Split(File.ReadAllText("tools/native-operations/Operations.sql"),@"^\s*GO\s*$",System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x));
await using(var setup=new SqlConnection(admin.ConnectionString)){await setup.OpenAsync();foreach(var batch in nativeBatches){await using var deploy=new SqlCommand(batch,setup){CommandTimeout=30};await deploy.ExecuteNonQueryAsync();}}
var pendingResumeId=Guid.NewGuid();var pendingResumeHash=new string('a',64);await SetMaintenance(true);
await using(var pending=new SqlConnection(admin.ConnectionString)){await pending.OpenAsync();await using(var seed=new SqlCommand("DECLARE @epoch bigint=(SELECT Epoch FROM m0.SecurityState WHERE Id=1);INSERT ops.Audit(OperationId,Action,Target,PreviewHash,OldEpoch,NewEpoch,AppliedAt,HostReconciled) VALUES(@id,'CredentialRecovery','missing_rebind_fixture',@hash,@epoch-1,@epoch,SYSDATETIMEOFFSET(),0)",pending)){seed.Parameters.AddWithValue("@id",pendingResumeId);seed.Parameters.AddWithValue("@hash",pendingResumeHash);await seed.ExecuteNonQueryAsync();}var now=DateTimeOffset.UtcNow;try{await using var reconcile=new SqlCommand("DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));EXEC ops.Reconcile @PermitId,@OperationId,'CredentialRecovery','missing_rebind_fixture','demo_m0',@fp,@PreviewHash,@epoch,@epoch,@issued,@expires",pending);reconcile.Parameters.AddWithValue("@PermitId",Guid.NewGuid());reconcile.Parameters.AddWithValue("@OperationId",pendingResumeId);reconcile.Parameters.AddWithValue("@PreviewHash",pendingResumeHash);reconcile.Parameters.AddWithValue("@epoch",await new SqlCommand("SELECT Epoch FROM m0.SecurityState WHERE Id=1",pending).ExecuteScalarAsync());reconcile.Parameters.AddWithValue("@issued",now);reconcile.Parameters.AddWithValue("@expires",now.AddMinutes(10));await reconcile.ExecuteNonQueryAsync();throw new InvalidOperationException("FAILED: reconciliation must wait for fresh MFA");}catch(SqlException e) when(e.Number==51000){}await using var verify=new SqlCommand("SELECT HostReconciled,(SELECT Maintenance FROM m0.SecurityState WHERE Id=1) FROM ops.Audit WHERE OperationId=@id",pending);verify.Parameters.AddWithValue("@id",pendingResumeId);await using var result=await verify.ExecuteReaderAsync();await result.ReadAsync();Check(!result.GetBoolean(0)&&result.GetBoolean(1),"native resume cannot clear maintenance before target fresh MFA");}
await using(var cleanup=new SqlConnection(admin.ConnectionString)){await cleanup.OpenAsync();await using var remove=new SqlCommand("DELETE ops.Audit WHERE OperationId=@id",cleanup);remove.Parameters.AddWithValue("@id",pendingResumeId);await remove.ExecuteNonQueryAsync();}await SetMaintenance(false);await VerifyTransferResumeAfterActivation();
try {
await using(var lockConnection=new SqlConnection(admin.ConnectionString)){await lockConnection.OpenAsync();await using var lockTx=await lockConnection.BeginTransactionAsync();await using(var acquire=new SqlCommand("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource='kohami.m0.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000;IF @r<0 THROW 51000,'LOCK_TEST_FAILED',1",lockConnection,(SqlTransaction)lockTx))await acquire.ExecuteNonQueryAsync();var resolver=new CommerceResourceResolver(new M0Sql(settings));var resolved=await resolver.ResolveScopesAsync("RefundCase","refund_case_demo").WaitAsync(TimeSpan.FromSeconds(3));Check(resolved.Any(x=>x==new CommerceScope("Order","order_refund_demo")),"scope resolution reads under existing exclusive security lock without self-wait");await lockTx.RollbackAsync();}
await SetMaintenance(true);var startedId="maintenance_started_"+Guid.NewGuid().ToString("N");var pendingId="maintenance_pending_"+Guid.NewGuid().ToString("N");var startedJob="job_maintenance_"+Guid.NewGuid().ToString("N");var pendingJob="job_maintenance_"+Guid.NewGuid().ToString("N");
await using(var seedJobs=new SqlConnection(admin.ConnectionString)){await seedJobs.OpenAsync();await using var seed=new SqlCommand("INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES('demo_m0','RefundOperation',@started,'fixture',1,N'{\"state\":\"Succeeded\",\"dispatchState\":\"Started\",\"amountTwd\":1,\"currency\":\"TWD\"}');INSERT m0commerce.Jobs(Dataset,Id,Kind,TargetId,Actor,SecurityVersion,Permission,ResourceKind,ResourceId,State,Payload,CreatedAt) VALUES('demo_m0',@startedJob,'Refund',@started,'fixture',1,'refund.execute','Case','fixture','Started',N'{\"outcome\":\"success\"}',SYSUTCDATETIME()),('demo_m0',@pendingJob,'Scenario',@pending,'fixture',1,'demo.control','Dataset','demo_m0','NotStarted',N'{\"targetKind\":\"PaymentAttempt\",\"targetId\":\"missing\",\"scenario\":\"success\"}',SYSUTCDATETIME())",seedJobs);seed.Parameters.AddWithValue("@started",startedId);seed.Parameters.AddWithValue("@startedJob",startedJob);seed.Parameters.AddWithValue("@pendingJob",pendingJob);seed.Parameters.AddWithValue("@pending",pendingId);await seed.ExecuteNonQueryAsync();}
Check(await worker.DispatchOnce()==1,"maintenance resumes already-started worker job only");
await using(var checkJobs=new SqlConnection(admin.ConnectionString)){await checkJobs.OpenAsync();await using var check=new SqlCommand("SELECT (SELECT State FROM m0commerce.Jobs WHERE Id=@startedJob),(SELECT State FROM m0commerce.Jobs WHERE Id=@pendingJob),(SELECT Applied FROM m0commerce.Inbox WHERE SourceId=@source)",checkJobs);check.Parameters.AddWithValue("@startedJob",startedJob);check.Parameters.AddWithValue("@pendingJob",pendingJob);check.Parameters.AddWithValue("@source",startedId+"_terminal");await using var result=await check.ExecuteReaderAsync();await result.ReadAsync();Check(result.GetString(0)=="Completed"&&result.GetString(1)=="NotStarted"&&result.GetBoolean(2),"maintenance tracks started job completion without dispatching new work");}
await using(var cleanupJobs=new SqlConnection(admin.ConnectionString)){await cleanupJobs.OpenAsync();await using var cleanup=new SqlCommand("DELETE m0commerce.Inbox WHERE SourceId=@source;DELETE m0commerce.Jobs WHERE Id IN(@startedJob,@pendingJob);DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND Id=@started",cleanupJobs);cleanup.Parameters.AddWithValue("@source",startedId+"_terminal");cleanup.Parameters.AddWithValue("@startedJob",startedJob);cleanup.Parameters.AddWithValue("@pendingJob",pendingJob);cleanup.Parameters.AddWithValue("@started",startedId);await cleanup.ExecuteNonQueryAsync();}await SetMaintenance(false);
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
 DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@collection;
 DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind IN('RefundCase','PaymentAttempt','Order','Checkout') AND Id IN(@compensation,@attempt,@order,@checkout);
 DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@order;
 UPDATE m0commerce.Inventory SET Stock=@stock WHERE Dataset='demo_m0' AND Sku='sku_demo_03'",cleanup);
 remove.Parameters.AddWithValue("@unknown",lateAttemptId+"_unknown");remove.Parameters.AddWithValue("@terminal",lateAttemptId+"_terminal");remove.Parameters.AddWithValue("@collection","collection_"+lateAttemptId);remove.Parameters.AddWithValue("@compensation","comp_"+lateAttemptId);remove.Parameters.AddWithValue("@attempt",lateAttemptId);remove.Parameters.AddWithValue("@order",lateOrderId);remove.Parameters.AddWithValue("@checkout",lateCheckoutId);remove.Parameters.AddWithValue("@stock",lateStockBefore);await remove.ExecuteNonQueryAsync();
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
