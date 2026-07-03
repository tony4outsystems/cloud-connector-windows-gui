using CloudConnectorGui.App;
using CloudConnectorGui.Core;
using Xunit;

namespace CloudConnectorGui.App.Tests;

public sealed class MainWindowStateTests
{
    [Fact]
    public void ApplyConfigurationCopiesLaunchAndUpdateState()
    {
        var state = new MainWindowState();
        var lastCheck = new DateOnly(2026, 7, 3);

        state.ApplyConfiguration(new GuiConfiguration
        {
            Address = "gateway",
            Token = "token",
            Proxy = "http://proxy:8080",
            Verbose = true,
            SelfUpdateCheckInterval = SelfUpdateIntervals.Weekly,
            LastSelfUpdateCheck = lastCheck,
            Endpoints = [new Endpoint("8081", "api.internal", "443")]
        });

        Assert.Equal("gateway", state.Address);
        Assert.Equal("token", state.Token);
        Assert.Equal("http://proxy:8080", state.Proxy);
        Assert.True(state.Verbose);
        Assert.Equal(SelfUpdateIntervals.Weekly, state.SelfUpdateCheckInterval);
        Assert.Equal(lastCheck, state.LastSelfUpdateCheck);
        Assert.Equal([new Endpoint("8081", "api.internal", "443")], state.Endpoints);
    }

    [Fact]
    public void ToConfigurationPreservesSelfUpdateFields()
    {
        var state = new MainWindowState
        {
            Address = "gateway",
            Token = "token",
            Proxy = "http://proxy:8080",
            Verbose = true,
            SelfUpdateCheckInterval = SelfUpdateIntervals.Monthly,
            LastSelfUpdateCheck = new DateOnly(2026, 7, 3)
        };
        state.Endpoints.Add(new Endpoint("8081", "api.internal", "443"));

        var configuration = state.ToConfiguration();

        Assert.Equal("gateway", configuration.Address);
        Assert.Equal("token", configuration.Token);
        Assert.Equal("http://proxy:8080", configuration.Proxy);
        Assert.True(configuration.Verbose);
        Assert.Equal(SelfUpdateIntervals.Monthly, configuration.SelfUpdateCheckInterval);
        Assert.Equal(new DateOnly(2026, 7, 3), configuration.LastSelfUpdateCheck);
        Assert.Equal([new Endpoint("8081", "api.internal", "443")], configuration.Endpoints);
    }
}
