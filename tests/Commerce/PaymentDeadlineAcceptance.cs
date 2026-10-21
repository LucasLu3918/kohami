using System.Text;
using System.Text.Json.Nodes;
using Kohami.Commerce;
using Kohami.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

static class PaymentDeadlineAcceptance {
 public static async Task RunAsync(string config,string adminConnectionString) {
  var settings=RuntimeSettings.Load(config);
  var clock=new MutableTimeProvider(new DateTimeOffset(2026,10,16,0,0,0,TimeSpan.Zero));
  var store=new CommerceStore(new SqlRuntime(settings));
  var worker=new MockWorker(store,clock,NullLogger<MockWorker>.Instance);
  var ownerPrefix="ac06_owner_"+Guid.NewGuid().ToString("N");
  var protection=new EphemeralDataProtectionProvider();
  var serviceSecurity=new PaymentOwnerSecurity();

  async Task<(string orderId,string checkoutId,string attemptId,string owner,long expires,long stockBefore)> Seed(string label) {
   var suffix=Guid.NewGuid().ToString("N");var orderId="ac06_order_"+label+"_"+suffix;var checkoutId="ac06_checkout_"+label+"_"+suffix;var attemptId="ac06_payment_"+label+"_"+suffix;var owner=ownerPrefix+"_"+label+"_"+suffix;
   var createdAt=clock.GetUtcNow();var expires=createdAt.AddMinutes(15);var snapshot=CommerceWire.Snapshot(new JsonArray(CommerceWire.Item("sku_demo_03",1,650,10)),60,true);var line=snapshot["lines"]!.AsArray()[0]!.AsObject();line["orderLineId"]="ac06_line_"+suffix;line["units"]!.AsArray()[0]!.AsObject()["unitId"]="ac06_unit_"+suffix;
   var checkout=CommerceWire.Obj(("checkoutId",checkoutId),("orderId",orderId),("mode","mock"),("expiresAt",expires),("snapshot",snapshot),("orderState","AwaitingPayment"),("paymentState","Processing"),("compensationRequired",false),("placedAt",createdAt),("email",owner+"@example.test"));
   var attempt=CommerceWire.Obj(("id",attemptId),("checkoutId",checkoutId),("mode","mock"),("state","Processing"),("amountTwd",snapshot.Number("totalTwd")),("currency","TWD"));
   var seeded=await store.Transaction(async tx=>{
    var rows=await tx.Rows("SELECT Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku='sku_demo_03'");if(rows.Count!=1)throw new InvalidOperationException("AC06_FIXTURE_SKU_MISSING");var stockBefore=Convert.ToInt64(rows[0][0]);
    if(await tx.Execute("UPDATE commerce.Inventory SET Stock=Stock-1 WHERE Dataset='kohami_local' AND Sku='sku_demo_03' AND Stock>=1")!=1)throw new InvalidOperationException("AC06_FIXTURE_STOCK_UNAVAILABLE");
    await tx.Execute("INSERT commerce.Reservations VALUES('kohami_local',@o,'sku_demo_03',1,'Held',@e)",("@o",orderId),("@e",expires));
    await tx.Save(new("Checkout",checkoutId,owner,1,checkout),true);await tx.Save(new("Order",orderId,owner,1,(JsonObject)checkout.DeepClone()),true);await tx.Save(new("PaymentAttempt",attemptId,owner,1,attempt),true);return stockBefore;
   });
   return(orderId,checkoutId,attemptId,owner,expires.ToUnixTimeMilliseconds(),seeded);
  }

  async Task<(string order,string payment,string reservation,long stock,int primary,int late,int compensations)> State((string orderId,string checkoutId,string attemptId,string owner,long expires,long stockBefore) item) => await store.Transaction(async tx=>{
   var checkout=await tx.Get("Checkout",item.checkoutId);var attempt=await tx.Get("PaymentAttempt",item.attemptId);var reservation=Convert.ToString(await tx.Scalar("SELECT State FROM commerce.Reservations WHERE Dataset='kohami_local' AND OrderId=@o",("@o",item.orderId)))??"Missing";var stock=Convert.ToInt64(await tx.Scalar("SELECT Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku='sku_demo_03'"));var primary=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o AND Kind='Primary'",("@o",item.orderId)));var late=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o AND Kind='Late'",("@o",item.orderId)));var compensation=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='RefundCase' AND Id=@id",("@id","comp_"+item.attemptId)));return(checkout.Body.Text("orderState"),attempt.Body.Text("state"),reservation,stock,primary,late,compensation);
  });

  async Task Cleanup((string orderId,string checkoutId,string attemptId,string owner,long expires,long stockBefore) item) {
   await using var c=new SqlConnection(adminConnectionString);await c.OpenAsync();await using var cmd=new SqlCommand(@"DELETE commerce.Inbox WHERE Dataset='kohami_local' AND EventId IN(@unknown,@terminal);
DELETE commerce.Jobs WHERE Dataset='kohami_local' AND Actor=@owner;
DELETE commerce.RefundUnits WHERE Dataset='kohami_local' AND OrderId=@order;
DELETE commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@order;
DELETE commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@owner;
DELETE commerce.Reservations WHERE Dataset='kohami_local' AND OrderId=@order;
DELETE commerce.Documents WHERE Dataset='kohami_local' AND Id IN(@checkout,@order,@attempt,@case,@receipt);
UPDATE commerce.Inventory SET Stock=@stock WHERE Dataset='kohami_local' AND Sku='sku_demo_03'",c);
   cmd.Parameters.AddWithValue("@unknown",item.attemptId+"_unknown");cmd.Parameters.AddWithValue("@terminal",item.attemptId+"_terminal");cmd.Parameters.AddWithValue("@owner",item.owner);cmd.Parameters.AddWithValue("@order",item.orderId);cmd.Parameters.AddWithValue("@checkout",item.checkoutId);cmd.Parameters.AddWithValue("@attempt",item.attemptId);cmd.Parameters.AddWithValue("@case","comp_"+item.attemptId);cmd.Parameters.AddWithValue("@receipt","DEMO-collection_"+item.attemptId);cmd.Parameters.AddWithValue("@stock",item.stockBefore);await cmd.ExecuteNonQueryAsync();
   await using var verify=new SqlCommand(@"SELECT
 (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Id IN(@checkout,@order,@attempt,@case,@receipt))+
 (SELECT COUNT(*) FROM commerce.Reservations WHERE Dataset='kohami_local' AND OrderId=@order)+
 (SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@order)+
 (SELECT COUNT(*) FROM commerce.Jobs WHERE Dataset='kohami_local' AND Actor=@owner)+
 (SELECT COUNT(*) FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@owner)+
 (SELECT COUNT(*) FROM commerce.Inbox WHERE Dataset='kohami_local' AND EventId IN(@unknown,@terminal)),
 (SELECT Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku='sku_demo_03')",c);
   foreach(SqlParameter parameter in cmd.Parameters)verify.Parameters.AddWithValue(parameter.ParameterName,parameter.Value);
   await using var result=await verify.ExecuteReaderAsync();await result.ReadAsync();Check(result.GetInt32(0)==0&&Convert.ToInt64(result.GetValue(1))==item.stockBefore,"payment deadline fixture removes all synthetic rows and restores its original SKU stock");
  }

  async Task Cancel((string orderId,string checkoutId,string attemptId,string owner,long expires,long stockBefore) item) {
   var order=await store.Transaction(tx=>tx.Get("Order",item.orderId));var etag=CommerceWire.Version("Order",item.orderId,order.Version);var context=new DefaultHttpContext();context.RequestServices=new MinimalJsonServices();context.Request.Method="POST";context.Request.ContentType="application/json";context.Request.Headers.IfMatch='"'+etag+'"';context.Request.Headers["Idempotency-Key"]=FoundationHttp.Id("ac06_cancel");context.Request.Headers["X-Fixture-Actor"]=item.owner;var browser=FoundationHttp.Id("ac06_browser");var browserValue=CommerceWire.Obj(("value",browser),("until",clock.GetUtcNow().AddHours(1).ToString("O"))).ToJsonString();context.Request.Headers.Cookie="kohami.browser="+protection.CreateProtector("kohami.browser").Protect(browserValue);context.Request.Body=new MemoryStream(Encoding.UTF8.GetBytes(CommerceWire.Obj(("expectedVersion",etag)).ToJsonString()));context.Response.Body=new MemoryStream();var result=await new CommerceService(store,new SessionCrypto(protection),serviceSecurity,clock).Cancel(context,item.orderId);await result.ExecuteAsync(context);
  }

  var before=await Seed("before");
  try {
   clock.SetUtcNow(DateTimeOffset.FromUnixTimeMilliseconds(before.expires).AddTicks(-TimeSpan.TicksPerSecond));await worker.RecordTrustedResult("PaymentAttempt",before.attemptId,"success");
   var state=await State(before);Check(state==("Confirmed","Succeeded","Consumed",before.stockBefore-1,1,0,0),"payment success one second before the 15-minute deadline consumes the reservation and confirms once");
  } finally {await Cleanup(before);}

  var equal=await Seed("equal");
  try {
   clock.SetUtcNow(DateTimeOffset.FromUnixTimeMilliseconds(equal.expires));await worker.RecordTrustedResult("PaymentAttempt",equal.attemptId,"success");var state=await State(equal);
   Check(state==("Expired","Succeeded","Released",equal.stockBefore,0,1,1),"payment success at the exact 15-minute deadline releases once and creates one late-payment compensation");
   await worker.RecordTrustedResult("PaymentAttempt",equal.attemptId,"success");state=await State(equal);Check(state==("Expired","Succeeded","Released",equal.stockBefore,0,1,1),"duplicate success at the deadline cannot duplicate collection, compensation, or stock release");
  } finally {await Cleanup(equal);}

  var cancelled=await Seed("cancel");
  try {
   clock.SetUtcNow(DateTimeOffset.FromUnixTimeMilliseconds(cancelled.expires).AddTicks(-TimeSpan.TicksPerSecond));await Cancel(cancelled);var afterCancel=await State(cancelled);Check(afterCancel==("Cancelled","Processing","Released",cancelled.stockBefore,0,0,0),"customer cancellation before the deadline releases its reservation once");
   clock.SetUtcNow(DateTimeOffset.FromUnixTimeMilliseconds(cancelled.expires).AddSeconds(1));await worker.RecordTrustedResult("PaymentAttempt",cancelled.attemptId,"success");var afterSuccess=await State(cancelled);Check(afterSuccess==("Cancelled","Succeeded","Released",cancelled.stockBefore,0,1,1),"success after cancellation is compensated without reviving the order or releasing stock twice");
  } finally {await Cleanup(cancelled);}

  var race=await Seed("race");
  try {
   clock.SetUtcNow(DateTimeOffset.FromUnixTimeMilliseconds(race.expires));using var ready=new CountdownEvent(2);var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
   var cancelTask=Task.Run(async()=>{ready.Signal();await gate.Task;try{await Cancel(race);return "Cancelled";}catch(KohamiException e) when((e.Status is 409 or 412)&&(e.Code is "INVALID_TRANSITION" or "VERSION_CHANGED")){return "Rejected";}});
   var paymentTask=Task.Run(async()=>{ready.Signal();await gate.Task;await worker.RecordTrustedResult("PaymentAttempt",race.attemptId,"success");});
   if(!ready.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("AC06_RACE_READY_TIMEOUT");gate.SetResult();var cancelOutcome=await cancelTask;await paymentTask;var final=await State(race);
   Check(cancelOutcome is "Cancelled" or "Rejected"&&final is ("Cancelled" or "Expired","Succeeded","Released",_,0,1,1)&&final.stock==race.stockBefore,"concurrent cancel and deadline success serialize to one terminal order with exactly one release, late collection, and compensation");
  } finally {await Cleanup(race);}
 }

 static void Check(bool pass,string name){if(!pass)throw new InvalidOperationException("FAILED: "+name);Console.WriteLine("PASS: "+name);}

 sealed class MutableTimeProvider(DateTimeOffset now):TimeProvider {
  long utcTicks=now.UtcTicks;
  public override DateTimeOffset GetUtcNow()=>new(Interlocked.Read(ref utcTicks),TimeSpan.Zero);
  public void SetUtcNow(DateTimeOffset value)=>Interlocked.Exchange(ref utcTicks,value.UtcTicks);
 }

 sealed class PaymentOwnerSecurity:ISecurity {
  public Task<Principal?> ResolveAsync(HttpContext context,CancellationToken ct=default){var id=context.Request.Headers["X-Fixture-Actor"].ToString();return Task.FromResult<Principal?>(id.Length==0?null:new Principal(id,"synthetic-ac06-session",1,"MemberAuthenticated",Array.Empty<string>()));}
  public Task<Principal> RequirePermissionAsync(HttpContext context,string permission,string resourceKind,string resourceId,CancellationToken ct=default)=>Task.FromException<Principal>(new KohamiException(403,"FORBIDDEN"));
  public Task<bool> CanDispatchAsync(string actorUserId,long securityVersion,string permission,string resourceKind,string resourceId,CancellationToken ct=default)=>Task.FromResult(false);
 }

 sealed class MinimalJsonServices:IServiceProvider {
  public object? GetService(Type serviceType)=>serviceType==typeof(Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>)?Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Http.Json.JsonOptions()):serviceType==typeof(Microsoft.Extensions.Logging.ILoggerFactory)?NullLoggerFactory.Instance:null;
 }
}
