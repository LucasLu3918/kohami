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
 [Fact]public async Task Phase2_proof_denied(){var e=await Assert.ThrowsAsync<M0Exception>(()=>contract.ValidateRequestAsync(Request("/auth/reauthentication-challenges","{\"purpose\":\"asset.publish\",\"resourceKind\":\"AssetRevision\",\"resourceId\":\"asset_demo\",\"requestNonce\":\"nonce_1234567890123456\"}")));Assert.Equal(422,e.Status);}
 [Fact]public async Task Exact_recent_purpose_kind_required(){var e=await Assert.ThrowsAsync<M0Exception>(()=>contract.ValidateRequestAsync(Request("/auth/reauthentication-challenges","{\"purpose\":\"refund.execute\",\"resourceKind\":\"User\",\"resourceId\":\"user_demo\",\"requestNonce\":\"nonce_1234567890123456\"}")));Assert.Equal(422,e.Status);}
 [Fact]public async Task Request_payload_rewound_for_handler(){var c=Request("/auth/sessions","{\"email\":\"buyer@example.test\",\"password\":\"password_example\"}");await contract.ValidateRequestAsync(c);Assert.Equal(0,c.Request.Body.Position);}
 [Fact]public void Unknown_headers_not_identity(){var c=Request("/auth/session","{}");c.Request.Method="GET";c.Request.Headers["X-Role"]="Owner";Assert.NotNull(contract.Operation(c));}
 [Fact]public void ETag_is_opaque_and_aggregate_mismatch_rejected(){var c=new DefaultHttpContext();c.Request.Headers.IfMatch=M0Http.ETag(9);var e=Assert.Throws<M0Exception>(()=>M0Http.Match(c,10));Assert.Equal(412,e.Status);}
}
