using System.Text;
using System.Text.Json;
using Kohami.M0.Core;
using Microsoft.AspNetCore.Http;
namespace Kohami.M0.Tests;
public sealed class ContractTests {
 readonly M0Contract contract=new(Path.Combine(AppContext.BaseDirectory,"Core.openapi.json"));
 static DefaultHttpContext Request(string path,string body){var c=new DefaultHttpContext();c.Request.Path="/api/v1"+path;c.Request.Method="POST";c.Request.ContentType="application/json";c.Request.Headers["X-CSRF-TOKEN"]="csrf_token_01234567890123456789";c.Request.Body=new MemoryStream(Encoding.UTF8.GetBytes(body));return c;}
 [Theory][InlineData("{\"email\":\"buyer@example.test\",\"password\":\"password_example\"}")]
 public async Task Login_shape_accepts_declared_fields(string body){await contract.ValidateRequestAsync(Request("/auth/sessions",body));}
 [Theory][InlineData("{\"email\":\"buyer@example.test\",\"password\":\"password_example\",\"role\":\"Owner\"}")][InlineData("{\"email\":\"buyer@example.test\"}")][InlineData("{\"email\":\"buyer@example.test\",\"password\":\"password_example\",\"email\":\"other@example.test\"}")]
 public async Task Unknown_missing_duplicate_fields_rejected(string body){var e=await Assert.ThrowsAsync<M0Exception>(()=>contract.ValidateRequestAsync(Request("/auth/sessions",body)));Assert.Equal(422,e.Status);}
 [Fact]public async Task Phase2_not_registered(){var e=await Assert.ThrowsAsync<M0Exception>(()=>contract.ValidateRequestAsync(Request("/fitting/exports","{}")));Assert.Equal(404,e.Status);}
 [Fact]public async Task Phase2_route_is_not_registered(){var e=await Assert.ThrowsAsync<M0Exception>(()=>contract.ValidateRequestAsync(Request("/auth/reauthentication-challenges","{\"purpose\":\"asset.publish\",\"resourceKind\":\"AssetRevision\",\"resourceId\":\"asset_demo\",\"requestNonce\":\"nonce_1234567890123456\"}")));Assert.Equal(404,e.Status);}
 [Fact]public async Task Unapproved_surface_is_not_registered(){var e=await Assert.ThrowsAsync<M0Exception>(()=>contract.ValidateRequestAsync(Request("/auth/reauthentication-challenges","{\"purpose\":\"refund.execute\",\"resourceKind\":\"User\",\"resourceId\":\"user_demo\",\"requestNonce\":\"nonce_1234567890123456\"}")));Assert.Equal(404,e.Status);}
 [Fact]public async Task Request_payload_rewound_for_handler(){var c=Request("/auth/sessions","{\"email\":\"buyer@example.test\",\"password\":\"password_example\"}");await contract.ValidateRequestAsync(c);Assert.Equal(0,c.Request.Body.Position);}
 [Fact]public async Task Session_logout_contract_accepts_configured_preview_origin(){var c=new DefaultHttpContext();c.Request.Path="/api/v1/auth/session";c.Request.Method="DELETE";c.Request.Headers["X-CSRF-TOKEN"]="csrf_token_01234567890123456789";c.Request.Headers.Origin="https://localhost:3017";await contract.ValidateRequestAsync(c);}
 [Fact]public void Unknown_headers_not_identity(){var c=Request("/auth/session","{}");c.Request.Method="GET";c.Request.Headers["X-Role"]="Owner";Assert.NotNull(contract.Operation(c));}
 [Fact]public void Wallet_insufficient_balance_is_checkout_only(){
  using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Core.openapi.json")));
  var mapped=new List<string>();var responseEnums=new List<string>();
  foreach(var path in document.RootElement.GetProperty("paths").EnumerateObject())foreach(var method in path.Value.EnumerateObject()){
   var operation=method.Value;var operationId=operation.GetProperty("operationId").GetString()!;
   if(operation.TryGetProperty("x-error-code-map",out var errors)&&errors.TryGetProperty("409",out var codes)
      &&codes.EnumerateArray().Any(code=>code.GetString()=="WALLET_INSUFFICIENT_BALANCE"))mapped.Add(operationId);
   if(operation.TryGetProperty("responses",out var responses)&&responses.TryGetProperty("409",out var conflict)
      &&conflict.GetProperty("content").TryGetProperty("application/problem+json",out var problem)
      &&problem.GetProperty("schema").TryGetProperty("allOf",out var allOf)){
    var responseCodes=allOf[1].GetProperty("properties").GetProperty("code").GetProperty("enum");
    if(responseCodes.EnumerateArray().Any(code=>code.GetString()=="WALLET_INSUFFICIENT_BALANCE"))responseEnums.Add(operationId);
   }
  }
  var expected=new[]{"createCheckout"};
  Assert.Equal(expected,mapped.OrderBy(value=>value,StringComparer.Ordinal));
  Assert.Equal(expected,responseEnums.OrderBy(value=>value,StringComparer.Ordinal));
 }
 [Fact]public void ETag_is_opaque_and_aggregate_mismatch_rejected(){var c=new DefaultHttpContext();c.Request.Headers.IfMatch=M0Http.ETag(9);var e=Assert.Throws<M0Exception>(()=>M0Http.Match(c,10));Assert.Equal(412,e.Status);}
 [Fact]public void Declared_session_responses_are_validated(){var c=new DefaultHttpContext();c.Request.Path="/api/v1/auth/session";c.Request.Method="GET";c.Response.StatusCode=200;c.Response.ContentType="application/json";using var good=JsonDocument.Parse("{\"state\":\"Anonymous\",\"mode\":\"mock\",\"userId\":null,\"permissions\":[],\"expiresAt\":null}");Assert.True(contract.ValidResponse(c,good.RootElement));using var bad=JsonDocument.Parse("{\"state\":\"Anonymous\",\"mode\":\"mock\",\"userId\":null,\"permissions\":[\"refund.execute\"],\"expiresAt\":null}");Assert.False(contract.ValidResponse(c,bad.RootElement));using var admin=JsonDocument.Parse("{\"state\":\"AdminAuthenticated\",\"mode\":\"mock\",\"userId\":\"owner_demo\",\"permissions\":[\"refund.execute\"],\"expiresAt\":\"2026-10-17T05:00:00Z\"}");Assert.True(contract.ValidResponse(c,admin.RootElement));}
}
