using Kohami.M0.Core;
namespace Kohami.M0.Commerce;
public sealed record CommerceScope(string Kind,string Id);
public sealed class CommerceResourceResolver(CommerceStore store) {
 // Root adapts this to the identity IM0ResourceScopeResolver; no dependency from Commerce to Security.
 public async Task<IReadOnlyList<CommerceScope>> ResolveScopesAsync(string kind,string id,CancellationToken ct=default)=>await store.Transaction(async tx=>{
  var d=await tx.Find(kind=="Case"?"RefundCase":kind,id);if(d is null)return (IReadOnlyList<CommerceScope>)Array.Empty<CommerceScope>();
  var scopes=new List<CommerceScope>{new("Dataset",CommerceStore.Dataset),new(kind,id)};
  if(kind is "RefundCase" or "Case") {scopes.Add(new("Case",id));scopes.Add(new("RefundCase",id));scopes.Add(new("Order",d.Body.Text("orderId")));}
  else if(kind=="RefundOperation"){scopes.Add(new("Case",d.Body.Text("caseId")));scopes.Add(new("RefundCase",d.Body.Text("caseId")));scopes.Add(new("Order",d.Body.Text("orderId")));}
  else if(kind=="Checkout")scopes.Add(new("Order",d.Body.Text("orderId")));
  return scopes;
 },ct);
}
