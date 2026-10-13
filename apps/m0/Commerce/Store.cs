using System.Data;
using System.Text.Json.Nodes;
using Kohami.M0.Core;
using Microsoft.Data.SqlClient;
namespace Kohami.M0.Commerce;

public sealed record CommerceDocument(string Kind,string Id,string Owner,long Version,JsonObject Body);
public sealed record CommerceReply(JsonObject Body,int Status=200,long? Version=null);
public sealed class CommerceStore(M0Sql sql) {
 public const string Dataset="demo_m0";
 public async Task<T> Transaction<T>(Func<CommerceTransaction,Task<T>> work,CancellationToken ct=default,bool worker=false) {
  await using var c=await sql.OpenAsync(ct,worker);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  await using(var securityCmd=M0Sql.Command(c,"DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource='kohami.m0.security',@LockMode=@mode,@LockOwner='Transaction',@LockTimeout=10000;IF @r<0 THROW 50001,'SECURITY_LOCK_TIMEOUT',1;IF NOT EXISTS(SELECT 1 FROM m0.SecurityState WHERE Id=1 AND Maintenance=0) THROW 50002,'MAINTENANCE_REQUIRED',1;",tx,("@mode",worker?"Exclusive":"Shared"))) await securityCmd.ExecuteNonQueryAsync(ct);
  await using(var cmd=M0Sql.Command(c,"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='kohami.m0.commerce.demo_m0',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 50001,'COMMERCE_LOCK_TIMEOUT',1;",tx)) await cmd.ExecuteNonQueryAsync(ct);
  var result=await work(new(c,tx,ct));await tx.CommitAsync(ct);return result;
 }
}
public sealed class CommerceTransaction(SqlConnection c,SqlTransaction tx,CancellationToken ct) {
 public SqlCommand Command(string text,params (string Name,object? Value)[] args)=>M0Sql.Command(c,text,tx,args);
 public async Task<int> Execute(string text,params (string Name,object? Value)[] args){await using var cmd=Command(text,args);return await cmd.ExecuteNonQueryAsync(ct);}
 public async Task<object?> Scalar(string text,params (string Name,object? Value)[] args){await using var cmd=Command(text,args);return await cmd.ExecuteScalarAsync(ct);}
 public async Task<List<object[]>> Rows(string text,params (string Name,object? Value)[] args){await using var cmd=Command(text,args);await using var r=await cmd.ExecuteReaderAsync(ct);var rows=new List<object[]>();while(await r.ReadAsync(ct)){var v=new object[r.FieldCount];r.GetValues(v);rows.Add(v);}return rows;}
 public async Task<CommerceDocument?> Find(string kind,string id){CommerceWire.Identifier(id);var rows=await Rows("SELECT OwnerId,Version,Body FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind=@k AND Id=@i",("@k",kind),("@i",id));return rows.Count==0?null:new(kind,id,(string)rows[0][0],(long)rows[0][1],JsonNode.Parse((string)rows[0][2])!.AsObject());}
 public async Task<CommerceDocument> Get(string kind,string id)=>await Find(kind,id)??throw new M0Exception(404,"RESOURCE_NOT_FOUND");
 public async Task Save(CommerceDocument d,bool fresh=false){d.Body["version"]=CommerceWire.Version(d.Kind,d.Id,d.Version);if(fresh)await Execute("INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES('demo_m0',@k,@i,@o,@v,@b)",("@k",d.Kind),("@i",d.Id),("@o",d.Owner),("@v",d.Version),("@b",d.Body.ToJsonString()));else if(await Execute("UPDATE m0commerce.Documents SET OwnerId=@o,Version=@v,Body=@b WHERE Dataset='demo_m0' AND Kind=@k AND Id=@i",("@k",d.Kind),("@i",d.Id),("@o",d.Owner),("@v",d.Version),("@b",d.Body.ToJsonString()))!=1)throw new M0Exception(404,"RESOURCE_NOT_FOUND");}
 public async Task<CommerceReply?> Replay(string actor,string scope,string key,string hash){var rows=await Rows("SELECT RequestHash,Response,StatusCode,Version FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@a AND Scope=@s AND OperationKey=@k",("@a",actor),("@s",scope),("@k",key));if(rows.Count==0)return null;if((string)rows[0][0]!=hash)throw new M0Exception(409,"KEY_CONFLICT");return new(JsonNode.Parse((string)rows[0][1])!.AsObject(),(int)rows[0][2],rows[0][3] is DBNull?null:(long)rows[0][3]);}
 public async Task Record(string actor,string scope,string key,string hash,CommerceReply r)=>await Execute("INSERT m0commerce.Idempotency(Dataset,Actor,Scope,OperationKey,RequestHash,Response,StatusCode,Version) VALUES('demo_m0',@a,@s,@k,@h,@b,@c,@v)",("@a",actor),("@s",scope),("@k",key),("@h",hash),("@b",r.Body.ToJsonString()),("@c",r.Status),("@v",r.Version));
 public async Task Job(string kind,string target,string actor,long securityVersion,string? permission,string resourceKind,string resourceId,JsonObject payload,DateTimeOffset now)=>await Execute("INSERT m0commerce.Jobs(Dataset,Id,Kind,TargetId,Actor,SecurityVersion,Permission,ResourceKind,ResourceId,State,Payload,CreatedAt) VALUES('demo_m0',@id,@k,@t,@a,@v,@p,@rk,@ri,'NotStarted',@b,@now)",("@id",M0Http.Id("job")),("@k",kind),("@t",target),("@a",actor),("@v",securityVersion),("@p",permission),("@rk",resourceKind),("@ri",resourceId),("@b",payload.ToJsonString()),("@now",now));
}
public static class CommerceWire {
 public static string RequestHash(JsonObject body){static JsonNode? Sort(JsonNode? n){if(n is JsonObject o){var result=new JsonObject();foreach(var p in o.OrderBy(x=>x.Key,StringComparer.Ordinal))result[p.Key]=Sort(p.Value);return result;}if(n is JsonArray a)return new JsonArray(a.Select(Sort).ToArray());return n?.DeepClone();}return M0Http.Hash(Sort(body)!.ToJsonString());}
 public static string Version(string kind,string id,long version)=>M0Http.Hash("demo_m0\0"+kind+"\0"+id+"\0"+version.ToString(System.Globalization.CultureInfo.InvariantCulture));
 public static JsonObject Obj(params (string Name,object? Value)[] fields){var b=new JsonObject();foreach(var f in fields)b[f.Name]=System.Text.Json.JsonSerializer.SerializeToNode(f.Value);return b;}
 public static void Identifier(string id){if(id.Length is <1 or >100||id.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='_'&&c!='-'))throw new M0Exception(422,"VALIDATION_ERROR");}
 public static void Fields(JsonObject b,string[] required,params string[] optional){if(required.Any(k=>b[k] is null)||b.Any(k=>!required.Contains(k.Key)&&!optional.Contains(k.Key)))throw new M0Exception(422,"VALIDATION_ERROR");foreach(var p in b){if(p.Value is JsonValue v&&v.TryGetValue<string>(out var text)){if(text.Length>4096)throw new M0Exception(422,"VALIDATION_ERROR");if(p.Key.EndsWith("Id",StringComparison.Ordinal))Identifier(text);}}}
 public static void Expected(Microsoft.AspNetCore.Http.HttpContext h,CommerceDocument d,JsonObject? body=null,string field="expectedVersion"){var current=Version(d.Kind,d.Id,d.Version);var header=h.Request.Headers.IfMatch.ToString();if(header.Length==0)throw new M0Exception(428,"PRECONDITION_REQUIRED");if(header!='"'+current+'"')throw new M0Exception(412,"VERSION_CHANGED");if(body is not null&&body.Text(field)!=current)throw new M0Exception(412,"VERSION_CHANGED");}
 public static JsonObject Content(string text)=>Obj(("value",text),("requestedLocale","zh-TW"),("sourceLocale","zh-TW"),("revision","content_demo_v1"),("fallback",false));
 public static JsonObject Item(string sku,int quantity,long price,int stock)=>Obj(("skuId",sku),("quantity",quantity),("productSlug",sku+"_product"),("name",Content("合成商品")),("colorId","color_demo"),("colorLabel",Content("示意色")),("garmentSize","M"),("unitPriceTwd",price),("availability",stock>0?"InStock":"OutOfStock"),("reasonCodes",Array.Empty<string>()),("priceVersion","prices_demo_v1"),("stockVersion","stock_demo_v1"));
 // Largest-remainder allocation uses stable SKU order and ordinal order; money remains integer TWD.
 public static JsonObject Snapshot(JsonArray items,long shipping,bool discount) {
  var source=items.Select(x=>x!.AsObject()).OrderBy(x=>x.Text("skuId"),StringComparer.Ordinal).ToArray();
  long gross=source.Sum(x=>checked(x.Number("quantity")*x.Number("unitPriceTwd")));if(gross<=0)throw new M0Exception(422,"VALIDATION_ERROR");
  long off=discount?(long)decimal.Round(gross*.15m,0,MidpointRounding.AwayFromZero):0;
  var cuts=source.Select(x=>(long)(off*(decimal)(x.Number("quantity")*x.Number("unitPriceTwd"))/gross)).ToArray();
  var remainder=off-cuts.Sum();foreach(var i in Enumerable.Range(0,source.Length).OrderByDescending(i=>off*(decimal)(source[i].Number("quantity")*source[i].Number("unitPriceTwd"))/gross-cuts[i]).ThenBy(i=>i).Take((int)remainder))cuts[i]++;
  var lines=new JsonArray();for(var i=0;i<source.Length;i++){var x=source[i];var q=(int)x.Number("quantity");var paid=checked(q*x.Number("unitPriceTwd")-cuts[i]);var units=new JsonArray();for(var n=0;n<q;n++)units.Add(Obj(("unitId",null),("ordinal",n),("paidTwd",paid/q+(n>=q-paid%q?1:0))));lines.Add(Obj(("orderLineId",null),("skuId",x.Text("skuId")),("productSlug",x.Text("productSlug")),("name",x["name"]!.DeepClone()),("colorId",x.Text("colorId")),("garmentSize",x.Text("garmentSize")),("quantity",q),("unitPriceTwd",x.Number("unitPriceTwd")),("discountTwd",cuts[i]),("paidTwd",paid),("units",units)));}
  return Obj(("currency","TWD"),("merchandiseTwd",gross),("discountTwd",off),("shippingTwd",shipping),("totalTwd",checked(gross-off+shipping)),("feeVersion","fee_demo_v1"),("allocationVersion","largest_remainder_v1"),("lines",lines),("priceVersion","prices_demo_v1"),("promotionVersion",discount?"DEMO15_v1":"none_demo_v1"),("localeAtCheckout","zh-TW"));
 }
}
