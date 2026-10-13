using System.Text.Json.Nodes;
using Kohami.M0.Commerce;
using Kohami.M0.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

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
var settings=M0Settings.Load(config);var store=new CommerceStore(new M0Sql(settings));var worker=new MockWorker(store,TimeProvider.System,NullLogger<MockWorker>.Instance);
// Initial fixture has a single last item. Serialized conditional decrements must produce exactly one committed order.
var prefix=M0Http.Id("race");var results=await Task.WhenAll(Enumerable.Range(0,20).Select(async i=>{try{return await store.Transaction(async tx=>{var count=await tx.Execute("UPDATE m0commerce.Inventory SET Stock=Stock-1 WHERE Dataset='demo_m0' AND Sku='sku_demo_01' AND Stock>=1");if(count!=1)throw new M0Exception(409,"STOCK_CHANGED");await tx.Execute("INSERT m0commerce.Reservations VALUES('demo_m0',@o,'sku_demo_01',1,'Held',@e)",("@o",prefix+i),("@e",DateTimeOffset.UtcNow.AddMinutes(15)));return true;});}catch(M0Exception){return false;}}));
Check(results.Count(x=>x)==1,"SQL twenty requests last item one commit");
// A later SKU failure must roll back the first mutation.
var before=await store.Transaction(tx=>tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_02'"));try{await store.Transaction<bool>(async tx=>{await tx.Execute("UPDATE m0commerce.Inventory SET Stock=Stock-1 WHERE Dataset='demo_m0' AND Sku='sku_demo_02'");throw new M0Exception(409,"STOCK_CHANGED");});}catch(M0Exception){}
var after=await store.Transaction(tx=>tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_02'"));Check(Equals(before,after),"SQL multi SKU rollback");
await worker.ExpireReservations();Console.WriteLine("SQL tests complete; synthetic last-item reservation remains fixture evidence");
