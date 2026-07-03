using CloudConnectorGui.Core;
using Xunit;

namespace CloudConnectorGui.Core.Tests;

public sealed class ConnectorArgumentsTests
{
    [Fact]
    public void BuildCreatesMinimalCommandArguments()
    {
        var options = new LaunchOptions(
            "https://example.outsystems.app/sg_123",
            "abc123",
            [new Endpoint("8081", "10.0.0.4", "8393")]);

        var args = ConnectorArguments.Build(options);

        Assert.Equal(
            ["--header", "token: abc123", "https://example.outsystems.app/sg_123", "R:8081:10.0.0.4:8393"],
            args);
    }

    [Fact]
    public void BuildIncludesProxyVerboseAndMultipleEndpoints()
    {
        var options = new LaunchOptions(
            "gateway",
            "secret",
            [
                new Endpoint("8081", "api.internal", "443"),
                new Endpoint("8082", "smtp.internal", "587")
            ],
            "http://proxy:8080",
            Verbose: true);

        var args = ConnectorArguments.Build(options);

        Assert.Equal(
            ["--header", "token: secret", "--proxy", "http://proxy:8080", "-v", "gateway", "R:8081:api.internal:443", "R:8082:smtp.internal:587"],
            args);
    }

    [Fact]
    public void ToDisplayCommandQuotesWhitespace()
    {
        var options = new LaunchOptions(
            "https://example.outsystems.app/sg_123",
            "token with spaces",
            [new Endpoint("8081", "10.0.0.4", "8393")]);

        var command = ConnectorArguments.ToDisplayCommand("outsystemscc.exe", options);

        Assert.Contains("\"token: token with spaces\"", command, StringComparison.Ordinal);
    }
}
