using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kohami.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace Kohami.Commerce;
public sealed partial class CommerceService {
 public async Task<IResult> Orders(HttpContext h){
  var principal=await security.ResolveAsync(h,h.RequestAborted);
  if(principal is null||principal.Stage!="MemberAuthenticated")throw new KohamiException(401,"AUTHENTICATION_REQUIRED");
  var items=await store.Transaction(async tx=>{
   var rows=await tx.Rows("SELECT Id,Version,OwnerId,Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order' AND OwnerId=@user ORDER BY TRY_CONVERT(datetimeoffset,JSON_VALUE(Body,'$.placedAt')) DESC,Id DESC",("@user",principal.UserId));
   var result=new List<JsonObject>();
   foreach(var row in rows){var doc=new CommerceDocument("Order",(string)row[0],(string)row[2],(long)row[1],JsonNode.Parse((string)row[3])!.AsObject());result.Add(await OrderPublic(tx,doc));}
   return result;
  },h.RequestAborted);
  return FoundationHttp.Json(h,CommerceWire.Obj(("items",items)));
 }
 public async Task<IResult> Order(HttpContext h,string id){var d=await store.Transaction(tx=>tx.Get("Order",id),h.RequestAborted);var principal=await security.ResolveAsync(h,h.RequestAborted);if(principal?.Stage=="AdminAuthenticated")await security.RequirePermissionAsync(h,"orders.read","Order",id,ct:h.RequestAborted);else await Owner(h,d);return await store.Transaction(async tx=>DocumentResult(h,d,await OrderPublic(tx,d)),h.RequestAborted);}
 static async Task<JsonObject> OrderPublic(CommerceTransaction tx,CommerceDocument d){var rows=await tx.Rows("SELECT Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='RefundOperation' AND JSON_VALUE(Body,'$.orderId')=@o",("@o",d.Id));var ops=rows.Select(x=>JsonNode.Parse((string)x[0])!.AsObject()).ToArray();var totals=await tx.Rows("SELECT COALESCE(SUM(Collected),0),COALESCE(SUM(Pending),0),COALESCE(SUM(Refunded),0) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o",("@o",d.Id));
  var summary=CommerceWire.Obj(("requestedCount",0),("processingCount",ops.Count(x=>x.Text("state")=="Processing")),("unknownCount",ops.Count(x=>x.Text("state")=="Unknown")),("succeededCount",ops.Count(x=>x.Text("state")=="Succeeded")),("failedCount",ops.Count(x=>x.Text("state")=="Failed")),("succeededTwd",(long)totals[0][2]),("pendingTwd",(long)totals[0][1]));
  var shipmentRows=await tx.Rows("SELECT Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Shipment' AND JSON_VALUE(Body,'$.orderId')=@o",("@o",d.Id));
  var returnRows=await tx.Rows("SELECT Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Return' AND JSON_VALUE(Body,'$.orderId')=@o",("@o",d.Id));
  var receiptRows=await tx.Rows("SELECT Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='DemoReceipt' AND JSON_VALUE(Body,'$.orderId')=@o",("@o",d.Id));
  var adjustmentRows=await tx.Rows("SELECT Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='ReceiptAdjustment' AND JSON_VALUE(Body,'$.orderId')=@o",("@o",d.Id));
  var shipments=shipmentRows.Select(x=>JsonNode.Parse((string)x[0])!.AsObject()).ToArray();var returns=returnRows.Select(x=>JsonNode.Parse((string)x[0])!.AsObject()).ToArray();var receipts=receiptRows.Select(x=>JsonNode.Parse((string)x[0])!.AsObject()).ToArray();
  var adjustments=adjustmentRows.Select(x=>JsonNode.Parse((string)x[0])!.AsObject()).ToArray();
  var confirmedAt=UtcInstant(d.Body["paymentConfirmedAt"]);
  var publicShipments=shipments.Select(shipment=>PublicShipment(shipment,confirmedAt)).ToArray();
  var receiptState=receipts.Length==0?"NotCreated":receipts.Last().Text("state")=="Issued"?"Confirmed":receipts.Last().Text("state");
  var publicReturns=returns.Select(x=>{
   var state=x.Text("state");var quantity=(int)x.Number("quantity");var unitIds=(x["unitIds"] as JsonArray??[]).Select(unit=>unit!.GetValue<string>()).ToArray();var inspection=x["inspection"] as JsonObject;var restockCount=state=="Inspected"?(int)(inspection?.Number("resellableQuantity")??0):0;
   var received=state is "Received" or "Inspected"?unitIds:Array.Empty<string>();var restocked=state=="Inspected"?unitIds.Take(restockCount).ToArray():Array.Empty<string>();var quarantined=state=="Inspected"?unitIds.Skip(restockCount).ToArray():Array.Empty<string>();var accepted=state is "Accepted" or "Received" or "Inspected"?quantity:0;
   var history=(x["history"] as JsonArray??[]).OfType<JsonObject>().ToArray();
   DateTimeOffset? FirstAt(params string[] states)=>history.Where(item=>states.Contains(item.Text("state"),StringComparer.Ordinal)).Select(item=>UtcInstant(item["at"])).FirstOrDefault(item=>item is not null);
   var openedAt=UtcInstant(x["createdAt"]);var firstResponseAt=FirstAt("Accepted","Rejected","Escalated");var escalatedAt=FirstAt("Escalated");
   var timeline=history.Select(item=>{
    var eventState=item.Text("state");var publicEvent=CommerceWire.Obj(("state",eventState),("at",UtcInstant(item["at"])?.ToString("O")));
    if(eventState=="Rejected"&&item["reason"] is JsonValue reason&&reason.TryGetValue<string>(out var message))publicEvent["message"]=message;
    return publicEvent;
   }).ToArray();
   var inspectionSummary=state=="Inspected"&&inspection is not null?CommerceWire.Obj(("resellableQuantity",(int)inspection.Number("resellableQuantity")),("quarantinedQuantity",(int)inspection.Number("quarantinedQuantity"))):null;
   var caseSummary=CommerceWire.Obj(("classification","Return"),("openedAt",openedAt?.ToString("O")),("firstResponseAt",firstResponseAt?.ToString("O")),("firstResponseElapsedSeconds",ElapsedSeconds(openedAt,firstResponseAt)),("escalatedAt",escalatedAt?.ToString("O")),("escalationElapsedSeconds",ElapsedSeconds(openedAt,escalatedAt)));
   return CommerceWire.Obj(("id",x.Text("id")),("mode","mock"),("version",x.Text("version")),("state",state),("caseSummary",caseSummary),("timeline",timeline),("inspectionSummary",inspectionSummary),("lines",new[]{CommerceWire.Obj(("orderLineId",x.Text("orderLineId")),("requestedQuantity",quantity),("acceptedQuantity",accepted),("receivedUnitIds",received),("restockedUnitIds",restocked),("quarantinedUnitIds",quarantined))}));
  }).ToArray();
  return CommerceWire.Obj(("id",d.Id),("mode","mock"),("version",CommerceWire.Version(d.Kind,d.Id,d.Version)),("placedAt",d.Body.Text("placedAt")),("orderState",d.Body.Text("orderState")),("snapshot",d.Body["snapshot"]!.DeepClone()),("paymentSummary",CommerceWire.Obj(("state",d.Body.Text("paymentState")),("confirmedAt",confirmedAt?.ToString("O")),("confirmedCollectedTwd",(long)totals[0][0]),("compensationRequired",d.Body["compensationRequired"]!.DeepClone()))),("shipmentSummary",CommerceWire.Obj(("state",shipments.LastOrDefault()?.Text("state")??"NotCreated"),("shipments",publicShipments))),("refundSummary",summary),("receiptSummary",CommerceWire.Obj(("state",receiptState),("pendingAdjustmentCount",adjustments.Count(x=>x.Text("state") is "Processing" or "Unknown")))),("maskedContact","***@example.test"),("returns",new JsonArray(publicReturns.Select(x=>(JsonNode?)x).ToArray())),("actions",new JsonArray()));
 }
 static DateTimeOffset? UtcInstant(JsonNode? node){if(node is not JsonValue value)return null;if(value.TryGetValue<DateTimeOffset>(out var instant))return instant.ToUniversalTime();if(value.TryGetValue<string>(out var text)&&DateTimeOffset.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out instant))return instant.ToUniversalTime();return null;}
 static long? ElapsedSeconds(DateTimeOffset? start,DateTimeOffset? end){if(start is null||end is null||end<start)return null;return (long)(end.Value-start.Value).TotalSeconds;}
 static JsonObject PublicShipment(JsonObject shipment,DateTimeOffset? paymentConfirmedAt){
  var history=(shipment["history"] as JsonArray??[]).OfType<JsonObject>().ToArray();
  var publicHistory=history.Select(item=>CommerceWire.Obj(("state",item.Text("state")),("at",UtcInstant(item["at"])?.ToString("O")))).ToArray();
  var createdAt=UtcInstant(shipment["createdAt"]);
  DateTimeOffset? FirstAt(string state)=>history.Where(item=>item.Text("state")==state).Select(item=>UtcInstant(item["at"])).FirstOrDefault(item=>item is not null);
  var shippedAt=FirstAt("Shipped");var deliveredAt=FirstAt("Delivered");
  return CommerceWire.Obj(("id",shipment.Text("id")),("mode","mock"),("version",shipment.Text("version")),("state",shipment.Text("state")),("createdAt",createdAt?.ToString("O")),("history",publicHistory),
   ("timing",CommerceWire.Obj(("paymentToShipmentSeconds",ElapsedSeconds(paymentConfirmedAt,createdAt)),("shipmentToShippedSeconds",ElapsedSeconds(createdAt,shippedAt)),("shippedToDeliveredSeconds",ElapsedSeconds(shippedAt,deliveredAt)))));
 }
 public async Task<IResult> Cancel(HttpContext h,string id){var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["expectedVersion"],"reason");var old=await store.Transaction(tx=>tx.Get("Order",id),h.RequestAborted);await Owner(h,old,"cancel");var browser=Browser(h);var cap=ReadCookie(h,"kohami.guest.action");var principal=await security.ResolveAsync(h,h.RequestAborted);var member=principal is not null&&principal.Stage=="MemberAuthenticated"&&old.Owner==principal.UserId;
  var key=FoundationHttp.Key(h);var hash=FoundationHttp.Hash(CommerceWire.RequestHash(b)+"\0"+h.Request.Headers.IfMatch.ToString());var r=await store.Transaction(async tx=>{if(!member){var rows=await tx.Rows("SELECT Consumed,OperationKey FROM commerce.GuestCapabilities WHERE Dataset='kohami_local' AND Id=@id AND OrderId=@o AND BrowserId=@b AND Intent='cancel' AND ExpiresAt>@now",("@id",cap),("@o",id),("@b",browser),("@now",clock.GetUtcNow()));if(rows.Count!=1||((bool)rows[0][0]&&rows[0][1] as string!=key))throw new KohamiException(404,"RESOURCE_NOT_FOUND");}
   var replay=await tx.Replay(member?principal!.UserId:browser,"cancel/"+id,key,hash);if(replay is not null)return replay;var d=await tx.Get("Order",id);CommerceWire.Expected(h,d,b);if(d.Body.Text("orderState")!="AwaitingPayment")throw new KohamiException(409,"INVALID_TRANSITION");var checkout=await tx.Get("Checkout",d.Body.Text("checkoutId"));await CommerceLifecycle.Release(tx,checkout,"Cancelled");var updatedOrder=await tx.Get("Order",id);if(!member)await tx.Execute("UPDATE commerce.GuestCapabilities SET Consumed=1,OperationKey=@key WHERE Dataset='kohami_local' AND Id=@id",("@key",key),("@id",cap));var reply=new CommerceReply(CommerceWire.Obj(("id",id),("operationId",FoundationHttp.Id("cancel")),("mode","mock"),("state","Cancelled"),("version",CommerceWire.Version("Order",id,updatedOrder.Version)),("reasonCodes",Array.Empty<string>())),202,updatedOrder.Version);await tx.Record(member?principal!.UserId:browser,"cancel/"+id,key,hash,reply);return reply;},h.RequestAborted);return Result(h,r);
 }
 public async Task<IResult> GuestRequest(HttpContext h){var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["orderId","email","intent"]);var intent=b.Text("intent");if(intent is not ("read" or "cancel" or "return"))throw new KohamiException(422,"VALIDATION_ERROR");var email=b.Text("email");if(!email.EndsWith("@example.test",StringComparison.Ordinal)||email.Length>254)throw new KohamiException(422,"VALIDATION_ERROR");var browser=Browser(h,true);var request=FoundationHttp.Id("verify");var nonce=FoundationHttp.Id("nonce");var proof=FoundationHttp.Id("proof");await store.Transaction(async tx=>{var count=Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM commerce.GuestRequests WHERE Dataset='kohami_local' AND BrowserId=@b AND ExpiresAt>@now",("@b",browser),("@now",clock.GetUtcNow())));if(count>=5)throw new KohamiException(429,"RATE_LIMITED");var order=await tx.Find("Order",b.Text("orderId"));var matched=order is not null&&order.Owner.StartsWith("browser_",StringComparison.Ordinal)&&order.Body.Text("email")==email;await tx.Execute("INSERT commerce.GuestRequests VALUES('kohami_local',@id,@o,@b,@i,@n,@p,@e,0,@m)",("@id",request),("@o",b.Text("orderId")),("@b",browser),("@i",intent),("@n",FoundationHttp.Hash(nonce)),("@p",FoundationHttp.Hash(proof)),("@e",clock.GetUtcNow().AddMinutes(10)),("@m",matched));if(matched)await tx.Execute("INSERT commerce.PrivateDeliveries VALUES('kohami_local',@id,@r,@p)",("@id",FoundationHttp.Id("delivery")),("@r",request),("@p",crypto.Sessions.CreateProtector("kohami.mock.private-delivery").Protect(proof)));return true;},h.RequestAborted);return FoundationHttp.Json(h,CommerceWire.Obj(("state","Accepted"),("requestId",request),("requestNonce",nonce)),202);}
 public async Task<IResult> GuestConfirm(HttpContext h){var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["requestId","requestNonce","proof","intent"]);var browser=Browser(h);var result=await store.Transaction(async tx=>{var rows=await tx.Rows("SELECT OrderId,Intent,NonceHash,ProofHash,ExpiresAt,Used,Matched,BrowserId FROM commerce.GuestRequests WHERE Dataset='kohami_local' AND Id=@id",("@id",b.Text("requestId")));if(rows.Count!=1)throw new KohamiException(404,"RESOURCE_NOT_FOUND");var x=rows[0];if((string)x[1] is not ("read" or "cancel" or "return")||(bool)x[5]||!(bool)x[6]||(string)x[7]!=browser||(DateTimeOffset)x[4]<=clock.GetUtcNow()||(string)x[1]!=b.Text("intent")||(string)x[2]!=FoundationHttp.Hash(b.Text("requestNonce"))||(string)x[3]!=FoundationHttp.Hash(b.Text("proof")))throw new KohamiException(404,"RESOURCE_NOT_FOUND");await tx.Execute("UPDATE commerce.GuestRequests SET Used=1 WHERE Dataset='kohami_local' AND Id=@id",("@id",b.Text("requestId")));var cap=FoundationHttp.Id("cap");var expires=clock.GetUtcNow().AddMinutes((string)x[1]=="read"?30:10);await tx.Execute("INSERT commerce.GuestCapabilities(Dataset,Id,OrderId,BrowserId,Intent,ExpiresAt,Consumed) VALUES('kohami_local',@id,@o,@b,@i,@e,0)",("@id",cap),("@o",x[0]),("@b",browser),("@i",x[1]),("@e",expires));return (cap,order:(string)x[0],intent:(string)x[1],expires);},h.RequestAborted);SetCookie(h,result.intent=="read"?"kohami.guest.read":"kohami.guest.action",result.cap,result.expires);return FoundationHttp.Json(h,CommerceWire.Obj(("state","Verified"),("scope",result.intent=="read"?"guest.read":"guest.action."+result.intent),("resourceId",result.order),("expiresAt",result.expires)));}
 public async Task<IResult> ReadRefund(HttpContext h,string id){await security.RequirePermissionAsync(h,"orders.read","RefundOperation",id,ct:h.RequestAborted);var d=await store.Transaction(tx=>tx.Get("RefundOperation",id),h.RequestAborted);return DocumentResult(h,d,RefundPublic(d.Body));}
 static JsonObject RefundPublic(JsonObject b){var result=b.DeepClone().AsObject();result.Remove("orderId");result.Remove("actor");return result;}
 public async Task<IResult> Refund(HttpContext h,string id,string basis){var p=await security.RequirePermissionAsync(h,"refund.execute","RefundCase",id,ct:h.RequestAborted);var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);if(basis=="Merchandise")CommerceWire.Fields(b,["lines","refundShipping","amountTwd","reason","expectedVersion"]);else{CommerceWire.Fields(b,["expectedVersion","reason","confirmed"]);if(b["confirmed"]?.GetValue<bool>()!=true)throw new KohamiException(422,"VALIDATION_ERROR");}if(b.Text("reason").Length is <1 or >500)throw new KohamiException(422,"VALIDATION_ERROR");var r=await Write(h,p.UserId,"refund/"+id,b,async tx=>{var d=await tx.Get("RefundCase",id);CommerceWire.Expected(h,d,b);if(d.Body.Text("basis")!=basis||d.Body.Text("state")!="Requested")throw new KohamiException(409,"INVALID_TRANSITION");var source=d.Body.Text("sourceCollectionId");var rows=await tx.Rows("SELECT Collected,Pending,Refunded,Kind,ShippingPaid,ShippingSlot,PaymentMethod FROM commerce.Collections WHERE Dataset='kohami_local' AND Id=@s",("@s",source));if(rows.Count!=1)throw new KohamiException(404,"RESOURCE_NOT_FOUND");var s=rows[0];if(string.Equals((string)s[6],"Wallet",StringComparison.Ordinal))throw new KohamiException(409,"WALLET_REFUND_UNSUPPORTED");var available=(long)s[0]-(long)s[1]-(long)s[2];long amount=0,shipping=0;var units=new JsonArray();var op=FoundationHttp.Id("refund");
   if(basis=="Merchandise"){if((string)s[3]!="Primary")throw new KohamiException(409,"INVALID_TRANSITION");var requested=b["lines"]!.AsArray();if(requested.Count==0||requested.Select(x=>x!.AsObject().Text("orderLineId")).Distinct().Count()!=requested.Count)throw new KohamiException(422,"VALIDATION_ERROR");foreach(var line in requested){var l=line!.AsObject();CommerceWire.Fields(l,["orderLineId","quantity"]);var qty=l.Number("quantity");if(qty is <1 or >100)throw new KohamiException(422,"VALIDATION_ERROR");if(d.Body["eligibleUnitIds"] is not JsonArray allowed)throw new KohamiException(409,"INVALID_TRANSITION");var eligible=await tx.Rows("SELECT TOP (@q) UnitId,Ordinal,Paid FROM commerce.RefundUnits WHERE Dataset='kohami_local' AND OrderId=@o AND CollectionId=@s AND LineId=@l AND UnitId IN (SELECT [value] FROM OPENJSON(@eligible)) AND (Slot='Free' OR (Slot='Processing' AND OperationId=@return)) ORDER BY Ordinal",("@q",(int)qty),("@o",d.Body.Text("orderId")),("@s",source),("@l",l.Text("orderLineId")),("@eligible",d.Body["eligibleUnitIds"]!.ToJsonString()),("@return",d.Body["returnId"]?.GetValue<string>()));if(eligible.Count!=qty)throw new KohamiException(409,"INVALID_TRANSITION");foreach(var u in eligible){if(!allowed.Any(x=>x?.GetValue<string>()==(string)u[0]))throw new KohamiException(409,"INVALID_TRANSITION");amount=checked(amount+(long)u[2]);units.Add(CommerceWire.Obj(("unitId",u[0]),("orderLineId",l.Text("orderLineId")),("ordinal",u[1]),("paidTwd",u[2]),("slot","Processing")));var changed=await tx.Execute("UPDATE commerce.RefundUnits SET Slot='Processing',OperationId=@op WHERE Dataset='kohami_local' AND UnitId=@u AND (Slot='Free' OR (Slot='Processing' AND OperationId=@return))",("@op",op),("@u",u[0]),("@return",d.Body["returnId"]?.GetValue<string>()));if(changed!=1)throw new KohamiException(409,"INVALID_TRANSITION");}}if(b["refundShipping"]!.GetValue<bool>()){if(d.Body["shippingEligible"]?.GetValue<bool>()!=true||(string)s[5]!="Free")throw new KohamiException(409,"SHIPPING_NOT_ELIGIBLE");shipping=(long)s[4];}amount=checked(amount+shipping);if(amount!=b.Number("amountTwd"))throw new KohamiException(409,"AMOUNT_MISMATCH");}
   else if(basis=="ShippingOnly"){if((string)s[3]!="Primary"||d.Body["shippingEligible"]?.GetValue<bool>()!=true||(string)s[5]!="Free")throw new KohamiException(409,"SHIPPING_NOT_ELIGIBLE");amount=shipping=(long)s[4];}
   else {if((string)s[3]=="Primary")throw new KohamiException(409,"INVALID_TRANSITION");amount=available;}
   if(amount<=0||amount>available)throw new KohamiException(409,"AMOUNT_MISMATCH");await tx.Execute("UPDATE commerce.Collections SET Pending=Pending+@a,ShippingSlot=CASE WHEN @ship>0 THEN 'Processing' ELSE ShippingSlot END WHERE Dataset='kohami_local' AND Id=@s",("@a",amount),("@ship",shipping),("@s",source));var body=CommerceWire.Obj(("id",op),("caseId",id),("mode","mock"),("state","Processing"),("amountTwd",amount),("version",CommerceWire.Version("RefundOperation",op,1)),("displayNotice","模擬退款，無實際資金移動"),("sourceCollectionId",source),("kind",basis=="CollectionCompensation"?"PaymentCompensation":"OrderRefund"),("units",units),("shippingTwd",shipping),("dispatchState","NotStarted"));var internalBody=body.DeepClone().AsObject();internalBody["orderId"]=d.Body.Text("orderId");await tx.Save(new("RefundOperation",op,p.UserId,1,internalBody),true);d.Body["state"]="Submitted";await tx.Save(d with {Version=d.Version+1});var order=await tx.Get("Order",d.Body.Text("orderId"));await tx.Save(order with {Version=order.Version+1});await tx.Job("Refund",op,p.UserId,p.SecurityVersion,"refund.execute","RefundCase",id,CommerceWire.Obj(("outcome","unknown")),clock.GetUtcNow());return new(body,202,1);});return Result(h,r);}
 public async Task<IResult> CheckRefund(HttpContext h,string id){var d=await store.Transaction(tx=>tx.Get("RefundOperation",id),h.RequestAborted);var p=await security.RequirePermissionAsync(h,"refund.check","Case",d.Body.Text("caseId"),ct:h.RequestAborted);var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["expectedVersion","reason"]);var r=await Write(h,p.UserId,"checkRefund/"+id,b,async tx=>{var current=await tx.Get("RefundOperation",id);CommerceWire.Expected(h,current,b); // Query the original source only; never queues a second refund.
   return new(CommerceWire.Obj(("id",FoundationHttp.Id("check")),("mode","mock"),("state","Processing"),("resourceUrl","/api/v1/admin/refund-operations/"+id)),202);});return Result(h,r);}
 public async Task<IResult> Scenario(HttpContext h){var p=await security.RequirePermissionAsync(h,"demo.control","Dataset",CommerceStore.Dataset,ct:h.RequestAborted);var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["targetKind","targetId","scenario","expectedVersion"]);var kind=b.Text("targetKind");if(kind is not ("PaymentAttempt" or "RefundOperation" or "ReceiptAdjustment" or "DemoReceipt"))throw new KohamiException(422,"VALIDATION_ERROR");var outcome=b.Text("scenario");if(outcome is not ("success" or "declined" or "unknown" or "duplicate" or "late_success" or "amount_mismatch")||((kind is "ReceiptAdjustment" or "DemoReceipt")&&outcome is not ("success" or "declined" or "unknown" or "duplicate")))throw new KohamiException(422,"VALIDATION_ERROR");var r=await Write(h,p.UserId,"scenario/"+b.Text("targetId"),b,async tx=>{var target=await tx.Get(kind,b.Text("targetId"));CommerceWire.Expected(h,target,b);var id=FoundationHttp.Id("scenario");await tx.Job("Scenario",id,p.UserId,p.SecurityVersion,"demo.control","Dataset",CommerceStore.Dataset,(JsonObject)b.DeepClone(),clock.GetUtcNow());return new(CommerceWire.Obj(("id",id),("mode","mock"),("state","Processing"),("resourceUrl",kind=="RefundOperation"?"/api/v1/admin/refund-operations/"+target.Id:(kind is "ReceiptAdjustment" or "DemoReceipt")?"/api/v1/orders/"+target.Body.Text("orderId")+"/receipts":"/api/v1/checkouts/"+target.Body.Text("checkoutId")+"/status")),202);});return Result(h,r);}

 static async Task RequireCurrentGrant(CommerceTransaction tx,Principal p,string permission,string kind,string id,DateTimeOffset now){var allowed=await tx.Scalar("EXEC identity_access.security_dispatch_allowed @UserId=@u,@SecurityVersion=@v,@Permission=@p,@ResourceKind=@rk,@ResourceId=@ri,@DatasetId='kohami_local',@Now=@now",("@u",p.UserId),("@v",p.SecurityVersion),("@p",permission),("@rk",kind),("@ri",id),("@now",now));if(!Convert.ToBoolean(allowed))throw new KohamiException(403,"PERMISSION_CHANGED");}
 public async Task<IResult> Products(HttpContext h){var rows=await store.Transaction(tx=>tx.Rows("SELECT Sku,Price,Stock,Active FROM commerce.Inventory WHERE Dataset='kohami_local' AND Active=1 ORDER BY Sku"),h.RequestAborted);var items=rows.Select(x=>CommerceWire.Obj(("skuId",x[0]),("priceTwd",x[1]),("availability",(int)x[2]>0?"InStock":"OutOfStock"),("mode","mock"))).ToArray();return FoundationHttp.Json(h,CommerceWire.Obj(("mode","mock"),("items",items)));}
 static JsonArray History(JsonObject body){if(body["history"] is JsonArray history)return history;history=[];body["history"]=history;return history;}
 public async Task<IResult> CreateShipment(HttpContext h,string id){var p=await security.RequirePermissionAsync(h,"shipment.manage","Order",id,ct:h.RequestAborted);var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["expectedVersion"]);var r=await Write(h,p.UserId,"shipment/create/"+id,b,async tx=>{await RequireCurrentGrant(tx,p,"shipment.manage","Order",id,clock.GetUtcNow());var order=await tx.Get("Order",id);CommerceWire.Expected(h,order,b);if(order.Body.Text("orderState")!="Confirmed"||order.Body.Text("paymentState")!="Succeeded"||Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o AND Kind='Primary' AND Collected>0",("@o",id)))!=1)throw new KohamiException(409,"INVALID_TRANSITION");if(Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Shipment' AND JSON_VALUE(Body,'$.orderId')=@o",("@o",id)))!=0)throw new KohamiException(409,"INVALID_TRANSITION");var shipId=FoundationHttp.Id("shipment");var now=clock.GetUtcNow();var body=CommerceWire.Obj(("id",shipId),("orderId",id),("mode","mock"),("state","Pending"),("version",CommerceWire.Version("Shipment",shipId,1)),("createdAt",now),("history",new JsonArray(CommerceWire.Obj(("state","Pending"),("at",now),("actor",p.UserId),("eventId",FoundationHttp.Id("event"))))),("displayNotice","模擬配送，沒有真實包裹或承運追蹤"));await tx.Save(new("Shipment",shipId,p.UserId,1,body),true);await tx.Save(order with{Version=order.Version+1});return new(body,201,1);});return Result(h,r);}
 public async Task<IResult> TransitionShipment(HttpContext h,string id){var shipment=await store.Transaction(tx=>tx.Get("Shipment",id),h.RequestAborted);var orderId=shipment.Body.Text("orderId");var p=await security.RequirePermissionAsync(h,"shipment.manage","Order",orderId,ct:h.RequestAborted);var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["state","eventId","expectedVersion"],"reason");var next=b.Text("state");var r=await Write(h,p.UserId,"shipment/transition/"+id,b,async tx=>{await RequireCurrentGrant(tx,p,"shipment.manage","Order",orderId,clock.GetUtcNow());var d=await tx.Get("Shipment",id);var history=History(d.Body);var previous=history.OfType<JsonObject>().FirstOrDefault(x=>x.Text("eventId")==b.Text("eventId"));if(previous is not null){if(previous.Text("state")!=next||(previous["reason"]?.GetValue<string>()??"")!=(b["reason"]?.GetValue<string>()??""))throw new KohamiException(409,"KEY_CONFLICT");return new(d.Body,200,d.Version);}CommerceWire.Expected(h,d,b);var old=d.Body.Text("state");var allowed=(old,next) switch {("Pending","Shipped") or ("Shipped","InTransit") or ("InTransit","Delivered") or ("InTransit","Exception") or ("Exception","InTransit") or ("Exception","Returned")=>true,_=>false};if(!allowed)throw new KohamiException(409,"INVALID_TRANSITION");if((next is "Exception" or "Returned")&&b.Text("reason").Length is <1 or >500)throw new KohamiException(422,"VALIDATION_ERROR");var now=clock.GetUtcNow();d.Body["state"]=next;d.Body["history"]!.AsArray().Add(CommerceWire.Obj(("state",next),("at",now),("actor",p.UserId),("eventId",b.Text("eventId")),("reason",b["reason"]?.DeepClone())));await tx.Save(d with{Version=d.Version+1});var order=await tx.Get("Order",orderId);await tx.Save(order with{Version=order.Version+1});return new(d.Body,200,d.Version+1);});return Result(h,r);}
 public async Task<IResult> RequestReturn(HttpContext h,string id){var order=await store.Transaction(tx=>tx.Get("Order",id),h.RequestAborted);await Owner(h,order,"return");var capabilityBrowser=Browser(h);var capabilityId=ReadCookie(h,"kohami.guest.action");var principal=await security.ResolveAsync(h,h.RequestAborted);var member=principal is not null&&principal.Stage=="MemberAuthenticated"&&order.Owner==principal.UserId;var actor=member?principal!.UserId:order.Owner;var operationKey=FoundationHttp.Key(h);var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["orderLineId","quantity","reason"]);if(b.Text("reason").Length is <1 or >500||b.Number("quantity") is <1 or >100)throw new KohamiException(422,"VALIDATION_ERROR");var r=await Write(h,actor,"return/request/"+id,b,async tx=>{var current=await tx.Get("Order",id);if(!member){var capability=await tx.Rows("SELECT Consumed,OperationKey FROM commerce.GuestCapabilities WHERE Dataset='kohami_local' AND Id=@id AND OrderId=@o AND BrowserId=@b AND Intent='return' AND ExpiresAt>@now",("@id",capabilityId),("@o",id),("@b",capabilityBrowser),("@now",clock.GetUtcNow()));if(capability.Count!=1||((bool)capability[0][0]&&capability[0][1] as string!=operationKey))throw new KohamiException(404,"RESOURCE_NOT_FOUND");}if(current.Body.Text("orderState")!="Confirmed")throw new KohamiException(409,"INVALID_TRANSITION");var rows=await tx.Rows("SELECT Id,Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Shipment' AND JSON_VALUE(Body,'$.orderId')=@o",("@o",id));if(!rows.Any(x=>JsonNode.Parse((string)x[1])!.AsObject().Text("state")=="Delivered"))throw new KohamiException(409,"INVALID_TRANSITION");var line=current.Body["snapshot"]!["lines"]!.AsArray().OfType<JsonObject>().SingleOrDefault(x=>x.Text("orderLineId")==b.Text("orderLineId"))??throw new KohamiException(404,"RESOURCE_NOT_FOUND");var units=await tx.Rows("SELECT UnitId FROM commerce.RefundUnits WHERE Dataset='kohami_local' AND OrderId=@o AND LineId=@l AND Slot='Free' ORDER BY Ordinal",("@o",id),("@l",b.Text("orderLineId")));var q=(int)b.Number("quantity");if(units.Count<q)throw new KohamiException(409,"INVALID_TRANSITION");var unitIds=units.Take(q).Select(x=>(string)x[0]).ToArray();var existing=await tx.Rows("SELECT Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Return' AND JSON_VALUE(Body,'$.orderId')=@o",("@o",id));var claimed=existing.Where(x=>JsonNode.Parse((string)x[0])!.AsObject().Text("state")!="Rejected").SelectMany(x=>(JsonNode.Parse((string)x[0])!.AsObject()["unitIds"] as JsonArray??[]).Select(n=>n!.GetValue<string>())).ToHashSet(StringComparer.Ordinal);if(unitIds.Any(claimed.Contains))throw new KohamiException(409,"INVALID_TRANSITION");var returnId=FoundationHttp.Id("return");foreach(var unitId in unitIds){var changed=await tx.Execute("UPDATE commerce.RefundUnits SET Slot='Processing',OperationId=@r WHERE Dataset='kohami_local' AND UnitId=@u AND Slot='Free'",("@r",returnId),("@u",unitId));if(changed!=1)throw new KohamiException(409,"INVALID_TRANSITION");}var now=clock.GetUtcNow();var body=CommerceWire.Obj(("id",returnId),("orderId",id),("orderLineId",line.Text("orderLineId")),("skuId",line.Text("skuId")),("quantity",q),("unitIds",unitIds),("reason",b.Text("reason")),("mode","mock"),("state","Requested"),("version",CommerceWire.Version("Return",returnId,1)),("createdAt",now),("history",new JsonArray(CommerceWire.Obj(("state","Requested"),("at",now),("actor",actor)))),("inspection",null));await tx.Save(new("Return",returnId,current.Owner,1,body),true);await tx.Save(current with{Version=current.Version+1});if(!member)await tx.Execute("UPDATE commerce.GuestCapabilities SET Consumed=1,OperationKey=@key WHERE Dataset='kohami_local' AND Id=@id AND Consumed=0",("@key",operationKey),("@id",capabilityId));return new(body,201,1);});return Result(h,r);}
 public async Task<IResult> TransitionReturn(HttpContext h,string id){var ret=await store.Transaction(tx=>tx.Get("Return",id),h.RequestAborted);var orderId=ret.Body.Text("orderId");var p=await security.RequirePermissionAsync(h,"support.respond","Order",orderId,ct:h.RequestAborted);var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["state","expectedVersion"],"reason");var state=b.Text("state");if(state is not ("Accepted" or "Rejected" or "Received" or "Escalated")||(state is "Rejected" or "Escalated")&&b.Text("reason").Length is <1 or >500)throw new KohamiException(422,"VALIDATION_ERROR");var r=await Write(h,p.UserId,"return/transition/"+id,b,async tx=>{await RequireCurrentGrant(tx,p,"support.respond","Order",orderId,clock.GetUtcNow());var d=await tx.Get("Return",id);CommerceWire.Expected(h,d,b);var old=d.Body.Text("state");if(!((old=="Requested"&&state is "Accepted" or "Rejected" or "Escalated")||(old=="Escalated"&&state is "Accepted" or "Rejected")||(old=="Accepted"&&state=="Received")))throw new KohamiException(409,"INVALID_TRANSITION");if(state=="Rejected")await tx.Execute("UPDATE commerce.RefundUnits SET Slot='Free',OperationId=NULL WHERE Dataset='kohami_local' AND Slot='Processing' AND OperationId=@r",("@r",id));d.Body["state"]=state;History(d.Body).Add(CommerceWire.Obj(("state",state),("at",clock.GetUtcNow()),("actor",p.UserId),("reason",b["reason"]?.DeepClone())));await tx.Save(d with{Version=d.Version+1});var order=await tx.Get("Order",orderId);await tx.Save(order with{Version=order.Version+1});return new(d.Body,200,d.Version+1);});return Result(h,r);}
 public async Task<IResult> InspectReturn(HttpContext h,string id){var ret=await store.Transaction(tx=>tx.Get("Return",id),h.RequestAborted);var orderId=ret.Body.Text("orderId");var p=await security.RequirePermissionAsync(h,"return.inspect","Order",orderId,ct:h.RequestAborted);var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["resellableQuantity","reason","expectedVersion"]);if(b.Text("reason").Length is <1 or >500)throw new KohamiException(422,"VALIDATION_ERROR");var q=b.Number("resellableQuantity");var r=await Write(h,p.UserId,"return/inspect/"+id,b,async tx=>{await RequireCurrentGrant(tx,p,"return.inspect","Order",orderId,clock.GetUtcNow());var d=await tx.Get("Return",id);CommerceWire.Expected(h,d,b);if(d.Body.Text("state")!="Received"||q<0||q>d.Body.Number("quantity"))throw new KohamiException(409,"INVALID_TRANSITION");var order=await tx.Get("Order",orderId);var collectionRows=await tx.Rows("SELECT Id FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o AND Kind='Primary'",("@o",orderId));if(collectionRows.Count!=1)throw new KohamiException(409,"INVALID_TRANSITION");var source=(string)collectionRows[0][0];var units=(d.Body["unitIds"] as JsonArray??[]).Select(x=>x!.GetValue<string>()).Take((int)q).ToArray();foreach(var _ in units){var updated=await tx.Execute("UPDATE commerce.Inventory SET Stock=Stock+1 WHERE Dataset='kohami_local' AND Sku=@s AND Stock<2147483647",("@s",d.Body.Text("skuId")));if(updated!=1)throw new KohamiException(409,"INVALID_TRANSITION");}var movement=FoundationHttp.Id("movement");await tx.Save(new("InventoryMovement",movement,p.UserId,1,CommerceWire.Obj(("id",movement),("returnId",id),("orderId",orderId),("skuId",d.Body.Text("skuId")),("unitIds",units),("quantity",units.Length),("mode","mock"),("state","Applied"),("createdAt",clock.GetUtcNow()))),true);var caseId=FoundationHttp.Id("refundcase");var refundUnits=await tx.Rows("SELECT UnitId,Ordinal,Paid FROM commerce.RefundUnits WHERE Dataset='kohami_local' AND UnitId IN (SELECT [value] FROM OPENJSON(@units))",("@units",d.Body["unitIds"]!.ToJsonString()));if(refundUnits.Count!=d.Body.Number("quantity"))throw new KohamiException(409,"INVALID_TRANSITION");var caseBody=CommerceWire.Obj(("id",caseId),("orderId",orderId),("returnId",id),("basis","Merchandise"),("kind","OrderRefund"),("state","Requested"),("sourceCollectionId",source),("shippingEligible",false),("eligibleUnitIds",d.Body["unitIds"]!.DeepClone()));await tx.Save(new("RefundCase",caseId,d.Owner,1,caseBody),true);d.Body["state"]="Inspected";d.Body["inspection"]=CommerceWire.Obj(("resellableQuantity",units.Length),("quarantinedQuantity",d.Body.Number("quantity")-units.Length),("reason",b.Text("reason")),("refundCaseId",caseId),("refundCaseVersion",CommerceWire.Version("RefundCase",caseId,1)),("refundUnits",refundUnits.OrderBy(x=>Convert.ToInt32(x[1])).Select(x=>CommerceWire.Obj(("unitId",x[0]),("ordinal",x[1]),("paidTwd",x[2]))).ToArray()),("inventoryMovementId",movement));History(d.Body).Add(CommerceWire.Obj(("state","Inspected"),("at",clock.GetUtcNow()),("actor",p.UserId)));await tx.Save(d with{Version=d.Version+1});await tx.Save(order with{Version=order.Version+1});return new(d.Body,200,d.Version+1);});return Result(h,r);}
 public async Task<IResult> AggregateReport(HttpContext h,string? from,string? to)
 {
  Principal? principal;
  try { principal=await security.ResolveAsync(h,h.RequestAborted); }
  catch(KohamiException error) when(error.Status is 401 or 403)
  {
   await RecordDeniedReportAccess(h,null,ReportDateRange(from,to));
   throw;
  }
  Principal authorized;
  try { authorized=await security.RequirePermissionAsync(h,"reports.read","Report","operations",ct:h.RequestAborted); }
  catch(KohamiException error) when(error.Status is 401 or 403)
  {
   await RecordDeniedReportAccess(h,principal?.UserId,ReportDateRange(from,to));
   throw;
  }
  var range=ReportDateRange(from,to);
  await RecordReportAccess(h,authorized.UserId,"Authorized",range);
  if(!DateOnly.TryParseExact(from,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var start)||
     !DateOnly.TryParseExact(to,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var end)||
     end<start||end.DayNumber-start.DayNumber>30)throw new KohamiException(422,"VALIDATION_ERROR");
  var startUtc=new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc));
  var endUtc=new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc));
  var rows=await store.Transaction(tx=>tx.Rows("""
   WITH OrderCollections AS (
    SELECT c.OrderId,c.Kind,SUM(c.Collected) AS CollectedTwd,
      SUM(c.Refunded) AS SucceededRefundTwd,SUM(c.Pending) AS PendingTwd
    FROM commerce.Collections c WHERE c.Dataset='kohami_local' GROUP BY c.OrderId,c.Kind
   ), PlacedOrders AS (
    SELECT d.Id,d.Body,TRY_CONVERT(datetimeoffset(7),JSON_VALUE(d.Body,'$.placedAt')) AS PlacedAt
    FROM commerce.Documents d WHERE d.Dataset='kohami_local' AND d.Kind='Order'
   )
   SELECT CONVERT(char(10),CONVERT(date,SWITCHOFFSET(o.PlacedAt,'+00:00')),23) AS ReportDate,
    c.Kind,COUNT(DISTINCT o.Id) AS OrderCount,SUM(c.CollectedTwd) AS CollectedTwd,
    SUM(c.SucceededRefundTwd) AS SucceededRefundTwd,SUM(c.PendingTwd) AS PendingTwd,
    SUM(c.CollectedTwd-c.SucceededRefundTwd-c.PendingTwd) AS AvailableTwd,
    SUM(c.CollectedTwd-c.SucceededRefundTwd) AS NetConfirmedTwd,
    COUNT(DISTINCT CASE WHEN JSON_VALUE(o.Body,'$.compensationRequired')='true' THEN o.Id END) AS DiscrepancyCount
   FROM PlacedOrders o JOIN OrderCollections c ON c.OrderId=o.Id
   WHERE o.PlacedAt>=@start AND o.PlacedAt<@end
   GROUP BY CONVERT(char(10),CONVERT(date,SWITCHOFFSET(o.PlacedAt,'+00:00')),23),c.Kind
   ORDER BY ReportDate,c.Kind
   """,("@start",startUtc),("@end",endUtc)),h.RequestAborted);
  var aggregates=rows.Select(row=>CommerceWire.Obj(("date",row[0]),("sourceKind",row[1]),("orderCount",row[2]),
   ("collectedTwd",row[3]),("succeededRefundTwd",row[4]),("pendingTwd",row[5]),("availableTwd",row[6]),
   ("netConfirmedTwd",row[7]),("discrepancyCount",row[8]))).ToArray();
  h.Response.Headers.CacheControl="no-store";
  return FoundationHttp.Json(h,CommerceWire.Obj(("mode","mock"),("cutoff",clock.GetUtcNow()),("rows",aggregates),("notice","合成聚合示意，非正式財務報表")));
 }

 async Task RecordDeniedReportAccess(HttpContext h,string? actor,(DateOnly? From,DateOnly? To) range)
 {
  try { await RecordReportAccess(h,actor,"Denied",range); }
  catch(Exception auditError) when(auditError is not OperationCanceledException)
  { logger?.LogError("Denied report access audit persistence failed ({Type})",auditError.GetType().Name); }
 }

 static (DateOnly? From,DateOnly? To) ReportDateRange(string? from,string? to)
 {
  if(DateOnly.TryParseExact(from,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var start)&&
     DateOnly.TryParseExact(to,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var end)&&
     end>=start&&end.DayNumber-start.DayNumber<=30)return(start,end);
  return(null,null);
 }
 async Task RecordReportAccess(HttpContext h,string? actor,string outcome,(DateOnly? From,DateOnly? To) range)
 {
  await store.Transaction(tx=>tx.Execute("INSERT commerce.ReportAccessAudit(Dataset,ActorUserId,RequestedAt,AuthorizationOutcome,FromDate,ToDate) VALUES('kohami_local',@actor,@at,@outcome,@from,@to)",
   ("@actor",actor),("@at",clock.GetUtcNow()),("@outcome",outcome),
   ("@from",range.From?.ToDateTime(TimeOnly.MinValue)),("@to",range.To?.ToDateTime(TimeOnly.MinValue))),h.RequestAborted);
 }

 public async Task<IResult> ReconciliationReport(HttpContext h)
 {
  await security.RequirePermissionAsync(h,"reconciliation.read","Dataset",CommerceStore.Dataset,ct:h.RequestAborted);
  static string? OptionalText(JsonObject value,string field)=>value[field] is JsonValue node&&node.TryGetValue<string>(out var text)?text:null;
  var report=await store.Transaction(async tx=>
  {
   var collectionRows=await tx.Rows("SELECT Id,OrderId,Kind,Collected,Pending,Refunded FROM commerce.Collections WHERE Dataset='kohami_local' ORDER BY Id");
   var operationRows=await tx.Rows("SELECT Id,Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='RefundOperation'");
   var receiptRows=await tx.Rows("SELECT Id,Kind,Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind IN ('DemoReceipt','ReceiptAdjustment')");
   var orderRows=await tx.Rows("SELECT Id,Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order'");
   var operations=operationRows.Select(x=>JsonNode.Parse((string)x[1])!.AsObject()).ToArray();
   var orders=orderRows.Select(x=>new{Id=(string)x[0],Body=JsonNode.Parse((string)x[1])!.AsObject()}).ToArray();
   var collections=collectionRows.Select(x=>new{Id=(string)x[0],OrderId=(string)x[1],Kind=(string)x[2],Collected=(long)x[3],Pending=(long)x[4],Refunded=(long)x[5]}).ToArray();
   var collectionIds=collections.Select(x=>x.Id).ToHashSet(StringComparer.Ordinal);
   var orderIds=orders.Select(x=>x.Id).ToHashSet(StringComparer.Ordinal);
   var findings=new List<JsonObject>();
   long collected=0,pending=0,refunded=0,available=0,primary=0,lateOrExtra=0;
   foreach(var c in collections)
   {
    collected=checked(collected+c.Collected);pending=checked(pending+c.Pending);refunded=checked(refunded+c.Refunded);available=checked(available+c.Collected-c.Pending-c.Refunded);
    if(c.Kind=="Primary")primary=checked(primary+c.Collected);
    else
    {
     lateOrExtra=checked(lateOrExtra+c.Collected);
     findings.Add(CommerceWire.Obj(("code","NON_PRIMARY_COLLECTION_REQUIRES_REVIEW"),("sourceId",c.Id),("kind",c.Kind),("collectedTwd",c.Collected)));
    }
    if(!orderIds.Contains(c.OrderId))findings.Add(CommerceWire.Obj(("code","COLLECTION_ORDER_MISSING"),("sourceId",c.Id)));
    var sourceOps=operations.Where(x=>x.Text("sourceCollectionId")==c.Id).ToArray();
    var expectedPending=sourceOps.Where(x=>x.Text("state") is "Processing" or "Unknown").Sum(x=>x.Number("amountTwd"));
    var expectedRefunded=sourceOps.Where(x=>x.Text("state")=="Succeeded").Sum(x=>x.Number("amountTwd"));
    if(expectedPending!=c.Pending)findings.Add(CommerceWire.Obj(("code","REFUND_PENDING_MISMATCH"),("sourceId",c.Id),("ledgerTwd",c.Pending),("operationsTwd",expectedPending)));
    if(expectedRefunded!=c.Refunded)findings.Add(CommerceWire.Obj(("code","REFUND_SETTLED_MISMATCH"),("sourceId",c.Id),("ledgerTwd",c.Refunded),("operationsTwd",expectedRefunded)));
    foreach(var op in sourceOps)
    {
     var units=op["units"] as JsonArray??[];var itemAmount=units.Sum(x=>x?.AsObject().Number("paidTwd")??0);var calculated=checked(itemAmount+op.Number("shippingTwd"));
     if(units.Count>0&&calculated!=op.Number("amountTwd"))findings.Add(CommerceWire.Obj(("code","REFUND_AMOUNT_MISMATCH"),("sourceId",c.Id),("operationId",op.Text("id"))));
    }
   }
   foreach(var op in operations.Where(x=>!collectionIds.Contains(x.Text("sourceCollectionId"))))findings.Add(CommerceWire.Obj(("code","REFUND_SOURCE_MISSING"),("operationId",op.Text("id"))));
   var compensationOrders=orders.Where(x=>CompensationRequired(x.Body)).ToArray();
   foreach(var order in compensationOrders)
    if(!collections.Any(x=>x.OrderId==order.Id&&x.Kind is "Late" or "Extra"))findings.Add(CommerceWire.Obj(("code","COMPENSATION_COLLECTION_MISSING"),("orderId",order.Id)));
   var reconciliationCases=await tx.Rows("SELECT Id,Version,Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='ReconciliationCase'");
   var caseById=reconciliationCases.ToDictionary(x=>(string)x[0],x=>new{Version=(long)x[1],Body=JsonNode.Parse((string)x[2])!.AsObject()},StringComparer.Ordinal);
   foreach(var finding in findings)
   {
    var caseId=ReconciliationCaseId(finding);caseById.TryGetValue(caseId,out var existing);
    finding["caseId"]=caseId;
    finding["disposition"]=existing?.Body.Text("state")=="Closed"?"Reopened":existing?.Body.Text("state")??"Open";
    finding["responsibleActor"]=existing?.Body.Text("responsibleActor");
    finding["assignedActor"]=existing?.Body.Text("assignedActor");
    finding["version"]=CommerceWire.Version("ReconciliationCase",caseId,existing?.Version??0);
   }
   var receiptExceptions=receiptRows.Select(x=>JsonNode.Parse((string)x[2])!.AsObject()).Count(x=>x.Text("state") is "Failed" or "Unknown" or "Processing");
   var activeFindingIds=findings.Select(ReconciliationCaseId).ToHashSet(StringComparer.Ordinal);
   var readyToClose=reconciliationCases
    .Select(x=>new{Id=(string)x[0],Version=(long)x[1],Body=JsonNode.Parse((string)x[2])!.AsObject()})
    .Where(x=>(x.Body.Text("state") is "CheckRequested" or "CompensationLinked")&&!activeFindingIds.Contains(x.Id))
    .Select(x=>CommerceWire.Obj(("caseId",x.Id),("code",x.Body.Text("code")),("sourceId",OptionalText(x.Body,"sourceId")),("orderId",OptionalText(x.Body,"orderId")),("operationId",OptionalText(x.Body,"operationId")),("compensationCaseId",OptionalText(x.Body,"compensationCaseId")),("disposition",x.Body.Text("state")),("responsibleActor",OptionalText(x.Body,"responsibleActor")),("assignedActor",OptionalText(x.Body,"assignedActor")),("version",CommerceWire.Version("ReconciliationCase",x.Id,x.Version))))
    .ToArray();
   return CommerceWire.Obj(("mode","mock"),("readOnly",true),("scheduleStatus","ManualOnly"),("coverage","Current kohami_local ledger snapshot; no bank, provider or tax authority reconciliation"),("cutoffAt",clock.GetUtcNow()),("state",findings.Count==0?"NoDifferences":"DifferencesFound"),("summary",CommerceWire.Obj(("collectionCount",collections.Length),("primaryCollectedTwd",primary),("lateOrExtraCollectionCount",collections.Count(x=>x.Kind is "Late" or "Extra")),("lateOrExtraCollectedTwd",lateOrExtra),("collectedTwd",collected),("refundPendingTwd",pending),("refundSucceededTwd",refunded),("availableMockTwd",available),("confirmedOrderCount",orders.Count(x=>x.Body.Text("orderState")=="Confirmed")),("compensationRequiredOrderCount",compensationOrders.Length),("refundOperationCount",operations.Length),("receiptExceptionCount",receiptExceptions))),("findings",new JsonArray(findings.Select(x=>(JsonNode?)x).ToArray())),("readyToClose",new JsonArray(readyToClose.Select(x=>(JsonNode?)x).ToArray())));
  },h.RequestAborted);
  return FoundationHttp.Json(h,report);
 }
 public async Task<IResult> ReconciliationNotifications(HttpContext h)
 {
  var p=await security.ResolveAsync(h,h.RequestAborted);
  if(p is null||p.Stage!="AdminAuthenticated")throw new KohamiException(401,"AUTHENTICATION_REQUIRED");
  var notifications=await store.Transaction(async tx=>
  {
   var rows=await tx.Rows("SELECT TOP(50) Id,Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='ReconciliationNotification' AND OwnerId=@user ORDER BY TRY_CONVERT(datetimeoffset,JSON_VALUE(Body,'$.createdAt')) DESC,Id DESC",("@user",p.UserId));
   var result=new List<JsonObject>();
   foreach(var row in rows)
   {
    var body=JsonNode.Parse((string)row[1])!.AsObject();
    try{await RequireCurrentGrant(tx,p,"reconciliation.resolve","Case",body.Text("caseId"),clock.GetUtcNow());}
    catch(KohamiException error) when(error.Status is 403 or 404){continue;}
    var current=await tx.Find("ReconciliationCase",body.Text("caseId"));
    if(current?.Body.Text("assignedActor")!=p.UserId)continue;
    result.Add(CommerceWire.Obj(("id",row[0]),("caseId",body.Text("caseId")),("findingCode",body.Text("findingCode")),("assignedBy",body.Text("assignedBy")),("createdAt",body.Text("createdAt")),("channel","InApp")));
   }
   return result;
  },h.RequestAborted);
  return FoundationHttp.Json(h,CommerceWire.Obj(("mode","mock"),("items",notifications)));
 }
 public async Task<IResult> ResolveReconciliationCase(HttpContext h,string id)
 {
  CommerceWire.Identifier(id);
  var p=await security.RequirePermissionAsync(h,"reconciliation.resolve","Case",id,ct:h.RequestAborted);
  var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);
  CommerceWire.Fields(b,["action","expectedVersion","reason","evidenceRef"],"compensationCaseId");
  var action=b.Text("action");
  if(action is not ("QueueOriginalCheck" or "LinkCompensationCase" or "RecordReviewedNotApplicable" or "CloseReconciled")||b.Text("reason").Length is <1 or >500)
   throw new KohamiException(422,"VALIDATION_ERROR");
  CommerceWire.Identifier(b.Text("evidenceRef"));
  if(action=="LinkCompensationCase")
  {
   if(b["compensationCaseId"] is not JsonValue linkedValue||!linkedValue.TryGetValue<string>(out var linkedId))throw new KohamiException(422,"VALIDATION_ERROR");
   CommerceWire.Identifier(linkedId);
  }
  else if(b["compensationCaseId"] is not null)throw new KohamiException(422,"VALIDATION_ERROR");
  var reply=await Write(h,p.UserId,"reconciliation/resolve/"+id,b,async tx=>
  {
   await RequireCurrentGrant(tx,p,"reconciliation.resolve","Case",id,clock.GetUtcNow());
   var existing=await tx.Find("ReconciliationCase",id);
   var version=existing?.Version??0;
   var expected=CommerceWire.Version("ReconciliationCase",id,version);
   if(h.Request.Headers.IfMatch.ToString().Length==0)throw new KohamiException(428,"PRECONDITION_REQUIRED");
   if(h.Request.Headers.IfMatch!='"'+expected+'"'||b.Text("expectedVersion")!=expected)throw new KohamiException(412,"VERSION_CHANGED");
   var currentFinding=await FindCurrentReconciliationFinding(tx,id);
   var caseBody=existing?.Body??currentFinding??throw new KohamiException(404,"RESOURCE_NOT_FOUND");
   if(existing?.Body.Text("state")=="ReviewedNotApplicable"||existing?.Body.Text("state")=="Closed"&&currentFinding is null)throw new KohamiException(409,"INVALID_TRANSITION");
   string nextState;
   if(action=="QueueOriginalCheck")nextState="CheckRequested";
   else if(action=="LinkCompensationCase")
   {
    var compensation=await tx.Get("RefundCase",b.Text("compensationCaseId"));
    if(compensation.Body.Text("basis")!="CollectionCompensation")throw new KohamiException(409,"INVALID_TRANSITION");
    caseBody["compensationCaseId"]=b.Text("compensationCaseId");nextState="CompensationLinked";
   }
   else if(action=="RecordReviewedNotApplicable")nextState="ReviewedNotApplicable";
   else
   {
    if(existing is null||existing.Body.Text("state") is not ("CheckRequested" or "CompensationLinked")||await IsCurrentReconciliationFinding(tx,existing.Body))throw new KohamiException(409,"INVALID_TRANSITION");
    nextState="Closed";
   }
   var now=clock.GetUtcNow();caseBody["id"]=id;caseBody["state"]=nextState;caseBody["responsibleActor"]=p.UserId;
   var history=caseBody["history"] as JsonArray??new JsonArray();caseBody["history"]=history;
   history.Add(CommerceWire.Obj(("action",action),("previousState",existing?.Body.Text("state")??"Open"),("state",nextState),("actor",p.UserId),("reason",b.Text("reason")),("evidenceRef",b.Text("evidenceRef")),("at",now)));
   var updatedVersion=version+1;var document=new CommerceDocument("ReconciliationCase",id,existing?.Owner??p.UserId,updatedVersion,caseBody);
   await tx.Save(document,existing is null);
   return new CommerceReply(CommerceWire.Obj(("id",id),("mode","mock"),("disposition",nextState),("action",action),("responsibleActor",p.UserId),("version",CommerceWire.Version("ReconciliationCase",id,updatedVersion)),("history",history.DeepClone())),200,updatedVersion);
  });
  return Result(h,reply);
 }
 public async Task<IResult> AssignReconciliationCase(HttpContext h,string id)
 {
  CommerceWire.Identifier(id);
  var p=await security.RequirePermissionAsync(h,"reconciliation.resolve","Case",id,ct:h.RequestAborted);
  var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);
  CommerceWire.Fields(b,["assigneeEmail","expectedVersion","reason"]);
  var email=b.Text("assigneeEmail").Trim().ToLowerInvariant();
  if(email.Length is <5 or >254||!Regex.IsMatch(email,@"^[^\s@]+@[^\s@]+\.[^\s@]+$",RegexOptions.CultureInvariant)||b.Text("reason").Length is <1 or >500)
   throw new KohamiException(422,"VALIDATION_ERROR");
  var assignee=assigneeResolver is null?null:await assigneeResolver.ResolveAsync(email,id,h.RequestAborted);
  var eligibleAssignee=assignee??throw new KohamiException(422,"INVALID_ASSIGNEE");
  var reply=await Write(h,p.UserId,"reconciliation/assign/"+id,b,async tx=>
  {
   await RequireCurrentGrant(tx,p,"reconciliation.resolve","Case",id,clock.GetUtcNow());
   var existing=await tx.Find("ReconciliationCase",id);
   var version=existing?.Version??0;
   var expected=CommerceWire.Version("ReconciliationCase",id,version);
   if(h.Request.Headers.IfMatch.ToString().Length==0)throw new KohamiException(428,"PRECONDITION_REQUIRED");
   if(h.Request.Headers.IfMatch!='"'+expected+'"'||b.Text("expectedVersion")!=expected)throw new KohamiException(412,"VERSION_CHANGED");
   var currentFinding=await FindCurrentReconciliationFinding(tx,id);
   var caseBody=existing?.Body??currentFinding??throw new KohamiException(404,"RESOURCE_NOT_FOUND");
   if(existing?.Body.Text("state")=="Closed"&&currentFinding is null)throw new KohamiException(409,"INVALID_TRANSITION");
   await RequireCurrentGrant(tx,new Principal(eligibleAssignee.UserId,"",eligibleAssignee.SecurityVersion,"AdminAuthenticated",[]),"reconciliation.resolve","Case",id,clock.GetUtcNow());
   var now=clock.GetUtcNow();
   var previous=caseBody["assignedActor"]?.GetValue<string>();
   var previousState=existing?.Body.Text("state")??"Open";
   var nextState=currentFinding is not null&&previousState is ("Closed" or "ReviewedNotApplicable")?"Reopened":previousState;
   caseBody["id"]=id;caseBody["state"]=nextState;caseBody["assignedActor"]=eligibleAssignee.UserId;caseBody["assignedBy"]=p.UserId;caseBody["assignedAt"]=now;
   var history=caseBody["history"] as JsonArray??new JsonArray();caseBody["history"]=history;
   history.Add(CommerceWire.Obj(("action","AssignResponsibleActor"),("previousAssignee",previous),("assignedActor",eligibleAssignee.UserId),("previousState",previousState),("state",nextState),("actor",p.UserId),("reason",b.Text("reason")),("at",now)));
   var updatedVersion=version+1;await tx.Save(new CommerceDocument("ReconciliationCase",id,existing?.Owner??p.UserId,updatedVersion,caseBody),existing is null);
   if(currentFinding is not null&&IsMajorReconciliationFinding(currentFinding.Text("code")))
   {
    var noticeId=ReconciliationNotificationId(id,updatedVersion,eligibleAssignee.UserId);
    var notice=CommerceWire.Obj(("id",noticeId),("caseId",id),("findingCode",currentFinding.Text("code")),("assignedBy",p.UserId),("createdAt",now),("mode","mock"));
    await tx.Save(new CommerceDocument("ReconciliationNotification",noticeId,eligibleAssignee.UserId,1,notice),true);
   }
   return new CommerceReply(CommerceWire.Obj(("id",id),("mode","mock"),("assignedActor",eligibleAssignee.UserId),("assignedBy",p.UserId),("assignedAt",now),("version",CommerceWire.Version("ReconciliationCase",id,updatedVersion))),200,updatedVersion);
  });
  return Result(h,reply);
 }
 static bool IsMajorReconciliationFinding(string? code)=>code is "NON_PRIMARY_COLLECTION_REQUIRES_REVIEW" or "COLLECTION_ORDER_MISSING" or "REFUND_PENDING_MISMATCH" or "REFUND_SETTLED_MISMATCH" or "REFUND_AMOUNT_MISMATCH" or "REFUND_SOURCE_MISSING" or "COMPENSATION_COLLECTION_MISSING";
 static string ReconciliationNotificationId(string caseId,long version,string recipient)=>"recon_notice_"+FoundationHttp.Hash(string.Join("\0",caseId,version.ToString(System.Globalization.CultureInfo.InvariantCulture),recipient))[..32];
 static string ReconciliationCaseId(JsonObject finding)
 {
  static string OptionalText(JsonObject value,string key)=>value[key] is JsonValue node&&node.TryGetValue<string>(out var text)?text:"";
  var identity=string.Join("\0",new[]{OptionalText(finding,"code"),OptionalText(finding,"sourceId"),OptionalText(finding,"operationId"),OptionalText(finding,"orderId")});
  return "recon_"+FoundationHttp.Hash(identity)[..32];
 }
 static async Task<JsonObject?> FindCurrentReconciliationFinding(CommerceTransaction tx,string caseId)
 {
  var collections=await tx.Rows("SELECT Id,OrderId,Kind,Collected,Pending,Refunded FROM commerce.Collections WHERE Dataset='kohami_local'");
  var operations=(await tx.Rows("SELECT Id,Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='RefundOperation'")).Select(x=>JsonNode.Parse((string)x[1])!.AsObject()).ToArray();
  var orders=(await tx.Rows("SELECT Id,Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order'")).Select(x=>new{Id=(string)x[0],Body=JsonNode.Parse((string)x[1])!.AsObject()}).ToArray();
  var collectionIds=collections.Select(x=>(string)x[0]).ToHashSet(StringComparer.Ordinal);var orderIds=orders.Select(x=>x.Id).ToHashSet(StringComparer.Ordinal);var findings=new List<JsonObject>();
  foreach(var c in collections)
  {
   var source=(string)c[0];var orderId=(string)c[1];var kind=(string)c[2];
   if(kind!="Primary")findings.Add(CommerceWire.Obj(("code","NON_PRIMARY_COLLECTION_REQUIRES_REVIEW"),("sourceId",source),("kind",kind),("collectedTwd",(long)c[3])));
   if(!orderIds.Contains(orderId))findings.Add(CommerceWire.Obj(("code","COLLECTION_ORDER_MISSING"),("sourceId",source)));
   var sourceOps=operations.Where(x=>x.Text("sourceCollectionId")==source).ToArray();var expectedPending=sourceOps.Where(x=>x.Text("state") is "Processing" or "Unknown").Sum(x=>x.Number("amountTwd"));var expectedRefunded=sourceOps.Where(x=>x.Text("state")=="Succeeded").Sum(x=>x.Number("amountTwd"));
   if(expectedPending!=(long)c[4])findings.Add(CommerceWire.Obj(("code","REFUND_PENDING_MISMATCH"),("sourceId",source),("ledgerTwd",(long)c[4]),("operationsTwd",expectedPending)));
   if(expectedRefunded!=(long)c[5])findings.Add(CommerceWire.Obj(("code","REFUND_SETTLED_MISMATCH"),("sourceId",source),("ledgerTwd",(long)c[5]),("operationsTwd",expectedRefunded)));
   foreach(var op in sourceOps){var units=op["units"] as JsonArray??[];var calculated=checked(units.Sum(x=>x?.AsObject().Number("paidTwd")??0)+op.Number("shippingTwd"));if(units.Count>0&&calculated!=op.Number("amountTwd"))findings.Add(CommerceWire.Obj(("code","REFUND_AMOUNT_MISMATCH"),("sourceId",source),("operationId",op.Text("id"))));}
  }
  foreach(var op in operations.Where(x=>!collectionIds.Contains(x.Text("sourceCollectionId"))))findings.Add(CommerceWire.Obj(("code","REFUND_SOURCE_MISSING"),("operationId",op.Text("id"))));
  foreach(var order in orders.Where(x=>CompensationRequired(x.Body)))if(!collections.Any(x=>(string)x[1]==order.Id&&((string)x[2] is "Late" or "Extra")))findings.Add(CommerceWire.Obj(("code","COMPENSATION_COLLECTION_MISSING"),("orderId",order.Id)));
  return findings.FirstOrDefault(x=>ReconciliationCaseId(x)==caseId);
 }
 static async Task<bool> IsCurrentReconciliationFinding(CommerceTransaction tx,JsonObject caseBody)=>await FindCurrentReconciliationFinding(tx,caseBody.Text("id")) is not null;
 static bool CompensationRequired(JsonObject body)=>body["compensationRequired"] is JsonValue value&&value.TryGetValue<bool>(out var required)&&required;
 public async Task<IResult> Receipts(HttpContext h,string id){var order=await store.Transaction(tx=>tx.Get("Order",id),h.RequestAborted);await Owner(h,order);var rows=await store.Transaction(tx=>tx.Rows("SELECT Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind IN('DemoReceipt','ReceiptAdjustment') AND JSON_VALUE(Body,'$.orderId')=@o",("@o",id)),h.RequestAborted);var result=rows.Select(x=>JsonNode.Parse((string)x[0])!.AsObject()).Select(x=>CommerceWire.Obj(("id",x.Text("id")),("mode","mock"),("kind",x.Text("kind")),("state",x.Text("state")),("amountTwd",x.Number("amountTwd")),("displayNotice",x.Text("displayNotice")),("createdAt",x.Text("createdAt")))).ToArray();return FoundationHttp.Json(h,CommerceWire.Obj(("orderId",id),("mode","mock"),("taxEffect","none"),("receipts",result.Where(x=>x.Text("kind")=="Receipt").ToArray()),("adjustments",result.Where(x=>x.Text("kind")=="Adjustment").ToArray())));}
}
