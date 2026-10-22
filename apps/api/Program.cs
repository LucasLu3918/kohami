using System.Security.Cryptography;
using Kohami.Core;
using Kohami.Security;
using Kohami.Commerce;
using Kohami.Catalog;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
if(args.Contains("--worker-only")) {
 var workerSettings=RuntimeSettings.Load(Environment.GetEnvironmentVariable("KOHAMI_CONFIG")??throw new InvalidOperationException("KOHAMI_CONFIG_REQUIRED"));
 var workerBuilder=Host.CreateApplicationBuilder(args);workerBuilder.Logging.ClearProviders();
 workerBuilder.Services.AddSingleton(workerSettings);workerBuilder.Services.AddSingleton<SqlRuntime>();workerBuilder.Services.AddSingleton(TimeProvider.System);
 workerBuilder.Services.AddSingleton<CommerceStore>();workerBuilder.Services.AddSingleton<MockWorker>();workerBuilder.Services.AddHostedService(sp=>sp.GetRequiredService<MockWorker>());
 workerBuilder.Services.AddSingleton<AccountInactivityWorker>();workerBuilder.Services.AddHostedService(sp=>sp.GetRequiredService<AccountInactivityWorker>());
 await workerBuilder.Build().RunAsync();return;
}
var builder=WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders(); // No raw request, credential, SQL exception or proof logging.
var apiPortText=Environment.GetEnvironmentVariable("KOHAMI_API_PORT");
var apiPort=string.IsNullOrEmpty(apiPortText)?5080:
 int.TryParse(apiPortText,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var configuredApiPort)&&configuredApiPort is >=5080 and <=5099
  ?configuredApiPort:throw new InvalidOperationException("KOHAMI_API_PORT_INVALID");
builder.WebHost.UseUrls($"http://127.0.0.1:{apiPort}");
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=65536);
var settings=RuntimeSettings.Load(Environment.GetEnvironmentVariable("KOHAMI_CONFIG")??throw new InvalidOperationException("KOHAMI_CONFIG_REQUIRED"));
var crypto=SessionCrypto.Load(settings);var contract=new RuntimeContract(Environment.GetEnvironmentVariable("KOHAMI_CONTRACT")??"docs/contracts/runtime/Core.openapi.json");
builder.Services.AddSingleton(settings);builder.Services.AddSingleton(crypto);builder.Services.AddSingleton<SqlRuntime>();builder.Services.AddSingleton(contract);builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IDataProtectionProvider>(crypto.Sessions);
builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name="kohami.csrf";o.Cookie.HttpOnly=true;o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.SameSite=SameSiteMode.Lax;});
builder.Services.AddSecurity();
builder.Services.AddSingleton<IEmailEvidenceSink>(new PrivateEmailEvidenceSink(settings.PrivateInboxPath));
builder.Services.AddFoundationCommerce();
builder.Services.AddFoundationCatalog();
builder.Services.AddSingleton<IResourceScopeResolver,CommerceScopeAdapter>();
var app=builder.Build();
// Validate successful JSON responses against the same adopted wire contract used for requests.
app.Use(async(ctx,next)=>{
 var original=ctx.Response.Body;await using var buffer=new MemoryStream();ctx.Response.Body=buffer;await next();ctx.Response.Body=original;
 var operation=contract.Operation(ctx);
 if(operation is not null&&ctx.Response.StatusCode is >=200 and <300&&buffer.Length>0){bool valid=false;try{if(buffer.Length<=1_048_576&&ctx.Response.ContentType?.StartsWith("application/json",StringComparison.OrdinalIgnoreCase)==true){using var payload=System.Text.Json.JsonDocument.Parse(buffer.ToArray());valid=contract.ValidResponse(ctx,payload.RootElement);}}catch(System.Text.Json.JsonException){}if(!valid){ctx.Response.Headers.Clear();ctx.Response.Headers.CacheControl="no-store";ctx.Response.Headers["X-Content-Type-Options"]="nosniff";await Problem(ctx,500,"INTERNAL_ERROR");return;}}
 buffer.Position=0;await buffer.CopyToAsync(original,ctx.RequestAborted);
});
app.Use(async(ctx,next)=>{
 ctx.Response.Headers.CacheControl="no-store";ctx.Response.Headers["X-Content-Type-Options"]="nosniff";
 try{
  if(ctx.Connection.RemoteIpAddress is not null&&!System.Net.IPAddress.IsLoopback(ctx.Connection.RemoteIpAddress))throw new KohamiException(403,"PERMISSION_CHANGED");
  if(ctx.Request.Host.Host is not ("localhost" or "127.0.0.1") || ctx.Request.Host.Port!=apiPort)throw new KohamiException(403,"PERMISSION_CHANGED");
  if(ctx.Request.Headers["X-Forwarded-Proto"].ToString()=="https")ctx.Request.Scheme="https";
  if(ctx.Request.Path.StartsWithSegments("/api/v1")){
   if(ctx.Request.Method is not ("GET" or "HEAD")){
    if(ctx.Request.Headers.Origin.ToString()!=settings.Origin)throw new KohamiException(403,"CSRF_INVALID");
    await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx);
   }
   if(contract.Operation(ctx) is not null||!(CommerceEndpoints.UsesModuleContract(ctx.Request.Method,ctx.Request.Path.Value??"")||CatalogEndpoints.UsesModuleContract(ctx.Request.Method,ctx.Request.Path.Value??"")))await contract.ValidateRequestAsync(ctx);
  }
  await next(ctx);
 }catch(AntiforgeryValidationException){await Problem(ctx,403,"CSRF_INVALID");}
 catch(KohamiException e){await Problem(ctx,e.Status,e.Code);}
 catch(Microsoft.Data.SqlClient.SqlException){await Problem(ctx,503,"SERVICE_UNAVAILABLE");}
 catch(System.Text.Json.JsonException){await Problem(ctx,422,"VALIDATION_ERROR");}
 catch(ArgumentException){await Problem(ctx,422,"VALIDATION_ERROR");}
 catch(InvalidOperationException){await Problem(ctx,503,"SERVICE_UNAVAILABLE");}
 catch(Exception){await Problem(ctx,500,"INTERNAL_ERROR");}
});
var api=app.MapGroup("/api/v1");
api.MapGet("/auth/csrf",(HttpContext ctx,IAntiforgery antiforgery)=>{
 bool validBrowser=false;try{using var payload=System.Text.Json.JsonDocument.Parse(FoundationHttp.Cookie(ctx,crypto,"kohami.browser")??"null");validBrowser=payload.RootElement.TryGetProperty("until",out var until)&&until.GetDateTimeOffset()>DateTimeOffset.UtcNow&&payload.RootElement.TryGetProperty("value",out var value)&&!string.IsNullOrEmpty(value.GetString());}catch{} if(!validBrowser){var until=DateTimeOffset.UtcNow.AddHours(8);FoundationHttp.Cookie(ctx,crypto,"kohami.browser",System.Text.Json.JsonSerializer.Serialize(new{value=FoundationHttp.Id("browser"),until}),until);}
 bool validCommand=false;try{using var payload=System.Text.Json.JsonDocument.Parse(FoundationHttp.Cookie(ctx,crypto,"kohami.command")??"null");validCommand=payload.RootElement.TryGetProperty("until",out var until)&&until.GetDateTimeOffset()>DateTimeOffset.UtcNow&&payload.RootElement.TryGetProperty("value",out var value)&&!string.IsNullOrEmpty(value.GetString());}catch{} if(!validCommand){var until=DateTimeOffset.UtcNow.AddDays(30);FoundationHttp.Cookie(ctx,crypto,"kohami.command",System.Text.Json.JsonSerializer.Serialize(new{value=FoundationHttp.Id("browser"),until}),until);}
 var token=antiforgery.GetAndStoreTokens(ctx);return FoundationHttp.Json(ctx,new{requestToken=token.RequestToken});
});
api.MapSecurity();api.MapFoundationCommerce();api.MapFoundationCatalog();
app.MapGet("/health",async(SqlRuntime db)=>{await using var c=await db.OpenAsync();return Results.Json(new{status="database-connected",mode="mock",datasetId=settings.DatasetId});});
app.MapGet("/ready",async(SqlRuntime db,CancellationToken ct)=>await SchemaContract.IsReadyAsync(db,ct)
 ?Results.Json(new{status="ready",mode="mock",datasetId=settings.DatasetId})
 :Results.Json(new{status="not-ready"},statusCode:503));
await app.RunAsync();
static async Task Problem(HttpContext ctx,int status,string code){if(ctx.Response.HasStarted)return;
 var operation=ctx.RequestServices.GetRequiredService<RuntimeContract>().Operation(ctx);
 if(operation is not null && operation.Value.TryGetProperty("x-error-code-map",out var map) && map.TryGetProperty(status.ToString(System.Globalization.CultureInfo.InvariantCulture),out var allowed) && !allowed.EnumerateArray().Any(x=>x.GetString()==code))code=allowed[0].GetString()!;ctx.Response.StatusCode=status;ctx.Response.ContentType="application/problem+json";await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new{type="about:blank",title="無法完成操作",status,code,traceId=Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12))}));}
public partial class Program { }

sealed class CommerceScopeAdapter(CommerceResourceResolver resolver):IResourceScopeResolver { public async Task<IReadOnlyList<ResourceScope>> ResolveAsync(string kind,string id,CancellationToken ct)=> (await resolver.ResolveScopesAsync(kind,id,ct)).Select(x=>new ResourceScope(x.Kind,x.Id)).ToArray(); }
