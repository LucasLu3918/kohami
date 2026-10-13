using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
namespace Kohami.M0.Core;
public static class M0Http {
 public static string Id(string prefix)=>prefix+"_"+Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
 public static string Hash(string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
 public static string ETag(long version)=>'"'+WebEncoders.Base64UrlEncode(BitConverter.GetBytes(version))+'"';
 public static void Match(HttpContext ctx,long version){var s=ctx.Request.Headers.IfMatch.ToString();if(s.Length==0)throw new M0Exception(428,"PRECONDITION_REQUIRED");if(s!=ETag(version))throw new M0Exception(412,"VERSION_CHANGED");}
 public static string Key(HttpContext ctx){var k=ctx.Request.Headers["Idempotency-Key"].ToString();if(k.Length is <1 or >160)throw new M0Exception(428,"PRECONDITION_REQUIRED");return k;}
 public static async Task<JsonObject> BodyAsync(HttpContext ctx,CancellationToken ct=default){return await JsonNode.ParseAsync(ctx.Request.Body,cancellationToken:ct) as JsonObject??throw new M0Exception(422,"VALIDATION_ERROR");}
 public static string Text(this JsonObject value,string field)=>value[field]?.GetValue<string>()??throw new M0Exception(422,"VALIDATION_ERROR");
 public static long Number(this JsonObject value,string field)=>value[field]?.GetValue<long>()??throw new M0Exception(422,"VALIDATION_ERROR");
 public static IResult Json(HttpContext ctx,object body,int status=200,long? version=null){ctx.Response.Headers.CacheControl="no-store";if(version is not null)ctx.Response.Headers.ETag=ETag(version.Value);return Results.Json(body,statusCode:status);}
 public static string? Cookie(HttpContext ctx,M0Crypto crypto,string name){if(!ctx.Request.Cookies.TryGetValue(name,out var raw))return null;try{return crypto.Sessions.CreateProtector(name).Unprotect(raw);}catch(CryptographicException){return null;}}
 public static void Cookie(HttpContext ctx,M0Crypto crypto,string name,string value,DateTimeOffset until){ctx.Response.Cookies.Append(name,crypto.Sessions.CreateProtector(name).Protect(value),new CookieOptions{HttpOnly=true,Secure=true,SameSite=SameSiteMode.Lax,Path="/",Expires=until});}
 public static void DeleteCookie(HttpContext ctx,string name)=>ctx.Response.Cookies.Delete(name,new CookieOptions{Secure=true,HttpOnly=true,SameSite=SameSiteMode.Lax,Path="/"});
}
