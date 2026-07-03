namespace CloudConnectorGui.App;

public sealed record SelfUpdateStatus(
    string CurrentVersion,
    string LatestVersion,
    bool IsUpdateAvailable,
    GitHubReleaseAsset? Asset);
