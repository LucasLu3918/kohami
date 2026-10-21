using Kohami.Core;
using Kohami.Commerce;
using Kohami.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var settings = RuntimeSettings.Load(Environment.GetEnvironmentVariable("KOHAMI_CONFIG")
    ?? throw new InvalidOperationException("KOHAMI_CONFIG_REQUIRED"));
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<SqlRuntime>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CommerceStore>();
builder.Services.AddSingleton<MockWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MockWorker>());
builder.Services.AddSingleton<AccountInactivityWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AccountInactivityWorker>());
await builder.Build().RunAsync();
