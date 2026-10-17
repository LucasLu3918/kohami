using System.Text.Json.Nodes;
using Kohami.M0.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Kohami.M0.Commerce;
public static class CommerceLifecycle {
 public static async Task SaveOrderAndCheckout(CommerceTransaction tx,CommerceDocument checkout){await tx.Save(checkout);var order=await tx.Get("Order",checkout.Body.Text("orderId"));var b=checkout.Body.DeepClone().AsObject();b["id"]=order.Id;await tx.Save(order with {Version=order.Version+1,Body=b});}
 public static async Task<bool> Expire(CommerceTransaction tx,CommerceDocument checkout,DateTimeOffset now){if(checkout.Body.Text("orderState")!="AwaitingPayment"||DateTimeOffset.Parse(checkout.Body.Text("expiresAt"))>now)return false;await Release(tx,checkout,"Expired");checkout.Body["orderState"]="Expired";return true;}
 public static async Task Release(CommerceTransaction tx,CommerceDocument checkout,string state){var held=await tx.Rows("SELECT Sku,Quantity FROM m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@o AND State='Held'",("@o",checkout.Body.Text("orderId")));foreach(var row in held)await tx.Execute("UPDATE m0commerce.Inventory SET Stock=Stock+@q WHERE Dataset='demo_m0' AND Sku=@s",("@q",row[1]),("@s",row[0]));await tx.Execute("UPDATE m0commerce.Reservations SET State='Released' WHERE Dataset='demo_m0' AND OrderId=@o AND State='Held'",("@o",checkout.Body.Text("orderId")));checkout.Body["orderState"]=state;await SaveOrderAndCheckout(tx,checkout with {Version=checkout.Version+1});}
}
public sealed class MockWorker(CommerceStore store,TimeProvider clock,ILogger<MockWorker> logger):BackgroundService {
 readonly string instance=M0Http.Id("worker");
 DateOnly? lastRetentionPurgeUtcDate;
 DateTimeOffset? retentionRetryAfter;
 protected override async Task ExecuteAsync(CancellationToken stoppingToken){while(!stoppingToken.IsCancellationRequested){try{await PurgeExpiredLogsIfDue(stoppingToken);await DispatchOnce(stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}catch(Exception e){logger.LogError("Mock worker failed ({Type}); persistent lease enables retry",e.GetType().Name);}await Task.Delay(TimeSpan.FromMilliseconds(300),clock,stoppingToken);}}
 async Task PurgeExpiredLogsIfDue(CancellationToken ct){var now=clock.GetUtcNow();var date=DateOnly.FromDateTime(now.UtcDateTime);if(lastRetentionPurgeUtcDate==date||retentionRetryAfter>now)return;try{await PurgeExpiredLogs(now,ct);lastRetentionPurgeUtcDate=date;retentionRetryAfter=null;}catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}catch(Exception e){retentionRetryAfter=now.AddMinutes(5);logger.LogError("Retention cleanup failed ({Type}); business work continues and cleanup will retry",e.GetType().Name);}}
 public async Task PurgeExpiredLogs(DateTimeOffset? now=null,CancellationToken ct=default)=>await store.Transaction(async tx=>{var cutoff=now??clock.GetUtcNow();await tx.Execute("EXEC m0.security_purge_expired_logs @Now=@now",("@now",cutoff));await tx.Execute("EXEC m0commerce.purge_report_access_audit @Now=@now",("@now",cutoff));await tx.Execute("EXEC m0commerce.purge_expired_guest_proofs @Now=@now",("@now",cutoff));await PurgeExpiredAftercareDetails(tx,cutoff);return true;},ct,true,true);
 static async Task PurgeExpiredAftercareDetails(CommerceTransaction tx,DateTimeOffset now)
 {
  var shipments=await tx.Rows("SELECT Id,OwnerId,Version,Body FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Shipment' AND JSON_VALUE(Body,'$.retention.status') IS NULL AND JSON_VALUE(Body,'$.state') IN ('Delivered','Returned')");
  foreach(var row in shipments)
  {
   var document=new CommerceDocument("Shipment",(string)row[0],(string)row[1],(long)row[2],JsonNode.Parse((string)row[3])!.AsObject());
   if(AftercareRetention.TryRedactShipment(document,now,out var redacted))await tx.Save(redacted);
  }

  var returns=await tx.Rows("SELECT Id,OwnerId,Version,Body FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Return' AND JSON_VALUE(Body,'$.retention.status') IS NULL AND JSON_VALUE(Body,'$.state') IN ('Rejected','Inspected')");
  foreach(var row in returns)
  {
   var id=(string)row[0];
   var document=new CommerceDocument("Return",id,(string)row[1],(long)row[2],JsonNode.Parse((string)row[3])!.AsObject());
   if(document.Body.Text("state")=="Rejected")
   {
    var linked=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind IN ('RefundCase','InventoryMovement') AND JSON_VALUE(Body,'$.returnId')=@id",("@id",id)))>0;
    if(AftercareRetention.TryRedactRejectedReturn(document,now,linked,out var rejectedRedacted))await tx.Save(rejectedRedacted);
   }
   else if(await HasValidInspectedReturnClosure(tx,document)&&AftercareRetention.TryRedactInspectedReturn(document,now,true,out var inspectedRedacted))
    await tx.Save(inspectedRedacted);
  }
 }
 static async Task<bool> HasValidInspectedReturnClosure(CommerceTransaction tx,CommerceDocument document)
 {
  var closure=document.Body["closure"] as JsonObject;
  var inspection=document.Body["inspection"] as JsonObject;
  var caseId=JsonText(closure,"refundCaseId");
  var operationId=JsonText(closure,"refundOperationId");
  var movementId=JsonText(inspection,"inventoryMovementId");
  if(JsonText(closure,"status")!="Closed"||JsonText(closure,"basis")!="RefundSucceeded"||
     caseId!=JsonText(inspection,"refundCaseId")||string.IsNullOrWhiteSpace(caseId)||
     string.IsNullOrWhiteSpace(operationId)||string.IsNullOrWhiteSpace(movementId))return false;
  async Task<JsonObject?> ReadBody(string kind,string id)
  {
   var rows=await tx.Rows("SELECT Body FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind=@kind AND Id=@id",("@kind",kind),("@id",id));
   return rows.Count==1?JsonNode.Parse((string)rows[0][0])?.AsObject():null;
  }
  var refundCase=await ReadBody("RefundCase",caseId);
  var refundOperation=await ReadBody("RefundOperation",operationId);
  var inventoryMovement=await ReadBody("InventoryMovement",movementId);
  var orderId=JsonText(document.Body,"orderId");
  return !string.IsNullOrWhiteSpace(orderId)&&JsonText(refundCase,"returnId")==document.Id&&JsonText(refundCase,"orderId")==orderId&&
         JsonText(refundOperation,"caseId")==caseId&&JsonText(refundOperation,"state")=="Succeeded"&&JsonText(refundOperation,"orderId")==orderId&&
         JsonText(inventoryMovement,"returnId")==document.Id&&JsonText(inventoryMovement,"orderId")==orderId&&JsonText(inventoryMovement,"state")=="Applied";
 }
 static string? JsonText(JsonObject? body,string name)=>body?[name] is JsonValue value&&value.TryGetValue<string>(out var text)?text:null;
 public Task<int> DispatchOnce(CancellationToken ct=default)=>DispatchOnceCore(null,true,ct);
 // Native/offline test boundary: process only the specified synthetic job without running global maintenance.
 public Task<int> DispatchSpecificOnce(string jobId,CancellationToken ct=default)=>string.IsNullOrWhiteSpace(jobId)?Task.FromException<int>(new ArgumentException("A job id is required.",nameof(jobId))):DispatchOnceCore(jobId,false,ct);
 async Task<int> DispatchOnceCore(string? jobId,bool expireReservations,CancellationToken ct){if(expireReservations)await ExpireReservations(ct);var claim=await store.Transaction(async tx=>{
  var maintenance=Convert.ToBoolean(await tx.Scalar("SELECT Maintenance FROM m0.SecurityState WHERE Id=1"));
  var jobs=await tx.Rows("SELECT TOP (1) Id,Kind,TargetId,Actor,SecurityVersion,Permission,ResourceKind,ResourceId,State,Payload,Fence FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND State IN ('NotStarted','Started') AND (LeaseUntil IS NULL OR LeaseUntil<=@now) AND (State='Started' OR @maintenance=0) AND (@jobId IS NULL OR Id=@jobId) ORDER BY CreatedAt,Id",("@now",clock.GetUtcNow()),("@maintenance",maintenance),("@jobId",jobId));if(jobs.Count==0)return (JobClaim?)null;var j=jobs[0];var notStarted=(string)j[8]=="NotStarted";
  if(notStarted&&j[5] is string permission){await tx.Execute("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource='kohami.m0.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;IF @r<0 THROW 50001,'SECURITY_LOCK_TIMEOUT',1;");var allowed=await tx.Scalar("EXEC m0.security_dispatch_allowed @UserId=@u,@SecurityVersion=@v,@Permission=@p,@ResourceKind=@rk,@ResourceId=@ri,@DatasetId='demo_m0',@Now=@now",("@u",j[3]),("@v",j[4]),("@p",permission),("@rk",j[6]),("@ri",j[7]),("@now",clock.GetUtcNow()));if(!Convert.ToBoolean(allowed)){await tx.Execute("UPDATE m0commerce.Jobs SET State='Blocked' WHERE Dataset='demo_m0' AND Id=@id",("@id",j[0]));if((string)j[1]=="Refund")await FailUndispatchedRefund(tx,(string)j[2],clock.GetUtcNow());return null;}}
  var fence=(long)j[10]+1;await tx.Execute("UPDATE m0commerce.Jobs SET State='Started',LeaseOwner=@who,LeaseUntil=@until,Fence=@f WHERE Dataset='demo_m0' AND Id=@id",("@who",instance),("@until",clock.GetUtcNow().AddSeconds(30)),("@f",fence),("@id",j[0]));if((string)j[1]=="Refund"){var d=await tx.Get("RefundOperation",(string)j[2]);if(d.Body.Text("dispatchState")!="Started"){d.Body["dispatchState"]="Started";await tx.Save(d with {Version=d.Version+1});}}return new JobClaim((string)j[0],(string)j[1],(string)j[2],fence,JsonNode.Parse((string)j[9])!.AsObject());},ct,true,true);
  if(claim is null)return 0;
  await store.Transaction(async tx=>{var own=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Id=@id AND LeaseOwner=@who AND Fence=@f AND State='Started' AND LeaseUntil>@now",("@id",claim.Id),("@who",instance),("@f",claim.Fence),("@now",clock.GetUtcNow())));if(own!=1)return false;
   if(claim.Kind=="Scenario"){var targetKind=claim.Payload.Text("targetKind");var targetId=claim.Payload.Text("targetId");if(targetKind is "ReceiptAdjustment" or "DemoReceipt")await ReceiptResult(tx,targetKind,targetId,claim.Payload.Text("scenario"),clock.GetUtcNow());else await ApplyResult(tx,targetKind,targetId,claim.Payload.Text("scenario"),claim.Id,ct);}
   else if(claim.Kind is "ReceiptAdjustment" or "DemoReceipt")await ReceiptResult(tx,claim.Kind,claim.Target,claim.Payload.Text("outcome"),clock.GetUtcNow());
   else await ApplyResult(tx,claim.Kind=="Payment"?"PaymentAttempt":"RefundOperation",claim.Target,claim.Payload.Text("outcome"),claim.Id,ct);
   await tx.Execute("UPDATE m0commerce.Jobs SET State='Completed',LeaseOwner=NULL,LeaseUntil=NULL WHERE Dataset='demo_m0' AND Id=@id AND Fence=@f",("@id",claim.Id),("@f",claim.Fence));return true;},ct,true,true);return 1;
 }
 public async Task ExpireReservations(CancellationToken ct=default)=>await store.Transaction(async tx=>{if(Convert.ToBoolean(await tx.Scalar("SELECT Maintenance FROM m0.SecurityState WHERE Id=1")))return true;var rows=await tx.Rows("SELECT Id FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND JSON_VALUE(Body,'$.orderState')='AwaitingPayment' AND CAST(JSON_VALUE(Body,'$.expiresAt') AS datetimeoffset)<=@now",("@now",clock.GetUtcNow()));foreach(var x in rows)await CommerceLifecycle.Expire(tx,await tx.Get("Checkout",(string)x[0]),clock.GetUtcNow());return true;},ct,true,true);
 // Offline test/native boundary only: no route exposes this method or permits a browser-supplied trusted result.
 public Task RecordTrustedResult(string targetKind,string targetId,string outcome,CancellationToken ct=default)=>store.Transaction(async tx=>{if(targetKind is not ("PaymentAttempt" or "RefundOperation"))throw new M0Exception(422,"VALIDATION_ERROR");var eventOutcome=outcome is "duplicate" or "late_success"?"success":outcome;var suffix=eventOutcome=="unknown"?"unknown":eventOutcome=="amount_mismatch"?"invalid":"terminal";await ApplyResult(tx,targetKind,targetId,eventOutcome,targetId+"_"+suffix,ct);return true;},ct,true);
 public Task RecordMockMoneyEvent(MockMoneyEvent paymentEvent,CancellationToken ct=default)=>store.Transaction(async tx=>{await ApplyMockMoneyEvent(tx,"PaymentAttempt",paymentEvent.OperationId,paymentEvent,ct);return true;},ct,true);
 async Task ApplyResult(CommerceTransaction tx,string kind,string target,string outcome,string eventId,CancellationToken ct){
  _ = ct;
  if(outcome is not ("success" or "declined" or "unknown" or "duplicate" or "late_success" or "amount_mismatch"))throw new M0Exception(422,"VALIDATION_ERROR");
  var operation=await tx.Get(kind,target);
  var orderId=kind=="PaymentAttempt"?(await tx.Get("Checkout",operation.Body.Text("checkoutId"))).Body.Text("orderId"):operation.Body.Text("orderId");
  var amount=operation.Body.Number("amountTwd")+(outcome=="amount_mismatch"?1:0);
  var currency=operation.Body["currency"] is JsonValue value?value.GetValue<string>():"TWD";
  await ApplyMockMoneyEvent(tx,kind,target,new MockMoneyEvent(eventId,"mock",CommerceStore.Dataset,orderId,target,amount,currency,1,outcome),ct);
 }
 async Task ApplyMockMoneyEvent(CommerceTransaction tx,string kind,string target,MockMoneyEvent providerEvent,CancellationToken ct){
  _ = ct;
  if(kind is not ("PaymentAttempt" or "RefundOperation")||string.IsNullOrWhiteSpace(providerEvent.EventId)||providerEvent.EventId.Length>100||providerEvent.Outcome is not ("success" or "declined" or "unknown" or "duplicate" or "late_success" or "amount_mismatch"))throw new M0Exception(422,"VALIDATION_ERROR");
  var operation=await tx.Get(kind,target);
  var orderId=kind=="PaymentAttempt"?(await tx.Get("Checkout",operation.Body.Text("checkoutId"))).Body.Text("orderId"):operation.Body.Text("orderId");
  var expectedAmount=operation.Body.Number("amountTwd");var expectedCurrency=operation.Body["currency"] is JsonValue currency?currency.GetValue<string>():"TWD";
  var reason=providerEvent.Mode!="mock"?"MODE_MISMATCH":providerEvent.Dataset!=CommerceStore.Dataset?"DATASET_MISMATCH":providerEvent.OperationId!=target?"OPERATION_MISMATCH":providerEvent.OrderId!=orderId?"ORDER_MISMATCH":providerEvent.ContractVersion!=1?"UNSUPPORTED_EVENT_VERSION":providerEvent.AmountTwd!=expectedAmount?"AMOUNT_MISMATCH":providerEvent.Currency!=expectedCurrency?"CURRENCY_MISMATCH":providerEvent.Outcome=="amount_mismatch"?"AMOUNT_MISMATCH":null;
  var normalized=providerEvent.Outcome is "duplicate" or "late_success"?"success":providerEvent.Outcome;
  var body=CommerceWire.Obj(("targetKind",kind),("targetId",target),("eventId",providerEvent.EventId),("mode",providerEvent.Mode),("dataset",providerEvent.Dataset),("orderId",providerEvent.OrderId),("operationId",providerEvent.OperationId),("amountTwd",providerEvent.AmountTwd),("currency",providerEvent.Currency),("contractVersion",providerEvent.ContractVersion),("outcome",providerEvent.Outcome),("normalizedOutcome",normalized),("validationState",reason is null?"Accepted":"Quarantined"),("quarantineReason",reason));
  var hash=M0Http.Hash(body.ToJsonString());var old=await tx.Rows("SELECT PayloadHash FROM m0commerce.Inbox WHERE Dataset='demo_m0' AND EventId=@id",("@id",providerEvent.EventId));
  if(old.Count>0){if((string)old[0][0]!=hash)throw new M0Exception(409,"KEY_CONFLICT");return;}
  await tx.Execute("INSERT m0commerce.Inbox(Dataset,EventId,SourceId,PayloadHash,Body,Applied) VALUES('demo_m0',@i,@s,@h,@b,0)",("@i",providerEvent.EventId),("@s",providerEvent.EventId),("@h",hash),("@b",body.ToJsonString()));
  if(reason is not null)return; // Quarantined events are retained for review but cannot mutate money.
  var current=operation.Body.Text("state");if(current is "Succeeded" or "Failed"){await tx.Execute("UPDATE m0commerce.Inbox SET Applied=1 WHERE Dataset='demo_m0' AND EventId=@id",("@id",providerEvent.EventId));return;}
  if(kind=="PaymentAttempt")await PaymentResult(tx,operation,normalized);else {if(operation.Body.Text("dispatchState")!="Started")throw new M0Exception(409,"INVALID_TRANSITION");await RefundResult(tx,operation,normalized,clock.GetUtcNow());}
  await tx.Execute("UPDATE m0commerce.Inbox SET Applied=1 WHERE Dataset='demo_m0' AND EventId=@id",("@id",providerEvent.EventId));
 }
 async Task PaymentResult(CommerceTransaction tx,CommerceDocument attempt,string outcome){var checkout=await tx.Get("Checkout",attempt.Body.Text("checkoutId"));await CommerceLifecycle.Expire(tx,checkout,clock.GetUtcNow());checkout=await tx.Get("Checkout",checkout.Id);var state=outcome=="success"?"Succeeded":outcome=="declined"?"Failed":"Unknown";attempt.Body["state"]=state;await tx.Save(attempt with {Version=attempt.Version+1});checkout.Body["paymentState"]=state;
  if(state=="Succeeded"){var kind=checkout.Body.Text("orderState")=="AwaitingPayment"?"Primary":"Late";var existing=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o AND Kind='Primary'",("@o",checkout.Body.Text("orderId"))));if(existing>0)kind="Extra";var collection="collection_"+attempt.Id;var total=attempt.Body.Number("amountTwd");var snap=checkout.Body["snapshot"]!.AsObject();await tx.Execute("INSERT m0commerce.Collections(Dataset,Id,OrderId,Kind,Collected,ShippingPaid) VALUES('demo_m0',@id,@o,@k,@a,@s)",("@id",collection),("@o",checkout.Body.Text("orderId")),("@k",kind),("@a",total),("@s",kind=="Primary"?snap.Number("shippingTwd"):0));
   if(kind=="Primary"){checkout.Body["paymentConfirmedAt"]=clock.GetUtcNow().ToString("O");await tx.Execute("UPDATE m0commerce.Reservations SET State='Consumed' WHERE Dataset='demo_m0' AND OrderId=@o AND State='Held'",("@o",checkout.Body.Text("orderId")));checkout.Body["orderState"]="Confirmed";foreach(var l in snap["lines"]!.AsArray()){var line=l!.AsObject();foreach(var u in line["units"]!.AsArray()){var unit=u!.AsObject();await tx.Execute("INSERT m0commerce.RefundUnits(Dataset,UnitId,OrderId,LineId,CollectionId,Ordinal,Paid) VALUES('demo_m0',@u,@o,@l,@c,@n,@a)",("@u",unit.Text("unitId")),("@o",checkout.Body.Text("orderId")),("@l",line.Text("orderLineId")),("@c",collection),("@n",(int)unit.Number("ordinal")),("@a",unit.Number("paidTwd")));}}}
   else {checkout.Body["compensationRequired"]=true;var caseId="comp_"+attempt.Id;var b=CommerceWire.Obj(("id",caseId),("orderId",checkout.Body.Text("orderId")),("basis","CollectionCompensation"),("kind","PaymentCompensation"),("state","Requested"),("sourceCollectionId",collection),("shippingEligible",false));await tx.Save(new("RefundCase",caseId,checkout.Owner,1,b),true);}
   var receiptId="DEMO-"+collection;var receipt=CommerceWire.Obj(("id",receiptId),("orderId",checkout.Body.Text("orderId")),("collectionId",collection),("kind","Receipt"),("mode","mock"),("state","Processing"),("amountTwd",total),("currency","TWD"),("taxEffect","none"),("displayNotice","DEMO 憑證，無稅務效力"),("createdAt",clock.GetUtcNow()));await tx.Save(new("DemoReceipt",receiptId,checkout.Owner,1,receipt),true);await tx.Job("DemoReceipt",receiptId,checkout.Owner,0,null,"Order",checkout.Body.Text("orderId"),CommerceWire.Obj(("outcome","success")),clock.GetUtcNow());}
  await CommerceLifecycle.SaveOrderAndCheckout(tx,checkout with {Version=checkout.Version+1});
 }
 static async Task RefundResult(CommerceTransaction tx,CommerceDocument d,string outcome,DateTimeOffset now){var state=outcome=="success"?"Succeeded":outcome=="declined"?"Failed":"Unknown";d.Body["state"]=state;foreach(var unit in d.Body["units"]!.AsArray())unit!.AsObject()["slot"]=state=="Failed"?"Free":state;
  var amount=d.Body.Number("amountTwd");var source=d.Body.Text("sourceCollectionId");var refundCase=await tx.Get("RefundCase",d.Body.Text("caseId"));var returnedUnitSlot=state=="Failed"&&refundCase.Body["returnId"] is JsonValue?"Processing":state=="Failed"?"Free":state;if(state is "Succeeded" or "Failed")await tx.Execute("UPDATE m0commerce.Collections SET Pending=Pending-@a,Refunded=Refunded+@paid,ShippingSlot=CASE WHEN @ship>0 THEN @slot ELSE ShippingSlot END WHERE Dataset='demo_m0' AND Id=@s",("@a",amount),("@paid",state=="Succeeded"?amount:0),("@ship",d.Body.Number("shippingTwd")),("@slot",state=="Failed"?"Free":state),("@s",source));else if(d.Body.Number("shippingTwd")>0)await tx.Execute("UPDATE m0commerce.Collections SET ShippingSlot='Unknown' WHERE Dataset='demo_m0' AND Id=@s",("@s",source));await tx.Execute("UPDATE m0commerce.RefundUnits SET Slot=@slot,OperationId=CASE WHEN @slot='Free' THEN NULL WHEN @slot='Processing' AND @return IS NOT NULL THEN @return ELSE OperationId END WHERE Dataset='demo_m0' AND OperationId=@op",("@slot",returnedUnitSlot),("@return",refundCase.Body["returnId"]?.GetValue<string>()),("@op",d.Id));
  if(state=="Succeeded"){var adjustmentId="DEMO-ADJ-"+d.Id;if(await tx.Find("ReceiptAdjustment",adjustmentId) is null){var adjustment=CommerceWire.Obj(("id",adjustmentId),("orderId",d.Body.Text("orderId")),("refundOperationId",d.Id),("sourceCollectionId",source),("kind","Adjustment"),("mode","mock"),("state","Processing"),("amountTwd",amount),("taxEffect","none"),("displayNotice","DEMO 退款調整，無稅務效力"),("createdAt",now));await tx.Save(new("ReceiptAdjustment",adjustmentId,d.Owner,1,adjustment),true);await tx.Job("ReceiptAdjustment",adjustmentId,d.Owner,0,null,"Order",d.Body.Text("orderId"),CommerceWire.Obj(("outcome","success")),now);}}
  await tx.Save(d with {Version=d.Version+1});if(state=="Failed"){refundCase.Body["state"]="Requested";await tx.Save(refundCase with {Version=refundCase.Version+1});}else if(state=="Succeeded")await RecordInspectedReturnClosure(tx,d,refundCase,now);var order=await tx.Get("Order",d.Body.Text("orderId"));await tx.Save(order with {Version=order.Version+1});}
 static async Task RecordInspectedReturnClosure(CommerceTransaction tx,CommerceDocument operation,CommerceDocument refundCase,DateTimeOffset now)
 {
  var returnId=JsonText(refundCase.Body,"returnId");
  if(string.IsNullOrWhiteSpace(returnId)||JsonText(refundCase.Body,"orderId")!=JsonText(operation.Body,"orderId")||
     JsonText(operation.Body,"caseId")!=refundCase.Id||JsonText(operation.Body,"state")!="Succeeded")return;
  var returns=await tx.Rows("SELECT OwnerId,Version,Body FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Return' AND Id=@id",("@id",returnId));
  if(returns.Count!=1)return;
  var inspected=new CommerceDocument("Return",returnId,(string)returns[0][0],(long)returns[0][1],JsonNode.Parse((string)returns[0][2])!.AsObject());
  var movementId=JsonText(inspected.Body["inspection"] as JsonObject,"inventoryMovementId");
  if(string.IsNullOrWhiteSpace(movementId))return;
  var movements=await tx.Rows("SELECT Body FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='InventoryMovement' AND Id=@id",("@id",movementId));
  if(movements.Count!=1)return;
  var movement=JsonNode.Parse((string)movements[0][0])!.AsObject();
  if(JsonText(movement,"returnId")!=returnId||JsonText(movement,"orderId")!=JsonText(operation.Body,"orderId")||JsonText(movement,"state")!="Applied")return;
  if(AftercareRetention.TryMarkInspectedReturnClosed(inspected,refundCase.Id,operation.Id,now,out var closed))await tx.Save(closed);
 }
 static async Task ReceiptResult(CommerceTransaction tx,string kind,string id,string outcome,DateTimeOffset now){if(outcome is not ("success" or "declined" or "unknown" or "duplicate"))throw new M0Exception(422,"VALIDATION_ERROR");var d=await tx.Get(kind,id);var success=kind=="DemoReceipt"?"Issued":"Adjusted";if(d.Body.Text("state")==success)return;d.Body["state"]=outcome switch{"success" or "duplicate"=>success,"declined"=>"Failed",_=>"Unknown"};d.Body["lastAttemptAt"]=now;await tx.Save(d with{Version=d.Version+1});}
 static async Task FailUndispatchedRefund(CommerceTransaction tx,string id,DateTimeOffset now){var d=await tx.Get("RefundOperation",id);if(d.Body.Text("dispatchState")=="Started")return;await RefundResult(tx,d,"declined",now);}
 sealed record JobClaim(string Id,string Kind,string Target,long Fence,JsonObject Payload);
}
