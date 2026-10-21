using Kohami.Core;
using System.Text.Json;
using Microsoft.Data.SqlClient;
namespace Kohami.Commerce;
public sealed record CommerceScope(string Kind,string Id);
public sealed class CommerceResourceResolver(SqlRuntime sql) {
 // Root adapts this to the identity IResourceScopeResolver; no dependency from Commerce to Security.
 // Authorization already owns the exclusive security applock. Opening a CommerceStore
 // transaction here would wait for a shared applock on this same request and deadlock.
 public async Task<IReadOnlyList<CommerceScope>> ResolveScopesAsync(string kind,string id,CancellationToken ct=default){
  if(kind=="Report")return id=="operations"?[new("Dataset",CommerceStore.Dataset),new("Report",id)]:[];
  if(kind=="Product"){
   await using var catalog=await sql.OpenAsync(ct);
   using var query=SqlRuntime.Command(catalog,"SELECT ProductId FROM identity_access.CatalogProducts WHERE Dataset=@dataset AND ProductId=@id",null,("@dataset",CommerceStore.Dataset),("@id",id));
   return await query.ExecuteScalarAsync(ct) is string product?[new("Dataset",CommerceStore.Dataset),new("Product",product)]:[];
  }
  var docKind=kind=="Case"?"RefundCase":kind;
  await using var c=await sql.OpenAsync(ct);
  using var cmd=SqlRuntime.Command(c,"SELECT Body FROM commerce.Documents WHERE Dataset=@dataset AND Kind=@kind AND Id=@id",null,("@dataset",CommerceStore.Dataset),("@kind",docKind),("@id",id));
  var raw=await cmd.ExecuteScalarAsync(ct);if(raw is not string json)return [];
  using var d=JsonDocument.Parse(json);var body=d.RootElement;
  string Text(string name)=>body.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
  var scopes=new List<CommerceScope>{new("Dataset",CommerceStore.Dataset),new(kind,id)};
  if(kind is "RefundCase" or "Case") {scopes.Add(new("Case",id));scopes.Add(new("RefundCase",id));scopes.Add(new("Order",Text("orderId")));}
  else if(kind=="RefundOperation"){scopes.Add(new("Case",Text("caseId")));scopes.Add(new("RefundCase",Text("caseId")));scopes.Add(new("Order",Text("orderId")));}
  else if(kind=="Checkout")scopes.Add(new("Order",Text("orderId")));
  else if(kind is "Shipment" or "Return" or "DemoReceipt" or "ReceiptAdjustment" or "InventoryMovement")scopes.Add(new("Order",Text("orderId")));
  return scopes;
 }
}
