using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Kohami.M0.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace Kohami.M0.Commerce;
public static class CommerceEndpoints {
 public static IServiceCollection AddM0Commerce(this IServiceCollection s){s.AddSingleton<CommerceStore>();s.AddSingleton<CommerceResourceResolver>();s.AddSingleton<CommerceService>();s.AddSingleton<MockWorker>();return s;}
 // These additive M3/M1 mock endpoints use the Commerce module's strict request validators.
 // The adopted M0 Core.openapi.json remains limited to its approved core34 surface.
 public static bool UsesModuleContract(string method,string path)=>
  method=="GET"&&path=="/api/v1/admin/reconciliation-report"||
  method=="GET"&&path=="/api/v1/admin/reconciliation-notifications"||
  method=="POST"&&Regex.IsMatch(path,@"^/api/v1/admin/reconciliation-cases/[A-Za-z0-9_-]+/resolutions$")||
  method=="POST"&&Regex.IsMatch(path,@"^/api/v1/admin/reconciliation-cases/[A-Za-z0-9_-]+/assignments$")||
  method=="PUT"&&path=="/api/v1/carts/current/promotion"||
  method=="DELETE"&&Regex.IsMatch(path,@"^/api/v1/carts/current/items/[A-Za-z0-9_-]+$")||
  method=="GET"&&Regex.IsMatch(path,@"^/api/v1/admin/stock/[A-Za-z0-9_-]+$")||
  method=="POST"&&path=="/api/v1/admin/stock-adjustments"||
  method=="GET"&&path=="/api/v1/orders"||
  method=="POST"&&Regex.IsMatch(path,@"^/api/v1/orders/[^/]+/member-claims$")||
  method=="GET"&&Regex.IsMatch(path,@"^/api/v1/orders/[^/]+/receipts$")||
  method=="POST"&&(Regex.IsMatch(path,@"^/api/v1/orders/[^/]+/returns$")||Regex.IsMatch(path,@"^/api/v1/admin/orders/[^/]+/shipments$")||Regex.IsMatch(path,@"^/api/v1/admin/shipments/[^/]+/transitions$")||Regex.IsMatch(path,@"^/api/v1/admin/returns/[^/]+/transitions$")||Regex.IsMatch(path,@"^/api/v1/admin/returns/[^/]+/inspections$"));
 public static RouteGroupBuilder MapM0Commerce(this RouteGroupBuilder g) {
  g.MapGet("/admin/stock/{skuId}",(HttpContext h,string skuId,CommerceService s)=>s.Stock(h,skuId));
  g.MapPost("/admin/stock-adjustments",(HttpContext h,CommerceService s)=>s.AdjustStock(h));
  g.MapGet("/admin/reconciliation-report",(HttpContext h,CommerceService s)=>s.ReconciliationReport(h));
  g.MapGet("/admin/reconciliation-notifications",(HttpContext h,CommerceService s)=>s.ReconciliationNotifications(h));
  g.MapPost("/admin/reconciliation-cases/{id}/resolutions",(HttpContext h,string id,CommerceService s)=>s.ResolveReconciliationCase(h,id));
  g.MapPost("/admin/reconciliation-cases/{id}/assignments",(HttpContext h,string id,CommerceService s)=>s.AssignReconciliationCase(h,id));
  g.MapPost("/carts",(HttpContext h,CommerceService s)=>s.CreateCart(h));
  g.MapGet("/carts/current",(HttpContext h,CommerceService s)=>s.Cart(h));
  g.MapPut("/carts/current/items/{skuId}",(HttpContext h,string skuId,CommerceService s)=>s.CartItem(h,skuId));
  g.MapDelete("/carts/current/items/{skuId}",(HttpContext h,string skuId,CommerceService s)=>s.RemoveCartItem(h,skuId));
  g.MapPut("/carts/current/promotion",(HttpContext h,CommerceService s)=>s.SetCartPromotion(h));
  g.MapPost("/checkout-quotes",(HttpContext h,CommerceService s)=>s.Quote(h));
  g.MapPost("/checkouts",(HttpContext h,CommerceService s)=>s.Checkout(h));
  g.MapGet("/checkouts/{id}/status",(HttpContext h,string id,CommerceService s)=>s.Status(h,id));
  g.MapPost("/checkouts/{id}/payment-attempts",(HttpContext h,string id,CommerceService s)=>s.Pay(h,id));
  g.MapGet("/orders/{id}",(HttpContext h,string id,CommerceService s)=>s.Order(h,id));
  g.MapGet("/orders",(HttpContext h,CommerceService s)=>s.Orders(h));
  g.MapPost("/orders/{id}/member-claims",(HttpContext h,string id,CommerceService s)=>s.ClaimGuestOrder(h,id));
  g.MapGet("/orders/{id}/receipts",(HttpContext h,string id,CommerceService s)=>s.Receipts(h,id));
  g.MapPost("/orders/{id}/returns",(HttpContext h,string id,CommerceService s)=>s.RequestReturn(h,id));
  g.MapPost("/orders/{id}/cancellations",(HttpContext h,string id,CommerceService s)=>s.Cancel(h,id));
  g.MapPost("/admin/orders/{id}/shipments",(HttpContext h,string id,CommerceService s)=>s.CreateShipment(h,id));
  g.MapPost("/admin/shipments/{id}/transitions",(HttpContext h,string id,CommerceService s)=>s.TransitionShipment(h,id));
  g.MapPost("/admin/returns/{id}/transitions",(HttpContext h,string id,CommerceService s)=>s.TransitionReturn(h,id));
  g.MapPost("/admin/returns/{id}/inspections",(HttpContext h,string id,CommerceService s)=>s.InspectReturn(h,id));
  g.MapPost("/guest-order-verifications",(HttpContext h,CommerceService s)=>s.GuestRequest(h));
  g.MapPost("/guest-order-verifications/confirm",(HttpContext h,CommerceService s)=>s.GuestConfirm(h));
  g.MapPost("/admin/refund-cases/{id}/operations",(HttpContext h,string id,CommerceService s)=>s.Refund(h,id,"Merchandise"));
  g.MapPost("/admin/order-shipping-refund-cases/{id}/operations",(HttpContext h,string id,CommerceService s)=>s.Refund(h,id,"ShippingOnly"));
  g.MapPost("/admin/payment-compensation-cases/{id}/operations",(HttpContext h,string id,CommerceService s)=>s.Refund(h,id,"CollectionCompensation"));
  g.MapGet("/admin/refund-operations/{id}",(HttpContext h,string id,CommerceService s)=>s.ReadRefund(h,id));
  g.MapPost("/admin/refund-operations/{id}/checks",(HttpContext h,string id,CommerceService s)=>s.CheckRefund(h,id));
  g.MapPost("/admin/demo/scenarios",(HttpContext h,CommerceService s)=>s.Scenario(h));return g;
 }
}
