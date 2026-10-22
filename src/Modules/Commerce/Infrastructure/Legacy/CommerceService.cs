using System.Text.Json.Nodes;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;
using Kohami.Core;
using Kohami.Commerce.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Kohami.Commerce;
public sealed partial class CommerceService(CommerceStore store,SessionCrypto crypto,ISecurity security,TimeProvider clock,IReconciliationAssigneeResolver? assigneeResolver=null,ILogger<CommerceService>? logger=null) {
 string? ReadCookie(HttpContext h,string name){var raw=FoundationHttp.Cookie(h,crypto,name);if(raw is null)return null;try{var b=JsonNode.Parse(raw)!.AsObject();return DateTimeOffset.Parse(b.Text("until"))>clock.GetUtcNow()?b.Text("value"):null;}catch(Exception e) when(e is System.Text.Json.JsonException or FormatException or InvalidOperationException){return null;}}
 void SetCookie(HttpContext h,string name,string value,DateTimeOffset until)=>FoundationHttp.Cookie(h,crypto,name,CommerceWire.Obj(("value",value),("until",until.ToString("O"))).ToJsonString(),until);
 const string BrowserCookie="kohami.browser";
 string Browser(HttpContext h,bool create=false){var b=ReadCookie(h,BrowserCookie);if(b is null&&create){b=FoundationHttp.Id("browser");SetCookie(h,BrowserCookie,b,clock.GetUtcNow().AddDays(7));}return b??throw new KohamiException(401,"AUTHENTICATION_REQUIRED");}
 string CommandActor(HttpContext h,bool create=false){var actor=ReadCookie(h,"kohami.command");if(actor is null&&create){actor=FoundationHttp.Id("browser");SetCookie(h,"kohami.command",actor,clock.GetUtcNow().AddDays(30));}return actor??throw new KohamiException(401,"AUTHENTICATION_REQUIRED");}
 string CommandActorOrBrowser(HttpContext h){return ReadCookie(h,"kohami.command")??ReadCookie(h,BrowserCookie)??throw new KohamiException(401,"AUTHENTICATION_REQUIRED");}
 string CartId(HttpContext h)=>ReadCookie(h,"kohami.cart")??throw new KohamiException(404,"RESOURCE_NOT_FOUND");
 async Task Owner(HttpContext h,CommerceDocument d,string intent="read") {
  var p=await security.ResolveAsync(h,h.RequestAborted);if(p is not null&&p.Stage=="MemberAuthenticated"&&d.Owner==p.UserId)return;
  if(d.Kind=="Cart"&&d.Owner==CommandActorOrBrowser(h))return;
  if(d.Kind=="Checkout" && ReadCookie(h,intent=="pay"?"kohami.checkout.pay":"kohami.checkout.read")==d.Id && d.Owner==CommandActorOrBrowser(h))return;
  if(d.Kind=="Order"){var name=intent=="read"?"kohami.guest.read":"kohami.guest.action";var id=ReadCookie(h,name);if(id is not null){var okay=await store.Transaction(async tx=>{var n=await tx.Scalar("SELECT COUNT(*) FROM commerce.GuestCapabilities WHERE Dataset='kohami_local' AND Id=@id AND OrderId=@o AND BrowserId=@b AND Intent=@i AND (Consumed=0 OR OperationKey=@key) AND ExpiresAt>@now",("@id",id),("@o",d.Id),("@b",Browser(h)),("@i",intent),("@now",clock.GetUtcNow()),("@key",h.Request.Headers["Idempotency-Key"].ToString()));return Convert.ToInt32(n)==1;},h.RequestAborted);if(okay)return;}}
  throw new KohamiException(404,"RESOURCE_NOT_FOUND");
 }
 static IResult Result(HttpContext h,CommerceReply r){if(r.Version is not null)h.Response.Headers.ETag='"'+r.Body.Text("version")+'"';return FoundationHttp.Json(h,r.Body,r.Status);}
 static IResult DocumentResult(HttpContext h,CommerceDocument d,JsonObject? body=null){var b=body??d.Body;b["version"]=CommerceWire.Version(d.Kind,d.Id,d.Version);h.Response.Headers.ETag='"'+b.Text("version")+'"';return FoundationHttp.Json(h,b);}
 async Task<CommerceReply> Write(HttpContext h,string actor,string scope,JsonObject b,Func<CommerceTransaction,Task<CommerceReply>> mutation) {var key=FoundationHttp.Key(h);var hash=FoundationHttp.Hash(CommerceWire.RequestHash(b)+"\0"+h.Request.Headers.IfMatch.ToString());return await store.Transaction(async tx=>{var replay=await tx.Replay(actor,scope,key,hash);if(replay is not null)return replay;var r=await mutation(tx);await tx.Record(actor,scope,key,hash,r);return r;},h.RequestAborted);}
 public async Task<IResult> CreateCart(HttpContext h) {
  var principal=await security.ResolveAsync(h,h.RequestAborted);var actor=principal?.Stage=="MemberAuthenticated"?principal.UserId:CommandActor(h,true);var b=new JsonObject();var r=await Write(h,actor,"createCart",b,async tx=>{var id=FoundationHttp.Id("cart");var body=CommerceWire.Obj(("id",id),("mode","mock"),("version",CommerceWire.Version("Cart",id,1)),("items",new JsonArray()),("promotionCode",null));await tx.Save(new("Cart",id,actor,1,body),true);return new(body,201,1);});SetCookie(h,"kohami.cart",r.Body.Text("id"),clock.GetUtcNow().AddDays(7));return Result(h,r);
 }
 public async Task<IResult> Cart(HttpContext h){var d=await store.Transaction(tx=>tx.Get("Cart",CartId(h)),h.RequestAborted);await Owner(h,d);return DocumentResult(h,d);}
 public async Task<IResult> CartItem(HttpContext h,string sku) {
  CommerceWire.Identifier(sku);
  var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["quantity"]);var q=b.Number("quantity");if(q is <1 or >100)throw new KohamiException(422,"VALIDATION_ERROR");var old=await store.Transaction(tx=>tx.Get("Cart",CartId(h)),h.RequestAborted);await Owner(h,old);
  var r=await Write(h,old.Owner,"setCartItem/"+old.Id+"/"+sku,b,async tx=>{var d=await tx.Get("Cart",old.Id);CommerceWire.Expected(h,d);var row=await tx.Rows("SELECT Price,Stock,Active FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@s",("@s",sku));if(row.Count==0||!(bool)row[0][2])throw new KohamiException(409,"PRODUCT_UNAVAILABLE");if((int)row[0][1]<=0)throw new KohamiException(409,"PRODUCT_UNAVAILABLE");var items=d.Body["items"]!.AsArray();var existing=items.FirstOrDefault(x=>x!.AsObject().Text("skuId")==sku);if(existing is not null)items.Remove(existing);items.Add(CommerceWire.Item(sku,(int)q,(long)row[0][0],(int)row[0][1]));var next=d with {Version=d.Version+1};await tx.Save(next);return new(next.Body,200,next.Version);});return Result(h,r);
 }
 public async Task<IResult> RemoveCartItem(HttpContext h,string sku) {
  CommerceWire.Identifier(sku);
  var body=new JsonObject();
  var old=await store.Transaction(tx=>tx.Get("Cart",CartId(h)),h.RequestAborted);await Owner(h,old);
  var r=await Write(h,old.Owner,"removeCartItem/"+old.Id+"/"+sku,body,async tx=>{
   var d=await tx.Get("Cart",old.Id);CommerceWire.Expected(h,d);
   var items=d.Body["items"]!.AsArray();var existing=items.FirstOrDefault(x=>x!.AsObject().Text("skuId")==sku);
   if(existing is null)throw new KohamiException(404,"RESOURCE_NOT_FOUND");
   items.Remove(existing);var next=d with {Version=d.Version+1};await tx.Save(next);return new(next.Body,200,next.Version);
  });
  return Result(h,r);
 }
 public async Task<IResult> SetCartPromotion(HttpContext h) {
  var body=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(body,Array.Empty<string>(),"promotionCode");
  if(!body.ContainsKey("promotionCode"))throw new KohamiException(422,"VALIDATION_ERROR");
  var codeNode=body["promotionCode"];string? code=null;
  if(codeNode is not null&&(!(codeNode is JsonValue value)||!value.TryGetValue<string>(out code)))throw new KohamiException(422,"VALIDATION_ERROR");
  if(code is not null&&code!="DEMO15")throw new KohamiException(422,"VALIDATION_ERROR");
  var cartId=CartId(h);var old=await store.Transaction(tx=>tx.Get("Cart",cartId),h.RequestAborted);await Owner(h,old);
  var r=await Write(h,old.Owner,"setCartPromotion/"+old.Id,body,async tx=>{
   var d=await tx.Get("Cart",old.Id);CommerceWire.Expected(h,d);d.Body["promotionCode"]=code;
   var next=d with {Version=d.Version+1};await tx.Save(next);return new(next.Body,200,next.Version);
  });return Result(h,r);
 }
 public async Task<IResult> Quote(HttpContext h) {
  var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["cartVersion","deliveryMethod","region"]);if(!MockShippingFeePolicy.IsSupportedRegion(b.Text("region")))throw new KohamiException(409,"FEE_UNAVAILABLE");if(!MockShippingFeePolicy.IsSupportedDeliveryMethod(b.Text("deliveryMethod")))throw new KohamiException(422,"VALIDATION_ERROR");var fee=MockShippingFeePolicy.Resolve(b.Text("deliveryMethod"),b.Text("region"));if(fee is null)throw new KohamiException(409,"FEE_UNAVAILABLE");var old=await store.Transaction(tx=>tx.Get("Cart",CartId(h)),h.RequestAborted);await Owner(h,old);
  var r=await Write(h,old.Owner,"createQuote/"+old.Id,b,async tx=>{var d=await tx.Get("Cart",old.Id);CommerceWire.Expected(h,d,b,"cartVersion");var items=d.Body["items"]!.AsArray();if(items.Count==0)throw new KohamiException(422,"VALIDATION_ERROR");foreach(var x in items){var row=await tx.Rows("SELECT Price,Stock,Active FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@s",("@s",x!.AsObject().Text("skuId")));if(row.Count==0||!(bool)row[0][2])throw new KohamiException(409,"PRODUCT_UNAVAILABLE");if((int)row[0][1]<=0)throw new KohamiException(409,"PRODUCT_UNAVAILABLE");if((int)row[0][1]<(int)x!.AsObject().Number("quantity"))throw new KohamiException(409,"STOCK_CHANGED");if((long)row[0][0]!=x!.AsObject().Number("unitPriceTwd"))throw new KohamiException(409,"QUOTE_CHANGED");}
   var id=FoundationHttp.Id("quote");var publicBody=CommerceWire.Obj(("id",id),("mode","mock"),("expiresAt",clock.GetUtcNow().AddMinutes(5)),("snapshot",CommerceWire.Snapshot(items,fee,d.Body["promotionCode"]?.GetValue<string>()=="DEMO15")),("version",CommerceWire.Version("Quote",id,1)));
   var body=(JsonObject)publicBody.DeepClone();body["cartId"]=d.Id;body["cartVersion"]=CommerceWire.Version(d.Kind,d.Id,d.Version);body["deliveryMethod"]=b.Text("deliveryMethod");body["region"]=b.Text("region");body["used"]=false;await tx.Save(new("Quote",id,d.Owner,1,body),true);return new(publicBody,201,1);});return Result(h,r);
 }
 public async Task<IResult> Checkout(HttpContext h) {
  var principal=await security.ResolveAsync(h,h.RequestAborted);
  if(principal is null||principal.Stage!="MemberAuthenticated")throw new KohamiException(401,"AUTHENTICATION_REQUIRED");
  var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);
  CommerceWire.Fields(b,["quoteId","cartVersion","contact","delivery","confirmed"],"paymentMethod");
  if(b["confirmed"]?.GetValue<bool>()!=true)throw new KohamiException(422,"VALIDATION_ERROR");
  var walletCheckout=false;
  if(b.ContainsKey("paymentMethod")){
   if(b["paymentMethod"] is not JsonValue paymentMethod||!paymentMethod.TryGetValue<string>(out var selectedMethod)||selectedMethod is not ("wallet" or "card"))throw new KohamiException(422,"VALIDATION_ERROR");
   walletCheckout=selectedMethod=="wallet";
  }
  var contact=b["contact"]!.AsObject();CommerceWire.Fields(contact,["email"]);var email=contact.Text("email");if(email.Length>254||email.Count(x=>x=='@')!=1||!string.Equals(email,principal.Email,StringComparison.OrdinalIgnoreCase))throw new KohamiException(422,"VALIDATION_ERROR");
  var delivery=b["delivery"]!.AsObject();CommerceWire.Fields(delivery,["method","region"]);
  var cart=await store.Transaction(tx=>tx.Get("Cart",CartId(h)),h.RequestAborted);
  if(walletCheckout&&cart.Owner!=principal.UserId)throw new KohamiException(404,"RESOURCE_NOT_FOUND");
  await Owner(h,cart);
  var owner=principal.UserId;
  var r=await Write(h,cart.Owner,"createCheckout/"+cart.Id,b,async tx=>{
   var d=await tx.Get("Cart",cart.Id);var quote=await tx.Get("Quote",b.Text("quoteId"));
   if(quote.Owner!=d.Owner||quote.Body.Text("cartId")!=d.Id)throw new KohamiException(404,"RESOURCE_NOT_FOUND");
   CommerceWire.Expected(h,d,b,"cartVersion");
   if(quote.Body["used"]!.GetValue<bool>())throw new KohamiException(409,"QUOTE_CHANGED");
   if(DateTimeOffset.Parse(quote.Body.Text("expiresAt"))<=clock.GetUtcNow())throw new KohamiException(409,"QUOTE_EXPIRED");
   if(b.Text("cartVersion")!=CommerceWire.Version(d.Kind,d.Id,d.Version)||quote.Body.Text("cartVersion")!=CommerceWire.Version(d.Kind,d.Id,d.Version))throw new KohamiException(409,"QUOTE_CHANGED");
   if(delivery.Text("method")!=quote.Body.Text("deliveryMethod")||delivery.Text("region")!=quote.Body.Text("region"))throw new KohamiException(409,"QUOTE_CHANGED");
   var fee=MockShippingFeePolicy.Resolve(delivery.Text("method"),delivery.Text("region"));
   var quoteSnapshot=quote.Body["snapshot"]!.AsObject();
   if(fee is null||quoteSnapshot.Number("shippingTwd")!=fee.AmountTwd||quoteSnapshot.Text("feeVersion")!=fee.Version)throw new KohamiException(409,"QUOTE_CHANGED");
   var snapshot=quoteSnapshot.DeepClone().AsObject();
   var orderId=FoundationHttp.Id("order");var checkoutId=FoundationHttp.Id("checkout");var now=clock.GetUtcNow();var until=now.AddMinutes(15);
   long availableTwd=0;var walletEntryId=walletCheckout?FoundationHttp.Id("walletpay"):null;
   if(walletCheckout){
    var debit=await tx.Rows("DECLARE @result int,@balance bigint;EXEC @result=commerce.wallet_debit @UserId=@user,@EntryId=@entry,@SourceId=@source,@AmountTwd=@amount,@CreatedAt=@now,@AvailableTwd=@balance OUTPUT;SELECT @result,@balance",("@user",principal!.UserId),("@entry",walletEntryId!),("@source",checkoutId),("@amount",snapshot.Number("totalTwd")),("@now",now));
    if(debit.Count!=1||Convert.ToInt32(debit[0][0])!=1)throw new KohamiException(409,"WALLET_INSUFFICIENT_BALANCE");
    availableTwd=(long)debit[0][1];
   }
   foreach(var node in snapshot["lines"]!.AsArray()){
    var line=node!.AsObject();var sku=line.Text("skuId");var quantity=(int)line.Number("quantity");
    var changed=await tx.Execute("UPDATE commerce.Inventory SET Stock=Stock-@q WHERE Dataset='kohami_local' AND Sku=@s AND Active=1 AND Price=@price AND Stock>=@q",("@q",quantity),("@s",sku),("@price",line.Number("unitPriceTwd")));
    if(changed!=1)throw new KohamiException(409,"STOCK_CHANGED");
    await tx.Execute("INSERT commerce.Reservations VALUES('kohami_local',@o,@s,@q,@state,@e)",("@o",orderId),("@s",sku),("@q",quantity),("@state",walletCheckout?"Consumed":"Held"),("@e",until));
    line["orderLineId"]=FoundationHttp.Id("line");
    foreach(var unit in line["units"]!.AsArray())unit!.AsObject()["unitId"]=FoundationHttp.Id("unit");
   }
   var body=CommerceWire.Obj(("checkoutId",checkoutId),("orderId",orderId),("mode","mock"),("version",CommerceWire.Version("Checkout",checkoutId,1)),("expiresAt",until),("snapshot",snapshot),("statusUrl","/api/v1/checkouts/"+checkoutId+"/status"),("capabilities",walletCheckout?new[]{"checkout.read"}:new[]{"checkout.read","checkout.pay"}));
   var stored=body.DeepClone().AsObject();stored["orderState"]=walletCheckout?"Confirmed":"AwaitingPayment";stored["paymentState"]=walletCheckout?"Succeeded":"Created";stored["compensationRequired"]=false;stored["email"]=email;stored["placedAt"]=now.ToString("O");stored["browserId"]=cart.Owner;
   if(walletCheckout){
    stored["paymentMethod"]="wallet";stored["paymentConfirmedAt"]=now.ToString("O");
    var entryId=walletEntryId!;
    var collectionId="wallet_collection_"+entryId;
    await tx.Execute("INSERT commerce.Collections(Dataset,Id,OrderId,Kind,Collected,ShippingPaid,PaymentMethod) VALUES('kohami_local',@id,@o,'Primary',@amount,@shipping,'Wallet')",("@id",collectionId),("@o",orderId),("@amount",snapshot.Number("totalTwd")),("@shipping",snapshot.Number("shippingTwd")));
    foreach(var lineNode in snapshot["lines"]!.AsArray()){
     var line=lineNode!.AsObject();
     foreach(var unitNode in line["units"]!.AsArray()){
      var unit=unitNode!.AsObject();
      await tx.Execute("INSERT commerce.RefundUnits(Dataset,UnitId,OrderId,LineId,CollectionId,Ordinal,Paid) VALUES('kohami_local',@u,@o,@l,@c,@n,@a)",("@u",unit.Text("unitId")),("@o",orderId),("@l",line.Text("orderLineId")),("@c",collectionId),("@n",(int)unit.Number("ordinal")),("@a",unit.Number("paidTwd")));
     }
    }
    var payment=CommerceWire.Obj(("id",entryId),("checkoutId",checkoutId),("orderId",orderId),("mode","mock"),("method","wallet"),("state","Succeeded"),("amountTwd",snapshot.Number("totalTwd")),("currency","TWD"),("createdAt",now),("availableTwd",availableTwd));
    await tx.Save(new("WalletPayment",entryId,principal!.UserId,1,payment),true);
   }
   d.Body["items"]=new JsonArray();d.Body["promotionCode"]=null;await tx.Save(d with {Version=d.Version+1});
   await tx.Save(new("Checkout",checkoutId,owner,1,stored),true);
   var order=stored.DeepClone().AsObject();order["id"]=orderId;await tx.Save(new("Order",orderId,owner,1,order),true);
   quote.Body["used"]=true;await tx.Save(quote with {Version=quote.Version+1});
   return new(body,201,1);
  });
  var checkout=r.Body.Text("checkoutId");var expires=DateTimeOffset.Parse(r.Body.Text("expiresAt"));
  if(expires>clock.GetUtcNow()){
   SetCookie(h,"kohami.checkout.read",checkout,expires);
   if(!walletCheckout)SetCookie(h,"kohami.checkout.pay",checkout,expires);
  }
  return Result(h,r);
 }
 public async Task<IResult> Status(HttpContext h,string id){var d=await store.Transaction(tx=>tx.Get("Checkout",id),h.RequestAborted);await Owner(h,d);var b=CommerceWire.Obj(("checkoutId",id),("mode","mock"),("version",CommerceWire.Version(d.Kind,d.Id,d.Version)),("orderState",d.Body.Text("orderState")),("paymentState",d.Body.Text("paymentState")),("compensationRequired",d.Body["compensationRequired"]!.DeepClone()),("expiresAt",d.Body.Text("expiresAt")),("snapshot",d.Body["snapshot"]!.DeepClone()));return DocumentResult(h,d,b);}
 private sealed record WalletLedgerCursor(DateTimeOffset CreatedAt,string EntryId);
 private static int WalletPageLimit(HttpContext h){var values=h.Request.Query["limit"];if(values.Count==0)return 20;if(values.Count!=1||!int.TryParse(values[0],NumberStyles.None,CultureInfo.InvariantCulture,out var limit)||limit is <1 or >100)throw new KohamiException(422,"VALIDATION_ERROR");return limit;}
 private static WalletLedgerCursor? WalletPageCursor(HttpContext h){var values=h.Request.Query["cursor"];if(values.Count==0)return null;if(values.Count!=1)throw new KohamiException(422,"VALIDATION_ERROR");var raw=values[0];if(string.IsNullOrEmpty(raw)||raw.Length>512)throw new KohamiException(422,"VALIDATION_ERROR");try{var token=raw.Replace('-','+').Replace('_','/');token+=new string('=',(4-token.Length%4)%4);using var json=JsonDocument.Parse(Convert.FromBase64String(token));var root=json.RootElement;if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("createdAt",out var createdAt)||!root.TryGetProperty("entryId",out var entryId)||!createdAt.TryGetDateTimeOffset(out var created)||entryId.ValueKind!=JsonValueKind.String)throw new KohamiException(422,"VALIDATION_ERROR");var id=entryId.GetString();if(id is null||id.Length is <1 or >100||id.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('_' or '-')))throw new KohamiException(422,"VALIDATION_ERROR");return new(created,id);}catch(FormatException){throw new KohamiException(422,"VALIDATION_ERROR");}catch(JsonException){throw new KohamiException(422,"VALIDATION_ERROR");}catch(InvalidOperationException){throw new KohamiException(422,"VALIDATION_ERROR");}}
 private static string WalletCursorToken(WalletLedgerCursor cursor){var bytes=JsonSerializer.SerializeToUtf8Bytes(CommerceWire.Obj(("createdAt",cursor.CreatedAt),("entryId",cursor.EntryId)));return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');}
 public async Task<IResult> CurrentWallet(HttpContext h){var principal=await security.ResolveAsync(h,h.RequestAborted);if(principal?.Stage!="MemberAuthenticated")throw new KohamiException(401,"AUTHENTICATION_REQUIRED");var limit=WalletPageLimit(h);var cursor=WalletPageCursor(h);var wallet=await store.Transaction(async tx=>{var accounts=await tx.Rows("SELECT AvailableTwd,Version FROM commerce.WalletAccounts WHERE Dataset='kohami_local' AND UserId=@u",("@u",principal.UserId));var ledger=await tx.Rows("SELECT TOP(@take) EntryId,EntryType,AmountTwd,Currency,SourceId,CreatedAt FROM commerce.WalletLedger WHERE Dataset='kohami_local' AND UserId=@u AND (@cursorCreatedAt IS NULL OR CreatedAt<@cursorCreatedAt OR (CreatedAt=@cursorCreatedAt AND EntryId<@cursorEntryId)) ORDER BY CreatedAt DESC,EntryId DESC",("@take",limit+1),("@u",principal.UserId),("@cursorCreatedAt",cursor?.CreatedAt),("@cursorEntryId",cursor?.EntryId));var hasMore=ledger.Count>limit;var page=ledger.Take(limit).ToArray();var entries=page.Select(row=>CommerceWire.Obj(("id",row[0]),("type",row[1]),("amountTwd",row[2]),("currency",row[3]),("sourceId",row[4]),("createdAt",row[5]))).ToArray();var next=hasMore&&page.Length>0?WalletCursorToken(new((DateTimeOffset)page[^1][5],(string)page[^1][0])):null;return CommerceWire.Obj(("mode","mock"),("currency","TWD"),("availableTwd",accounts.Count==0?0L:(long)accounts[0][0]),("version",accounts.Count==0?0L:(long)accounts[0][1]),("entries",entries),("nextCursor",next));},h.RequestAborted);return FoundationHttp.Json(h,wallet);}
 public async Task<IResult> TopUpWallet(HttpContext h){var principal=await security.ResolveAsync(h,h.RequestAborted);if(principal?.Stage!="MemberAuthenticated")throw new KohamiException(401,"AUTHENTICATION_REQUIRED");var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["amountTwd","cardNumber"]);var amount=b.Number("amountTwd");if(amount is <1 or >50000)throw new KohamiException(422,"VALIDATION_ERROR");if(!IsSyntheticCard(b.Text("cardNumber")))throw new KohamiException(422,"CARD_INVALID");b.Remove("cardNumber");var entry=FoundationHttp.Id("walletcredit");var now=clock.GetUtcNow();var r=await Write(h,principal.UserId,"wallet/topup",b,async tx=>{var rows=await tx.Rows("EXEC commerce.wallet_mock_credit @UserId=@u,@ActorId=@actor,@EntryId=@entry,@SourceId=@source,@EntryType='MemberTopUp',@AmountTwd=@amount,@Reason=@reason,@CreatedAt=@now",("@u",principal.UserId),("@actor",principal.UserId),("@entry",entry),("@source",entry),("@amount",amount),("@reason","Member synthetic test-card top-up"),("@now",now));var balance=rows.Count==0?await tx.Scalar("SELECT AvailableTwd FROM commerce.WalletAccounts WHERE Dataset='kohami_local' AND UserId=@u",("@u",principal.UserId)):rows[0][0];return new(CommerceWire.Obj(("entryId",entry),("mode","mock"),("amountTwd",amount),("availableTwd",balance)),201);});return Result(h,r);}
 public async Task<IResult> AdminWalletCredit(HttpContext h){var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["memberUserId","amountTwd","reason"]);var memberId=b.Text("memberUserId");CommerceWire.Identifier(memberId);var amount=b.Number("amountTwd");var reason=b.Text("reason").Trim();if(amount is <1 or >50000||reason.Length is <1 or >500)throw new KohamiException(422,"VALIDATION_ERROR");var actor=await security.RequirePermissionAsync(h,"user.manage","User",memberId,ct:h.RequestAborted);var entry=FoundationHttp.Id("walletadmin");var now=clock.GetUtcNow();var r=await Write(h,actor.UserId,"admin/wallet-credit/"+memberId,b,async tx=>{await RequireCurrentGrant(tx,actor,"user.manage","User",memberId,now);var rows=await tx.Rows("EXEC commerce.wallet_mock_credit @UserId=@u,@ActorId=@actor,@EntryId=@entry,@SourceId=@source,@EntryType='AdminCredit',@AmountTwd=@amount,@Reason=@reason,@CreatedAt=@now",("@u",memberId),("@actor",actor.UserId),("@entry",entry),("@source",entry),("@amount",amount),("@reason",reason),("@now",now));var balance=rows.Count==0?await tx.Scalar("SELECT AvailableTwd FROM commerce.WalletAccounts WHERE Dataset='kohami_local' AND UserId=@u",("@u",memberId)):rows[0][0];return new(CommerceWire.Obj(("entryId",entry),("userId",memberId),("mode","mock"),("amountTwd",amount),("availableTwd",balance)),201);});return Result(h,r);}
 public async Task<IResult> Pay(HttpContext h,string id){var b=await FoundationHttp.BodyAsync(h,h.RequestAborted);CommerceWire.Fields(b,["expectedVersion","cardNumber"]);if(!IsSyntheticCard(b.Text("cardNumber")))throw new KohamiException(422,"CARD_INVALID");b.Remove("cardNumber");var old=await store.Transaction(tx=>tx.Get("Checkout",id),h.RequestAborted);await Owner(h,old,"pay");var r=await Write(h,old.Owner,"pay/"+id,b,async tx=>{var d=await tx.Get("Checkout",id);CommerceWire.Expected(h,d,b);await CommerceLifecycle.Expire(tx,d,clock.GetUtcNow());if(d.Body.Text("orderState")!="AwaitingPayment")throw new KohamiException(409,"INVALID_TRANSITION");if(d.Body.Text("paymentState")=="Unknown")throw new KohamiException(409,"PAYMENT_UNKNOWN");if(d.Body.Text("paymentState") is "Processing" or "Succeeded")throw new KohamiException(409,"INVALID_TRANSITION");var attempt=FoundationHttp.Id("pay");var body=CommerceWire.Obj(("id",attempt),("checkoutId",id),("mode","mock"),("state","Processing"),("amountTwd",d.Body["snapshot"]!.AsObject().Number("totalTwd")),("version",CommerceWire.Version("PaymentAttempt",attempt,1)));await tx.Save(new("PaymentAttempt",attempt,d.Owner,1,body),true);d.Body["paymentState"]="Processing";await CommerceLifecycle.SaveOrderAndCheckout(tx,d with {Version=d.Version+1});await tx.Job("Payment",attempt,d.Owner,0,null,"Checkout",id,CommerceWire.Obj(("outcome","success")),clock.GetUtcNow());return new(body,202,1);});return Result(h,r);}
 static bool IsSyntheticCard(string value)=>value=="4111111111111111";
}
