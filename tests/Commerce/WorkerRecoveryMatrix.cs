using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using Kohami.Commerce;
using Kohami.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

static class WorkerRecoveryMatrix {
 public static async Task RunChildAsync(string[] args) {
  var config=args[1];
  var jobId=args[2];
  var gate=args[3];
  await File.WriteAllTextAsync(args[4],"ready");
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
  while(!File.Exists(gate))await Task.Delay(20,timeout.Token);
  var settings=RuntimeSettings.Load(config);
  var worker=new MockWorker(new CommerceStore(new SqlRuntime(settings)),TimeProvider.System,NullLogger<MockWorker>.Instance);
  Console.WriteLine("WORKER_LEASE_DISPATCHED:"+await worker.DispatchSpecificOnce(jobId,timeout.Token));
 }

 public static async Task RunAsync(string config,string adminConnectionString) {
  var suffix=Guid.NewGuid().ToString("N");
  var checkoutId="checkout_worker_recovery_"+suffix;
  var orderId="order_worker_recovery_"+suffix;
  var attemptId="pay_worker_recovery_"+suffix;
  var jobId="job_worker_recovery_"+suffix;
  var staleOwner="crashed_worker_"+suffix;
  var amount=100L;
  var seeded=false;
  var checkout=CommerceWire.Obj(
   ("id",checkoutId),
   ("checkoutId",checkoutId),
   ("orderId",orderId),
   ("mode","mock"),
   ("expiresAt",DateTimeOffset.UtcNow.AddMinutes(10)),
   ("snapshot",CommerceWire.Obj(("shippingTwd",0),("lines",new JsonArray()))),
   ("orderState","AwaitingPayment"),
   ("paymentState","Processing"),
   ("compensationRequired",false));
  var attempt=CommerceWire.Obj(
   ("id",attemptId),
   ("checkoutId",checkoutId),
   ("mode","mock"),
   ("state","Processing"),
   ("amountTwd",amount),
   ("currency","TWD"));
  var paymentEventId=jobId;

  try {
   await using(var connection=new SqlConnection(adminConnectionString)) {
    await connection.OpenAsync();
    await using var transaction=connection.BeginTransaction();
    await using var seed=new SqlCommand(@"
      INSERT commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body)
      VALUES('kohami_local','Checkout',@checkout,'worker_recovery_fixture',1,@body),
            ('kohami_local','Order',@order,'worker_recovery_fixture',1,@body),
            ('kohami_local','PaymentAttempt',@attempt,'worker_recovery_fixture',1,@attemptBody);
      INSERT commerce.Jobs(Dataset,Id,Kind,TargetId,Actor,SecurityVersion,Permission,ResourceKind,ResourceId,State,LeaseOwner,LeaseUntil,Fence,Payload,CreatedAt)
      VALUES('kohami_local',@job,'Payment',@attempt,'worker_recovery_fixture',0,NULL,'Order',@order,'Started',@owner,@lease,7,N'{""outcome"":""success""}',SYSDATETIMEOFFSET());",connection,transaction);
    seed.Parameters.AddWithValue("@checkout",checkoutId);
    seed.Parameters.AddWithValue("@order",orderId);
    seed.Parameters.AddWithValue("@attempt",attemptId);
    seed.Parameters.AddWithValue("@body",checkout.ToJsonString());
    seed.Parameters.AddWithValue("@attemptBody",attempt.ToJsonString());
    seed.Parameters.AddWithValue("@job",jobId);
    seed.Parameters.AddWithValue("@owner",staleOwner);
    seed.Parameters.AddWithValue("@lease",DateTimeOffset.UtcNow.AddMinutes(1));
    await seed.ExecuteNonQueryAsync();
    await transaction.CommitAsync();
    seeded=true;
   }

   var settings=RuntimeSettings.Load(config);
   var worker=new MockWorker(new CommerceStore(new SqlRuntime(settings)),TimeProvider.System,NullLogger<MockWorker>.Instance);
   Check(await worker.DispatchSpecificOnce(jobId)==0,"worker does not steal an unexpired lease from a live claimant");
   await using(var expire=new SqlConnection(adminConnectionString)) {
    await expire.OpenAsync();
    await using var command=new SqlCommand("UPDATE commerce.Jobs SET LeaseUntil=DATEADD(second,-1,SYSDATETIMEOFFSET()) WHERE Dataset='kohami_local' AND Id=@job AND State='Started'",expire);
    command.Parameters.AddWithValue("@job",jobId);
    if(await command.ExecuteNonQueryAsync()!=1)throw new InvalidOperationException("WORKER_RECOVERY_LEASE_NOT_EXPIRED");
   }

   var dispatches=await RaceRestartedWorkers(config,jobId);
   Check(dispatches.Order().SequenceEqual(new[]{0,1}),"two restarted workers race an expired started payment job and only one reclaims it");
   var settled=await ReadSettlement(adminConnectionString,jobId,attemptId,orderId);
   Check(settled==("Completed",8L,"Succeeded","Confirmed",1,1,1),"recovered payment applies one inbox event, primary collection, and receipt while advancing its fence");
   Check(await worker.DispatchSpecificOnce(jobId)==0,"completed recovered payment is not dispatched a second time");

   await using(var stale=new SqlConnection(adminConnectionString)) {
    await stale.OpenAsync();
    await using var command=new SqlCommand("UPDATE commerce.Jobs SET State='Completed' WHERE Dataset='kohami_local' AND Id=@job AND State='Started' AND Fence=@fence AND LeaseOwner=@owner AND LeaseUntil>SYSDATETIMEOFFSET()",stale);
    command.Parameters.AddWithValue("@job",jobId);
    command.Parameters.AddWithValue("@fence",7L);
    command.Parameters.AddWithValue("@owner",staleOwner);
    Check(await command.ExecuteNonQueryAsync()==0,"stale crashed worker fence cannot complete the recovered job");
   }
  } finally {
   if(seeded) {
    await using var cleanup=new SqlConnection(adminConnectionString);
    await cleanup.OpenAsync();
    await using var remove=new SqlCommand(@"
      DELETE commerce.Inbox WHERE Dataset='kohami_local' AND EventId=@event;
      DELETE commerce.Jobs WHERE Dataset='kohami_local' AND Id=@job;
      DELETE commerce.Jobs WHERE Dataset='kohami_local' AND Kind='DemoReceipt' AND TargetId=@receipt;
      DELETE commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@order;
      DELETE commerce.Documents WHERE Dataset='kohami_local' AND Id IN(@attempt,@order,@checkout,@receipt);",cleanup);
    remove.Parameters.AddWithValue("@event",paymentEventId);
    remove.Parameters.AddWithValue("@job",jobId);
    remove.Parameters.AddWithValue("@receipt","DEMO-collection_"+attemptId);
    remove.Parameters.AddWithValue("@order",orderId);
    remove.Parameters.AddWithValue("@attempt",attemptId);
    remove.Parameters.AddWithValue("@checkout",checkoutId);
    await remove.ExecuteNonQueryAsync();
   }
  }
  if(seeded)Check(await RemainingRows(adminConnectionString,jobId,attemptId,orderId,checkoutId)==0,"worker recovery fixture removes its inbox, jobs, ledger, and documents");

  await RunCommittedPaymentAndReceiptReplay(config,adminConnectionString);
  await RunUnknownPaymentReplay(config,adminConnectionString);
 }

 static async Task RunCommittedPaymentAndReceiptReplay(string config,string adminConnectionString) {
  var suffix=Guid.NewGuid().ToString("N");
  var checkoutId="checkout_worker_commit_"+suffix;
  var orderId="order_worker_commit_"+suffix;
  var attemptId="pay_worker_commit_"+suffix;
  var jobId="job_worker_commit_"+suffix;
  var receiptId="DEMO-collection_"+attemptId;
  var seeded=false;
  var checkout=CommerceWire.Obj(("id",checkoutId),("checkoutId",checkoutId),("orderId",orderId),("mode","mock"),
   ("expiresAt",DateTimeOffset.UtcNow.AddMinutes(10)),("snapshot",CommerceWire.Obj(("shippingTwd",0),("lines",new JsonArray()))),
   ("orderState","AwaitingPayment"),("paymentState","Processing"),("compensationRequired",false));
  var attempt=CommerceWire.Obj(("id",attemptId),("checkoutId",checkoutId),("mode","mock"),("state","Processing"),("amountTwd",100),("currency","TWD"));
  try {
   await using(var connection=new SqlConnection(adminConnectionString)) {
    await connection.OpenAsync();
    await using var seed=new SqlCommand(@"
      INSERT commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body)
      VALUES('kohami_local','Checkout',@checkout,'worker_commit_fixture',1,@body),
            ('kohami_local','Order',@order,'worker_commit_fixture',1,@body),
            ('kohami_local','PaymentAttempt',@attempt,'worker_commit_fixture',1,@attemptBody);
      INSERT commerce.Jobs(Dataset,Id,Kind,TargetId,Actor,SecurityVersion,Permission,ResourceKind,ResourceId,State,Payload,CreatedAt)
      VALUES('kohami_local',@job,'Payment',@attempt,'worker_commit_fixture',0,NULL,'Order',@order,'NotStarted',N'{""outcome"":""success""}',SYSDATETIMEOFFSET());",connection);
    seed.Parameters.AddWithValue("@checkout",checkoutId);seed.Parameters.AddWithValue("@order",orderId);seed.Parameters.AddWithValue("@attempt",attemptId);
    seed.Parameters.AddWithValue("@body",checkout.ToJsonString());seed.Parameters.AddWithValue("@attemptBody",attempt.ToJsonString());seed.Parameters.AddWithValue("@job",jobId);
    await seed.ExecuteNonQueryAsync();seeded=true;
   }
   var worker=new MockWorker(new CommerceStore(new SqlRuntime(RuntimeSettings.Load(config))),TimeProvider.System,NullLogger<MockWorker>.Instance);
   Check(await worker.DispatchSpecificOnce(jobId)==1,"committed NotStarted payment job resumes after worker restart");
   Check(await worker.DispatchSpecificOnce(jobId)==0,"replayed completed payment job does not reapply settlement");
   var payment=await ReadSettlement(adminConnectionString,jobId,attemptId,orderId);
   Check(payment==("Completed",1L,"Succeeded","Confirmed",1,1,1),"post-dispatch payment result and receipt job commit atomically with one settlement");
   var receiptJob=await ReadReceiptJob(adminConnectionString,"DemoReceipt",receiptId);
   Check(receiptJob is not null,"receipt notification remains queued after payment result commit");
   await ExpireStartedJob(adminConnectionString,receiptJob!,"crashed_receipt_"+suffix,3);
   var dispatches=await RaceRestartedWorkers(config,receiptJob!);
   Check(dispatches.Order().SequenceEqual(new[]{0,1}),"two restarted workers race the expired receipt notification and only one reclaims it");
   Check(await ReadReceiptState(adminConnectionString,"DemoReceipt",receiptId)==("Completed",4L,"Issued"),"receipt notification resumes without replaying the committed payment");
   Check(await worker.DispatchSpecificOnce(receiptJob!)==0,"issued receipt notification is not replayed again");
  } finally {
   if(seeded)await CleanupWorkerRows(adminConnectionString,jobId,attemptId,orderId,checkoutId,receiptId);
  }
  Check(await RemainingRows(adminConnectionString,jobId,attemptId,orderId,checkoutId)==0,"commit/replay fixture removes all synthetic rows");
 }

 static async Task RunUnknownPaymentReplay(string config,string adminConnectionString) {
  var suffix=Guid.NewGuid().ToString("N");
  var checkoutId="checkout_worker_unknown_"+suffix;
  var orderId="order_worker_unknown_"+suffix;
  var attemptId="pay_worker_unknown_"+suffix;
  var jobId="job_worker_unknown_"+suffix;
  var seeded=false;
  var checkout=CommerceWire.Obj(("id",checkoutId),("checkoutId",checkoutId),("orderId",orderId),("mode","mock"),
   ("expiresAt",DateTimeOffset.UtcNow.AddMinutes(10)),("snapshot",CommerceWire.Obj(("shippingTwd",0),("lines",new JsonArray()))),
   ("orderState","AwaitingPayment"),("paymentState","Processing"),("compensationRequired",false));
  var attempt=CommerceWire.Obj(("id",attemptId),("checkoutId",checkoutId),("mode","mock"),("state","Processing"),("amountTwd",100),("currency","TWD"));
  try {
   await using(var connection=new SqlConnection(adminConnectionString)) {
    await connection.OpenAsync();
    await using var seed=new SqlCommand(@"
      INSERT commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body)
      VALUES('kohami_local','Checkout',@checkout,'worker_unknown_fixture',1,@body),
            ('kohami_local','Order',@order,'worker_unknown_fixture',1,@body),
            ('kohami_local','PaymentAttempt',@attempt,'worker_unknown_fixture',1,@attemptBody);
      INSERT commerce.Jobs(Dataset,Id,Kind,TargetId,Actor,SecurityVersion,Permission,ResourceKind,ResourceId,State,Payload,CreatedAt)
      VALUES('kohami_local',@job,'Payment',@attempt,'worker_unknown_fixture',0,NULL,'Order',@order,'NotStarted',N'{""outcome"":""unknown""}',SYSDATETIMEOFFSET());",connection);
    seed.Parameters.AddWithValue("@checkout",checkoutId);seed.Parameters.AddWithValue("@order",orderId);seed.Parameters.AddWithValue("@attempt",attemptId);
    seed.Parameters.AddWithValue("@body",checkout.ToJsonString());seed.Parameters.AddWithValue("@attemptBody",attempt.ToJsonString());seed.Parameters.AddWithValue("@job",jobId);
    await seed.ExecuteNonQueryAsync();seeded=true;
   }
   var worker=new MockWorker(new CommerceStore(new SqlRuntime(RuntimeSettings.Load(config))),TimeProvider.System,NullLogger<MockWorker>.Instance);
   Check(await worker.DispatchSpecificOnce(jobId)==1,"worker persists an indeterminate payment result");
   Check(await worker.DispatchSpecificOnce(jobId)==0,"indeterminate payment is not retried as a new provider operation");
   var state=await ReadSettlement(adminConnectionString,jobId,attemptId,orderId);
   Check(state==("Completed",1L,"Unknown","AwaitingPayment",1,0,0),"Unknown remains unresolved with one inbox event and no duplicate collection or receipt");
  } finally {
   if(seeded)await CleanupWorkerRows(adminConnectionString,jobId,attemptId,orderId,checkoutId,"DEMO-collection_"+attemptId);
  }
  Check(await RemainingRows(adminConnectionString,jobId,attemptId,orderId,checkoutId)==0,"unknown-result fixture removes all synthetic rows");
 }

 static async Task ExpireStartedJob(string connectionString,string jobId,string owner,long fence) {
  await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();
  await using var command=new SqlCommand("UPDATE commerce.Jobs SET State='Started',LeaseOwner=@owner,LeaseUntil=DATEADD(second,-1,SYSDATETIMEOFFSET()),Fence=@fence WHERE Dataset='kohami_local' AND Id=@job AND State='NotStarted'",connection);
  command.Parameters.AddWithValue("@owner",owner);command.Parameters.AddWithValue("@fence",fence);command.Parameters.AddWithValue("@job",jobId);
  if(await command.ExecuteNonQueryAsync()!=1)throw new InvalidOperationException("WORKER_REPLAY_LEASE_SEED_FAILED");
 }

 static async Task<string?> ReadReceiptJob(string connectionString,string kind,string receiptId) {
  await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();
  await using var command=new SqlCommand("SELECT TOP(1) Id FROM commerce.Jobs WHERE Dataset='kohami_local' AND Kind=@kind AND TargetId=@target",connection);
  command.Parameters.AddWithValue("@kind",kind);command.Parameters.AddWithValue("@target",receiptId);return Convert.ToString(await command.ExecuteScalarAsync());
 }

 static async Task<(string state,long fence,string receipt)> ReadReceiptState(string connectionString,string kind,string receiptId) {
  await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();
  await using var command=new SqlCommand("SELECT j.State,j.Fence,JSON_VALUE(d.Body,'$.state') FROM commerce.Jobs j JOIN commerce.Documents d ON d.Dataset=j.Dataset AND d.Id=j.TargetId WHERE j.Dataset='kohami_local' AND j.Kind=@kind AND j.TargetId=@target",connection);
  command.Parameters.AddWithValue("@kind",kind);command.Parameters.AddWithValue("@target",receiptId);await using var reader=await command.ExecuteReaderAsync();
  if(!await reader.ReadAsync())throw new InvalidOperationException("WORKER_RECEIPT_REPLAY_STATE_MISSING");return(reader.GetString(0),reader.GetInt64(1),reader.GetString(2));
 }

 static async Task CleanupWorkerRows(string connectionString,string jobId,string attemptId,string orderId,string checkoutId,string receiptId) {
  await using var cleanup=new SqlConnection(connectionString);await cleanup.OpenAsync();
  await using var remove=new SqlCommand(@"
   DELETE commerce.Jobs WHERE Dataset='kohami_local' AND (Id=@job OR (Kind='DemoReceipt' AND TargetId=@receipt));
   DELETE commerce.Inbox WHERE Dataset='kohami_local' AND EventId=@event;
   DELETE commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@order;
   DELETE commerce.Documents WHERE Dataset='kohami_local' AND Id IN(@attempt,@order,@checkout,@receipt);",cleanup);
  remove.Parameters.AddWithValue("@job",jobId);remove.Parameters.AddWithValue("@event",jobId);remove.Parameters.AddWithValue("@order",orderId);
  remove.Parameters.AddWithValue("@attempt",attemptId);remove.Parameters.AddWithValue("@checkout",checkoutId);remove.Parameters.AddWithValue("@receipt",receiptId);await remove.ExecuteNonQueryAsync();
 }

 static async Task<int[]> RaceRestartedWorkers(string config,string jobId) {
  var suffix=Guid.NewGuid().ToString("N");
  var directory=Path.Combine(Path.GetTempPath(),"kohami-worker-lease-"+suffix);
  Directory.CreateDirectory(directory);
  var gate=Path.Combine(directory,"start");
  var processes=new List<Process>();
  var outputs=new List<Task<string>>();
  var errors=new List<Task<string>>();
  try {
   for(var i=0;i<2;i++) {
    var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Directory.GetCurrentDirectory()};
    start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    start.ArgumentList.Add("worker-lease-child");
    start.ArgumentList.Add(Path.GetFullPath(config));
    start.ArgumentList.Add(jobId);
    start.ArgumentList.Add(gate);
    start.ArgumentList.Add(Path.Combine(directory,$"ready-{i}"));
    var process=Process.Start(start)??throw new InvalidOperationException("WORKER_LEASE_CHILD_START_FAILED");
    processes.Add(process);
    outputs.Add(process.StandardOutput.ReadToEndAsync());
    errors.Add(process.StandardError.ReadToEndAsync());
   }
   using var readyTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
   while(processes.Select((_,i)=>File.Exists(Path.Combine(directory,$"ready-{i}"))).Any(ready=>!ready))await Task.Delay(20,readyTimeout.Token);
   await File.WriteAllTextAsync(gate,"go");
   foreach(var process in processes)await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
   var dispatches=new int[processes.Count];
   for(var i=0;i<processes.Count;i++) {
    var output=await outputs[i];
    if(processes[i].ExitCode!=0)throw new InvalidOperationException($"WORKER_LEASE_CHILD_FAILED exit={processes[i].ExitCode} stderr={await errors[i]} stdout={output}");
    var match=System.Text.RegularExpressions.Regex.Match(output,@"WORKER_LEASE_DISPATCHED:(\d+)");
    if(!match.Success||!int.TryParse(match.Groups[1].Value,out dispatches[i]))throw new InvalidOperationException("WORKER_LEASE_CHILD_RESULT_INVALID");
   }
   return dispatches;
  } finally {
   foreach(var process in processes){if(!process.HasExited)process.Kill(true);process.Dispose();}
   if(Directory.Exists(directory))Directory.Delete(directory,true);
  }
 }

 static async Task<(string state,long fence,string attempt,string order,int inbox,int collections,int receipts)> ReadSettlement(string connectionString,string jobId,string attemptId,string orderId) {
  await using var connection=new SqlConnection(connectionString);
  await connection.OpenAsync();
  await using var command=new SqlCommand(@"SELECT
    (SELECT State FROM commerce.Jobs WHERE Dataset='kohami_local' AND Id=@job),
    (SELECT Fence FROM commerce.Jobs WHERE Dataset='kohami_local' AND Id=@job),
    (SELECT JSON_VALUE(Body,'$.state') FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='PaymentAttempt' AND Id=@attempt),
    (SELECT JSON_VALUE(Body,'$.orderState') FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order' AND Id=@order),
    (SELECT COUNT(*) FROM commerce.Inbox WHERE Dataset='kohami_local' AND EventId=@job AND Applied=1),
    (SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@order AND Kind='Primary'),
    (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='DemoReceipt' AND JSON_VALUE(Body,'$.orderId')=@order)",connection);
  command.Parameters.AddWithValue("@job",jobId);
  command.Parameters.AddWithValue("@attempt",attemptId);
  command.Parameters.AddWithValue("@order",orderId);
  await using var result=await command.ExecuteReaderAsync();
  if(!await result.ReadAsync())throw new InvalidOperationException("WORKER_RECOVERY_SETTLEMENT_MISSING");
  return(result.GetString(0),result.GetInt64(1),result.GetString(2),result.GetString(3),result.GetInt32(4),result.GetInt32(5),result.GetInt32(6));
 }

 static async Task<int> RemainingRows(string connectionString,string jobId,string attemptId,string orderId,string checkoutId) {
  await using var connection=new SqlConnection(connectionString);
  await connection.OpenAsync();
  await using var command=new SqlCommand(@"SELECT
    (SELECT COUNT(*) FROM commerce.Inbox WHERE Dataset='kohami_local' AND EventId=@event)+
    (SELECT COUNT(*) FROM commerce.Jobs WHERE Dataset='kohami_local' AND (Id=@job OR (Kind='DemoReceipt' AND TargetId=@receipt)))+
    (SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@order)+
    (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Id IN(@attempt,@order,@checkout,@receipt))",connection);
  command.Parameters.AddWithValue("@event",jobId);
  command.Parameters.AddWithValue("@job",jobId);
  command.Parameters.AddWithValue("@receipt","DEMO-collection_"+attemptId);
  command.Parameters.AddWithValue("@order",orderId);
  command.Parameters.AddWithValue("@attempt",attemptId);
  command.Parameters.AddWithValue("@checkout",checkoutId);
  return Convert.ToInt32(await command.ExecuteScalarAsync());
 }

 static void Check(bool pass,string name) {
  if(!pass)throw new InvalidOperationException("FAILED: "+name);
  Console.WriteLine("PASS: "+name);
 }
}
