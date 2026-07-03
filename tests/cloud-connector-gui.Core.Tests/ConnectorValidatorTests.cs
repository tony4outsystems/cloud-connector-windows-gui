using CloudConnectorGui.Core;
using Xunit;

namespace CloudConnectorGui.Core.Tests;

public sealed class ConnectorValidatorTests
{
    [Fact]
    public void ValidateAcceptsValidOptions()
    {
        var options = new LaunchOptions(
            "https://example.outsystems.app/sg_123",
            "token",
            [new Endpoint("8081", "10.0.0.4", "8393")]);

        Assert.Empty(ConnectorValidator.Validate(options));
    }

    [Fact]
    public void ValidateRejectsMissingRequiredFields()
    {
        var options = new LaunchOptions("", "", []);

        var errors = ConnectorValidator.Validate(options);

        Assert.Contains("Address is required.", errors);
        Assert.Contains("Token is required.", errors);
        Assert.Contains("At least one endpoint is required.", errors);
    }

    [Fact]
    public void ValidateRejectsDuplicateLocalPorts()
    {
        var options = new LaunchOptions(
            "gateway",
            "token",
            [
                new Endpoint("8081", "api.internal", "443"),
                new Endpoint("8081", "smtp.internal", "587")
            ]);

        Assert.Contains("Local port 8081 is duplicated.", ConnectorValidator.Validate(options));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void ValidateRejectsInvalidPorts(string port)
    {
        var options = new LaunchOptions("gateway", "token", [new Endpoint(port, "", port)]);

        var errors = ConnectorValidator.Validate(options);

        Assert.Contains("Local port must be a number from 1 to 65535.", errors);
        Assert.Contains("Remote port must be a number from 1 to 65535.", errors);
        Assert.Contains("Remote host is required.", errors);
    }
}
