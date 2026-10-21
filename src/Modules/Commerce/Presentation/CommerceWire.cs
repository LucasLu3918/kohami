using System.Text.Json.Nodes;
using Kohami.Core;
using Kohami.Commerce.Domain;
namespace Kohami.Commerce;
public static class CommerceWire {
 public static string RequestHash(JsonObject body){static JsonNode? Sort(JsonNode? n){if(n is JsonObject o){var result=new JsonObject();foreach(var p in o.OrderBy(x=>x.Key,StringComparer.Ordinal))result[p.Key]=Sort(p.Value);return result;}if(n is JsonArray a)return new JsonArray(a.Select(Sort).ToArray());return n?.DeepClone();}return FoundationHttp.Hash(Sort(body)!.ToJsonString());}
 public static string Version(string kind,string id,long version)=>FoundationHttp.Hash("kohami_local\0"+kind+"\0"+id+"\0"+version.ToString(System.Globalization.CultureInfo.InvariantCulture));
 public static JsonObject Obj(params (string Name,object? Value)[] fields){var b=new JsonObject();foreach(var f in fields)b[f.Name]=System.Text.Json.JsonSerializer.SerializeToNode(f.Value);return b;}
 public static void Identifier(string id){if(id.Length is <1 or >100||id.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='_'&&c!='-'))throw new KohamiException(422,"VALIDATION_ERROR");}
 public static void Fields(JsonObject b,string[] required,params string[] optional){if(required.Any(k=>b[k] is null)||b.Any(k=>!required.Contains(k.Key)&&!optional.Contains(k.Key)))throw new KohamiException(422,"VALIDATION_ERROR");foreach(var p in b){if(p.Value is JsonValue v&&v.TryGetValue<string>(out var text)){if(text.Length>4096)throw new KohamiException(422,"VALIDATION_ERROR");if(p.Key.EndsWith("Id",StringComparison.Ordinal))Identifier(text);}}}
 public static void Expected(Microsoft.AspNetCore.Http.HttpContext h,CommerceDocument d,JsonObject? body=null,string field="expectedVersion"){var current=Version(d.Kind,d.Id,d.Version);var header=h.Request.Headers.IfMatch.ToString();if(header.Length==0)throw new KohamiException(428,"PRECONDITION_REQUIRED");if(header!='"'+current+'"')throw new KohamiException(412,"VERSION_CHANGED");if(body is not null&&body.Text(field)!=current)throw new KohamiException(412,"VERSION_CHANGED");}
 public static JsonObject Content(string text)=>Obj(("value",text),("requestedLocale","zh-TW"),("sourceLocale","zh-TW"),("revision","content_demo_v1"),("fallback",false));
 public static JsonObject Item(string sku,int quantity,long price,int stock)=>Obj(("skuId",sku),("quantity",quantity),("productSlug",sku+"_product"),("name",Content("合成商品")),("colorId","color_demo"),("colorLabel",Content("示意色")),("garmentSize","M"),("unitPriceTwd",price),("availability",stock>0?"InStock":"OutOfStock"),("reasonCodes",Array.Empty<string>()),("priceVersion","prices_demo_v1"),("stockVersion","stock_demo_v1"));
 // Largest-remainder allocation uses stable SKU order and ordinal order; money remains integer TWD.
 public static JsonObject Snapshot(JsonArray items,long shipping,bool discount) {
  return Snapshot(items,shipping,discount,MockShippingFeePolicy.CurrentVersion);
 }
 public static JsonObject Snapshot(JsonArray items,MockShippingFee fee,bool discount) {
  return Snapshot(items,fee.AmountTwd,discount,fee.Version);
 }
 static JsonObject Snapshot(JsonArray items,long shipping,bool discount,string feeVersion) {
  var source=items.Select(x=>x!.AsObject()).OrderBy(x=>x.Text("skuId"),StringComparer.Ordinal).ToArray();
  AllocatedPrice price;
  try { price=PriceAllocation.Calculate(source.Select(x=>new PricedSku(x.Text("skuId"),(int)x.Number("quantity"),x.Number("unitPriceTwd"))),shipping,discount); }
  catch(ArgumentException) { throw new KohamiException(422,"VALIDATION_ERROR"); }
  var lines=new JsonArray();for(var i=0;i<source.Length;i++){var x=source[i];var allocated=price.Lines[i];var units=new JsonArray();for(var n=0;n<allocated.UnitPayments.Count;n++)units.Add(Obj(("unitId",null),("ordinal",n),("paidTwd",allocated.UnitPayments[n])));lines.Add(Obj(("orderLineId",null),("skuId",x.Text("skuId")),("productSlug",x.Text("productSlug")),("name",x["name"]!.DeepClone()),("colorId",x.Text("colorId")),("garmentSize",x.Text("garmentSize")),("quantity",allocated.Quantity),("unitPriceTwd",allocated.UnitPriceTwd),("discountTwd",allocated.DiscountTwd),("paidTwd",allocated.PaidTwd),("units",units)));}
  return Obj(("currency","TWD"),("merchandiseTwd",price.MerchandiseTwd),("discountTwd",price.DiscountTwd),("shippingTwd",price.ShippingTwd),("totalTwd",price.TotalTwd),("feeVersion",feeVersion),("allocationVersion","largest_remainder_v1"),("lines",lines),("priceVersion","prices_demo_v1"),("promotionVersion",discount?"DEMO15_v1":"none_demo_v1"),("localeAtCheckout","zh-TW"));
 }
}
