using Velopack;

namespace CloudConnectorGui.App;

public sealed record SelfUpdateStatus(
    string CurrentVersion,
    string LatestVersion,
    bool IsUpdateAvailable,
    UpdateInfo? UpdateInfo);
