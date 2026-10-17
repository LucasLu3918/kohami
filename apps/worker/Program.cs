using Kohami.M0.Core;
using Kohami.M0.Commerce;
using Kohami.M0.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var settings = M0Settings.Load(Environment.GetEnvironmentVariable("M0_CONFIG")
    ?? throw new InvalidOperationException("M0_CONFIG_REQUIRED"));
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<M0Sql>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CommerceStore>();
builder.Services.AddSingleton<MockWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MockWorker>());
builder.Services.AddSingleton<AccountInactivityWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AccountInactivityWorker>());
await builder.Build().RunAsync();
