using System.Text.Json;
using Kohami.Core;

namespace Kohami.Core.Tests;

public sealed class RuntimeSettingsTests
{
    [Theory]
    [InlineData("https://localhost:3000")]
    [InlineData("https://localhost:3003")]
    [InlineData("https://localhost:3999")]
    public void Load_accepts_exact_https_localhost_preview_origins(string origin)
    {
        using var config = new TemporaryRuntimeConfig(origin);

        var settings = RuntimeSettings.Load(config.Path);

        Assert.Equal(origin, settings.Origin);
    }

    [Theory]
    [InlineData("http://localhost:3003")]
    [InlineData("https://127.0.0.1:3003")]
    [InlineData("https://example.test:3003")]
    [InlineData("https://localhost:2999")]
    [InlineData("https://localhost:4000")]
    [InlineData("https://localhost:3003/path")]
    [InlineData("https://localhost:3003/")]
    [InlineData("https://localhost:3003?query=1")]
    public void Load_rejects_noncanonical_or_nonlocal_origins(string origin)
    {
        using var config = new TemporaryRuntimeConfig(origin);

        var error = Assert.Throws<InvalidOperationException>(() => RuntimeSettings.Load(config.Path));

        Assert.Equal("ENVIRONMENT_SCOPE_INVALID", error.Message);
    }

    private sealed class TemporaryRuntimeConfig : IDisposable
    {
        private readonly string _directory = Directory.CreateTempSubdirectory("kohami-settings-test-").FullName;
        public string Path { get; }

        public TemporaryRuntimeConfig(string origin)
        {
            Path = System.IO.Path.Combine(_directory, "runtime.json");
            File.WriteAllText(Path, JsonSerializer.Serialize(new
            {
                datasetId = "kohami_local",
                origin,
                identityNamespace = "kohami.identity_access.v1"
            }));
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
