using CloudConnectorWindowsGui.Core;

namespace CloudConnectorWindowsGui;

internal sealed class GuiConfiguration
{
    public string Address { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string Proxy { get; init; } = string.Empty;

    public bool Verbose { get; init; }

    public string SelfUpdateCheckInterval { get; init; } = "daily";

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
            SelfUpdateCheckInterval = current?.SelfUpdateCheckInterval ?? "daily",
            LastSelfUpdateCheck = current?.LastSelfUpdateCheck,
            Endpoints = options.Endpoints
        };
    }
}
