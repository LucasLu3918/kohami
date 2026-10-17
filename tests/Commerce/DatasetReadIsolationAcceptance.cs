using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.M0.Core;
using Kohami.M0.Commerce;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

static class DatasetReadIsolationAcceptance {
 static void Check(bool pass,string name){if(!pass)throw new InvalidOperationException("FAILED: "+name);Console.WriteLine("PASS: "+name);}

 public static async Task RunAsync(CommerceStore store,M0Settings settings,string adminConnectionString) {
  var suffix=Guid.NewGuid().ToString("N");
  var sku="";
  long expectedCatalogPrice;
  int expectedCatalogStock;
  var checkout="isolation_checkout_"+suffix;
  var order="isolation_order_"+suffix;
  var collection="isolation_collection_"+suffix;
  var refund="isolation_refund_"+suffix;
  var receipt="isolation_receipt_"+suffix;
  var owner="browser_isolation_"+suffix;
  var crypto=new M0Crypto(new EphemeralDataProtectionProvider());
  var service=new CommerceService(store,crypto,new DatasetIsolationSecurity(),TimeProvider.System);
  var rawConnectionString=adminConnectionString;

  try {
   // Select the baseline from this dedicated fixture. Earlier Commerce SQL checks may
   // already have consumed sku_demo_01's single unit, so availability is not a constant.
   await using(var c=new SqlConnection(rawConnectionString)) {
    await c.OpenAsync();
    await using var baseline=new SqlCommand(@"
SELECT TOP(1) i.Sku,i.Price,i.Stock
FROM m0commerce.Inventory i
WHERE i.Dataset='demo_m0' AND i.Active=1
ORDER BY i.Sku;",c);
    await using var reader=await baseline.ExecuteReaderAsync();
    if(!await reader.ReadAsync())throw new InvalidOperationException("DEDICATED_PUBLIC_SKU_REQUIRED");
    sku=reader.GetString(0);expectedCatalogPrice=Convert.ToInt64(reader.GetValue(1));expectedCatalogStock=reader.GetInt32(2);
   }
   await using(var c=new SqlConnection(rawConnectionString)) {
    await c.OpenAsync();
    await using var alter=new SqlCommand("ALTER TABLE m0commerce.Inventory NOCHECK CONSTRAINT ALL; ALTER TABLE m0commerce.Documents NOCHECK CONSTRAINT ALL; ALTER TABLE m0commerce.Collections NOCHECK CONSTRAINT ALL;",c);
    await alter.ExecuteNonQueryAsync();
   }
   var now=DateTimeOffset.UtcNow;
   await using(var c=new SqlConnection(rawConnectionString)) {
    await c.OpenAsync();
    await using var seed=new SqlCommand(@"
INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES
 ('demo_m0','Checkout',@checkout,@owner,1,N'{""id"":""' + @checkout + N'"",""orderId"":""' + @order + N'"",""orderState"":""AwaitingPayment"",""paymentState"":""Created"",""compensationRequired"":false,""expiresAt"":""2035-01-01T00:00:00Z"",""snapshot"":{""lines"":[]}}'),
 ('demo_m0','Order',@order,@owner,1,N'{""orderState"":""Confirmed"",""paymentState"":""Succeeded"",""placedAt"":""2030-01-01T00:00:00Z"",""compensationRequired"":false,""snapshot"":{""lines"":[]}}'),
 ('demo_m0','RefundOperation',@refund,@owner,1,N'{""id"":""demo_refund"",""orderId"":""' + @order + N'"",""sourceCollectionId"":""' + @collection + N'"",""state"":""Unknown"",""amountTwd"":10,""shippingTwd"":0,""units"":[]}'),
 ('demo_m0','DemoReceipt',@receipt,@owner,1,N'{""id"":""demo_receipt"",""orderId"":""' + @order + N'"",""kind"":""Receipt"",""state"":""Issued"",""amountTwd"":100,""displayNotice"":""DEMO"",""createdAt"":""2030-01-01T00:00:00Z""}');
INSERT m0commerce.Collections(Dataset,Id,OrderId,Kind,Collected,Pending,Refunded,ShippingPaid)
 VALUES('demo_m0',@collection,@order,'Primary',100,10,5,0);
INSERT m0commerce.Inventory(Dataset,Sku,Price,Stock,Active) VALUES('other_dataset',@sku,99000,999,1);",c);
    seed.Parameters.AddWithValue("@checkout",checkout);seed.Parameters.AddWithValue("@order",order);seed.Parameters.AddWithValue("@collection",collection);seed.Parameters.AddWithValue("@refund",refund);seed.Parameters.AddWithValue("@receipt",receipt);seed.Parameters.AddWithValue("@owner",owner);seed.Parameters.AddWithValue("@sku",sku);
    await seed.ExecuteNonQueryAsync();
   }

   var reportBeforeContext=NewContext("/api/v1/admin/reconciliation-report");
   var reportBefore=await ReadResult(await service.ReconciliationReport(reportBeforeContext),reportBeforeContext);
   await using(var c=new SqlConnection(rawConnectionString)) {
    await c.OpenAsync();
    await using var seed=new SqlCommand(@"
INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES
 ('other_dataset','Checkout',@checkout,'foreign_browser',1,N'{""id"":""foreign_checkout"",""orderId"":""' + @order + N'"",""orderState"":""Confirmed"",""paymentState"":""Succeeded"",""compensationRequired"":true,""expiresAt"":""2030-01-01T00:00:00Z"",""snapshot"":{""lines"":[]}}'),
 ('other_dataset','Order',@order,'foreign_browser',1,N'{""orderState"":""Cancelled"",""paymentState"":""Failed"",""compensationRequired"":true,""snapshot"":{""lines"":[]}}'),
 ('other_dataset','RefundOperation',@refund,'foreign_browser',1,N'{""id"":""foreign_refund"",""orderId"":""' + @order + N'"",""sourceCollectionId"":""' + @collection + N'"",""state"":""Succeeded"",""amountTwd"":90000,""shippingTwd"":0,""units"":[]}'),
 ('other_dataset','DemoReceipt',@receipt,'foreign_browser',1,N'{""id"":""foreign_receipt"",""orderId"":""' + @order + N'"",""kind"":""Receipt"",""state"":""Failed"",""amountTwd"":90000,""displayNotice"":""FOREIGN"",""createdAt"":""2030-01-01T00:00:00Z""}');
INSERT m0commerce.Collections(Dataset,Id,OrderId,Kind,Collected,Pending,Refunded,ShippingPaid)
 VALUES('other_dataset',@collection,@order,'Primary',99000,99000,0,99000);",c);
    seed.Parameters.AddWithValue("@checkout",checkout);seed.Parameters.AddWithValue("@order",order);seed.Parameters.AddWithValue("@collection",collection);seed.Parameters.AddWithValue("@refund",refund);seed.Parameters.AddWithValue("@receipt",receipt);
    await seed.ExecuteNonQueryAsync();
   }

   var found=await store.Transaction(tx=>tx.Get("Checkout",checkout));
   Check(found.Body.Text("paymentState")=="Created","checkout document lookup selects only the configured Dataset when IDs collide");

   var statusContext=NewContext("/api/v1/checkouts/"+checkout+"/status");
   var expires=now.AddHours(1);
   SetRequestCookie(statusContext,crypto,"kohami.browser",CommerceWire.Obj(("value",owner),("until",expires.ToString("O"))).ToJsonString());
   SetRequestCookie(statusContext,crypto,"kohami.checkout.read",CommerceWire.Obj(("value",checkout),("until",expires.ToString("O"))).ToJsonString());
   var status=await ReadResult(await service.Status(statusContext,checkout),statusContext);
   Check(status["paymentState"]?.GetValue<string>()=="Created","CheckoutStatus API returns the configured Dataset row when an ID collides");

   var productContext=NewContext("/api/v1/products?datasetId=other_dataset");
   var products=await ReadResult(await service.Products(productContext),productContext);
   var duplicateSku=products["items"]!.AsArray().Single(x=>x!.AsObject().Text("skuId")==sku)!.AsObject();
   var catalogPrice=duplicateSku.Number("priceTwd");var catalogAvailability=duplicateSku.Text("availability");
   var expectedCatalogAvailability=expectedCatalogStock>0?"InStock":"OutOfStock";
   Check(catalogPrice==expectedCatalogPrice&&catalogAvailability==expectedCatalogAvailability,$"catalog read ignores a foreign Dataset row for the same SKU (price={catalogPrice}, availability={catalogAvailability})");

   var orderContext=NewContext("/api/v1/orders/"+order);
   var orderRead=await ReadResult(await service.Order(orderContext,order),orderContext);
   Check(orderRead["orderState"]?.GetValue<string>()=="Confirmed"&&orderRead["paymentSummary"]!["confirmedCollectedTwd"]!.GetValue<long>()==100,"Order API selects configured Dataset order and collection summary");
   Check(orderRead["refundSummary"]!["unknownCount"]!.GetValue<int>()==1&&orderRead["refundSummary"]!["succeededCount"]!.GetValue<int>()==0,"Order API excludes foreign refund-operation details");
   Check(orderRead["receiptSummary"]!["state"]?.GetValue<string>()=="Confirmed","Order API excludes foreign receipt state");

   var resolved=await new CommerceResourceResolver(new M0Sql(settings)).ResolveScopesAsync("Checkout",checkout);
   Check(resolved.Contains(new CommerceScope("Dataset","demo_m0"))&&resolved.Contains(new CommerceScope("Order",order)),"resource scope resolver resolves the configured Dataset row when IDs collide");
   var reportAfterContext=NewContext("/api/v1/admin/reconciliation-report");
   var reportAfter=await ReadResult(await service.ReconciliationReport(reportAfterContext),reportAfterContext);
   Check(reportBefore["summary"]!.ToJsonString()==reportAfter["summary"]!.ToJsonString()&&reportBefore["findings"]!.ToJsonString()==reportAfter["findings"]!.ToJsonString(),"reconciliation report is unchanged by foreign Dataset ledger and receipt rows");
  }
  finally {
   await using var c=new SqlConnection(rawConnectionString);await c.OpenAsync();
   await using var cleanup=new SqlCommand(@"
DELETE m0commerce.Inventory WHERE Dataset='other_dataset' AND Sku=@sku;
DELETE m0commerce.Collections WHERE Dataset IN('demo_m0','other_dataset') AND Id=@collection;
DELETE m0commerce.Documents WHERE Dataset IN('demo_m0','other_dataset') AND Id IN(@checkout,@order,@refund,@receipt);
ALTER TABLE m0commerce.Inventory WITH CHECK CHECK CONSTRAINT ALL;
ALTER TABLE m0commerce.Documents WITH CHECK CHECK CONSTRAINT ALL;
ALTER TABLE m0commerce.Collections WITH CHECK CHECK CONSTRAINT ALL;",c);
   cleanup.Parameters.AddWithValue("@sku",sku);cleanup.Parameters.AddWithValue("@collection",collection);cleanup.Parameters.AddWithValue("@checkout",checkout);cleanup.Parameters.AddWithValue("@order",order);cleanup.Parameters.AddWithValue("@refund",refund);cleanup.Parameters.AddWithValue("@receipt",receipt);await cleanup.ExecuteNonQueryAsync();
  }
 }

 static DefaultHttpContext NewContext(string target){var context=new DefaultHttpContext();context.RequestServices=new ReconciliationResultServices();context.Request.Method="GET";context.Request.Path=target.Split('?')[0];if(target.Contains('?'))context.Request.QueryString=new QueryString("?"+target.Split('?')[1]);context.Response.Body=new MemoryStream();return context;}
 static void SetRequestCookie(DefaultHttpContext context,M0Crypto crypto,string name,string value){var existing=context.Request.Headers.Cookie.ToString();context.Request.Headers.Cookie=string.IsNullOrEmpty(existing)?name+"="+crypto.Sessions.CreateProtector(name).Protect(value):existing+"; "+name+"="+crypto.Sessions.CreateProtector(name).Protect(value);}
 static async Task<JsonObject> ReadResult(IResult result,DefaultHttpContext context){await result.ExecuteAsync(context);context.Response.Body.Position=0;return (await JsonNode.ParseAsync(context.Response.Body))!.AsObject();}
}

sealed class DatasetIsolationSecurity:IM0Security {
 public Task<M0Principal?> ResolveAsync(HttpContext context,CancellationToken ct=default)=>Task.FromResult<M0Principal?>(new M0Principal("fixture_owner","synthetic-fixture-session",1,"AdminAuthenticated",["orders.read","reconciliation.read"]));
 public Task<M0Principal> RequirePermissionAsync(HttpContext context,string permission,string resourceKind,string resourceId,CancellationToken ct=default)=>permission is "orders.read" or "reconciliation.read"?Task.FromResult(new M0Principal("fixture_owner","synthetic-fixture-session",1,"AdminAuthenticated",["orders.read","reconciliation.read"])):Task.FromException<M0Principal>(new M0Exception(403,"PERMISSION_CHANGED"));
 public Task<bool> CanDispatchAsync(string actorUserId,long securityVersion,string permission,string resourceKind,string resourceId,CancellationToken ct=default)=>Task.FromResult(false);
}
