using CloudConnectorGui.Core;

namespace CloudConnectorGui.App;

public sealed class GuiConfiguration
{
    public string Address { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string Proxy { get; init; } = string.Empty;

    public bool Verbose { get; init; }

    public string SelfUpdateCheckInterval { get; init; } = SelfUpdateIntervals.Daily;

    public DateOnly? LastSelfUpdateCheck { get; init; }

    public IReadOnlyList<Endpoint> Endpoints { get; init; } = [];

    public LaunchOptions ToLaunchOptions()
    {
        return new LaunchOptions(Address, Token, Endpoints, Proxy, Verbose);
    }

    public static GuiConfiguration FromLaunchOptions(LaunchOptions options)
    {
        return FromLaunchOptions(options, current: null);
    }

    public static GuiConfiguration FromLaunchOptions(LaunchOptions options, GuiConfiguration? current)
    {
        return new GuiConfiguration
        {
            Address = options.Address,
            Token = options.Token,
            Proxy = options.Proxy ?? string.Empty,
            Verbose = options.Verbose,
            SelfUpdateCheckInterval = current?.SelfUpdateCheckInterval ?? SelfUpdateIntervals.Daily,
            LastSelfUpdateCheck = current?.LastSelfUpdateCheck,
            Endpoints = options.Endpoints
        };
    }
}
