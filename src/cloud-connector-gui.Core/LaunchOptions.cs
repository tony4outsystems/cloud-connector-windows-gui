namespace CloudConnectorGui.Core;

public sealed record LaunchOptions(
    string Address,
    string Token,
    IReadOnlyList<Endpoint> Endpoints,
    string? Proxy = null,
    bool Verbose = false);
