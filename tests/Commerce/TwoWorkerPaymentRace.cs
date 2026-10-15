using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using Kohami.M0.Commerce;
using Kohami.M0.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

static class TwoWorkerPaymentRace {
 public static async Task RunChildAsync(string[] args) {
  var settings=M0Settings.Load(args[1]);
  var gate=args[2];var ready=args[3];
  await File.WriteAllTextAsync(ready,"ready");
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
  while(!File.Exists(gate))await Task.Delay(20,timeout.Token);
  var worker=new MockWorker(new CommerceStore(new M0Sql(settings)),TimeProvider.System,NullLogger<MockWorker>.Instance);
  await worker.RecordMockMoneyEvent(new MockMoneyEvent(args[4],"mock","demo_m0",args[5],args[7],long.Parse(args[8]),"TWD",1,args[9]),timeout.Token);
 }

 public static async Task RunAsync(string config,string adminConnectionString) {
  var suffix=Guid.NewGuid().ToString("N");
  var orderId="order_worker_race_"+suffix;var checkoutId="checkout_worker_race_"+suffix;var attemptId="pay_worker_race_"+suffix;
  var ownerId="fixture_worker_race_"+suffix;var eventId="evt_worker_race_"+suffix;var amount=100L;var sku="sku_demo_03";
  long? originalStock=null;var seeded=false;
  var directory=Path.Combine(Path.GetTempPath(),"kohami-worker-race-"+suffix);Directory.CreateDirectory(directory);
  var gate=Path.Combine(directory,"start");
  try {
   var checkout=CommerceWire.Obj(("id",checkoutId),("checkoutId",checkoutId),("orderId",orderId),("mode","mock"),("expiresAt",DateTimeOffset.UtcNow.AddMinutes(10)),("snapshot",CommerceWire.Obj(("shippingTwd",0),("lines",new JsonArray()))),("orderState","AwaitingPayment"),("paymentState","Processing"),("compensationRequired",false));
   var attempt=CommerceWire.Obj(("id",attemptId),("checkoutId",checkoutId),("mode","mock"),("state","Processing"),("amountTwd",amount),("currency","TWD"));
   await using(var connection=new SqlConnection(adminConnectionString)) {
    await connection.OpenAsync();await using var transaction=await connection.BeginTransactionAsync();
    await using(var stock=new SqlCommand("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@sku",connection,(SqlTransaction)transaction)) {
     stock.Parameters.AddWithValue("@sku",sku);var value=await stock.ExecuteScalarAsync();
     if(value is null||value is DBNull)throw new InvalidOperationException("WORKER_RACE_FIXTURE_SKU_MISSING");
     originalStock=Convert.ToInt64(value);
    }
    await using(var reserve=new SqlCommand("UPDATE m0commerce.Inventory SET Stock=Stock-1 WHERE Dataset='demo_m0' AND Sku=@sku AND Stock>0;IF @@ROWCOUNT<>1 THROW 51000,'WORKER_RACE_STOCK_UNAVAILABLE',1;INSERT m0commerce.Reservations VALUES('demo_m0',@order,@sku,1,'Held',DATEADD(minute,10,SYSDATETIMEOFFSET()));INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES('demo_m0','Checkout',@checkout,@owner,1,@checkoutBody),('demo_m0','Order',@order,@owner,1,@orderBody),('demo_m0','PaymentAttempt',@attempt,@owner,1,@attemptBody)",connection,(SqlTransaction)transaction)) {
     reserve.Parameters.AddWithValue("@sku",sku);reserve.Parameters.AddWithValue("@order",orderId);reserve.Parameters.AddWithValue("@checkout",checkoutId);reserve.Parameters.AddWithValue("@attempt",attemptId);reserve.Parameters.AddWithValue("@owner",ownerId);reserve.Parameters.AddWithValue("@checkoutBody",checkout.ToJsonString());reserve.Parameters.AddWithValue("@orderBody",checkout.ToJsonString());reserve.Parameters.AddWithValue("@attemptBody",attempt.ToJsonString());await reserve.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();seeded=true;
   }

   var processes=new List<Process>();var outputs=new List<Task<string>>();var errors=new List<Task<string>>();
   try {
    for(var i=0;i<2;i++) {
     var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Directory.GetCurrentDirectory()};
     start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);start.ArgumentList.Add("worker-race-child");start.ArgumentList.Add(Path.GetFullPath(config));start.ArgumentList.Add(gate);start.ArgumentList.Add(Path.Combine(directory,$"ready-{i}"));start.ArgumentList.Add(eventId);start.ArgumentList.Add(orderId);start.ArgumentList.Add(checkoutId);start.ArgumentList.Add(attemptId);start.ArgumentList.Add(amount.ToString());start.ArgumentList.Add("success");
     var process=Process.Start(start)??throw new InvalidOperationException("WORKER_RACE_CHILD_START_FAILED");processes.Add(process);outputs.Add(process.StandardOutput.ReadToEndAsync());errors.Add(process.StandardError.ReadToEndAsync());
    }
    using var readyTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
    while(processes.Select((_,i)=>File.Exists(Path.Combine(directory,$"ready-{i}"))).Any(ready=>!ready))await Task.Delay(20,readyTimeout.Token);
    await File.WriteAllTextAsync(gate,"go");
    foreach(var process in processes)await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
    for(var i=0;i<processes.Count;i++)if(processes[i].ExitCode!=0)throw new InvalidOperationException($"WORKER_RACE_CHILD_FAILED exit={processes[i].ExitCode} stderr={await errors[i]} stdout={await outputs[i]}");
   } finally {
    foreach(var process in processes){if(!process.HasExited)process.Kill(true);process.Dispose();}
   }

   int inboxTotal,inboxApplied,primaryCount,receiptCount;string attemptState,orderState,paymentState,reservationState;
   await using(var verify=new SqlConnection(adminConnectionString)) {
    await verify.OpenAsync();await using var query=new SqlCommand(@"SELECT
      (SELECT COUNT(*) FROM m0commerce.Inbox WHERE Dataset='demo_m0' AND EventId=@event),
      (SELECT COUNT(*) FROM m0commerce.Inbox WHERE Dataset='demo_m0' AND EventId=@event AND Applied=1),
      (SELECT JSON_VALUE(Body,'$.state') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='PaymentAttempt' AND Id=@attempt),
      (SELECT JSON_VALUE(Body,'$.orderState') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@order),
      (SELECT JSON_VALUE(Body,'$.paymentState') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND Id=@checkout),
      (SELECT State FROM m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@order AND Sku=@sku),
      (SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@order AND Kind='Primary'),
      (SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='DemoReceipt' AND JSON_VALUE(Body,'$.orderId')=@order)",verify);
    query.Parameters.AddWithValue("@event",eventId);query.Parameters.AddWithValue("@attempt",attemptId);query.Parameters.AddWithValue("@order",orderId);query.Parameters.AddWithValue("@checkout",checkoutId);query.Parameters.AddWithValue("@sku",sku);
    await using var reader=await query.ExecuteReaderAsync();if(!await reader.ReadAsync())throw new InvalidOperationException("WORKER_RACE_VERIFY_EMPTY");
    inboxTotal=reader.GetInt32(0);inboxApplied=reader.GetInt32(1);attemptState=reader.GetString(2);orderState=reader.GetString(3);paymentState=reader.GetString(4);reservationState=reader.GetString(5);primaryCount=reader.GetInt32(6);receiptCount=reader.GetInt32(7);
   }
   Check(inboxTotal==1&&inboxApplied==1,"two independent worker processes apply a concurrent duplicate provider event once");
   Check(attemptState=="Succeeded"&&orderState=="Confirmed"&&paymentState=="Succeeded"&&reservationState=="Consumed"&&primaryCount==1&&receiptCount==1,"duplicate payment race preserves one confirmed order, collection, receipt, and consumed reservation");
  } finally {
   if(seeded&&originalStock is not null) {
    await using var cleanup=new SqlConnection(adminConnectionString);await cleanup.OpenAsync();await using var remove=new SqlCommand(@"DELETE m0commerce.Inbox WHERE Dataset='demo_m0' AND EventId=@event;
      DELETE m0commerce.Jobs WHERE Dataset='demo_m0' AND Actor=@owner;
      DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@order;
      DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@order;
      DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND (Id IN(@attempt,@order,@checkout) OR JSON_VALUE(Body,'$.orderId')=@order);
      UPDATE m0commerce.Inventory SET Stock=@stock WHERE Dataset='demo_m0' AND Sku=@sku",cleanup);
    remove.Parameters.AddWithValue("@event",eventId);remove.Parameters.AddWithValue("@owner",ownerId);remove.Parameters.AddWithValue("@order",orderId);remove.Parameters.AddWithValue("@attempt",attemptId);remove.Parameters.AddWithValue("@checkout",checkoutId);remove.Parameters.AddWithValue("@stock",originalStock.Value);remove.Parameters.AddWithValue("@sku",sku);await remove.ExecuteNonQueryAsync();
   }
   if(Directory.Exists(directory))Directory.Delete(directory,true);
  }
 }

 static void Check(bool pass,string name){if(!pass)throw new InvalidOperationException("FAILED: "+name);Console.WriteLine("PASS: "+name);}
}
