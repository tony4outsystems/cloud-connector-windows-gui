using CloudConnectorGui.Core;

namespace CloudConnectorGui.App;

public sealed class MainWindowState
{
    public string Address { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string Proxy { get; set; } = string.Empty;

    public bool Verbose { get; set; }

    public string SelfUpdateCheckInterval { get; set; } = SelfUpdateIntervals.Daily;

    public DateOnly? LastSelfUpdateCheck { get; set; }

    public List<Endpoint> Endpoints { get; } = [];

    public bool IsRunning { get; set; }

    public string StatusText { get; set; } = "Stopped";

    public string BinaryVersionText { get; set; } = "Connector binary: not checked";

    public SelfUpdateStatus? AvailableSelfUpdate { get; set; }

    public string SelfUpdateBannerText { get; set; } = string.Empty;

    public bool IsSelfUpdateBannerVisible { get; set; }

    public bool CanStart => !IsRunning;

    public bool CanStop => IsRunning;

    public bool CanUpdateBinary => !IsRunning;

    public bool CanEditConfiguration => !IsRunning;

    public bool CanApplySelfUpdate => !IsRunning && AvailableSelfUpdate is not null;

    public LaunchOptions ToLaunchOptions()
    {
        return new LaunchOptions(Address, Token, Endpoints, Proxy, Verbose);
    }

    public GuiConfiguration ToConfiguration()
    {
        return GuiConfiguration.FromLaunchOptions(ToLaunchOptions(), new GuiConfiguration
        {
            SelfUpdateCheckInterval = SelfUpdateCheckInterval,
            LastSelfUpdateCheck = LastSelfUpdateCheck
        });
    }

    public void ApplyConfiguration(GuiConfiguration configuration)
    {
        Address = configuration.Address;
        Token = configuration.Token;
        Proxy = configuration.Proxy;
        Verbose = configuration.Verbose;
        SelfUpdateCheckInterval = configuration.SelfUpdateCheckInterval;
        LastSelfUpdateCheck = configuration.LastSelfUpdateCheck;
        Endpoints.Clear();
        Endpoints.AddRange(configuration.Endpoints);
    }

    public void SetRunning(bool running)
    {
        IsRunning = running;
        StatusText = running ? "Running" : "Stopped";
    }

    public void ShowSelfUpdate(SelfUpdateStatus status)
    {
        AvailableSelfUpdate = status;
        SelfUpdateBannerText = $"GUI update available: {status.CurrentVersion} -> {status.LatestVersion}";
        IsSelfUpdateBannerVisible = true;
    }

    public void HideSelfUpdate()
    {
        IsSelfUpdateBannerVisible = false;
    }
}
