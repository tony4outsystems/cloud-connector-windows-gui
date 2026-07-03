using Velopack;
using Velopack.Sources;

namespace CloudConnectorGui.App;

public sealed class SelfUpdateManager
{
    private const string RepositoryOwner = "tony4outsystems";
    private const string RepositoryName = "cloud-connector-gui";

    private readonly UpdateManager updateManager;

    public SelfUpdateManager()
        : this(new UpdateManager(new GithubSource($"https://github.com/{RepositoryOwner}/{RepositoryName}", null, prerelease: false)))
    {
    }

    public SelfUpdateManager(UpdateManager updateManager)
    {
        this.updateManager = updateManager;
    }

    public string CurrentVersion => updateManager.CurrentVersion?.ToString() ?? "dev";

    public async Task<SelfUpdateStatus> GetUpdateStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!updateManager.IsInstalled)
        {
            return new SelfUpdateStatus(CurrentVersion, CurrentVersion, false, null);
        }

        var updateInfo = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
        return updateInfo is null
            ? new SelfUpdateStatus(CurrentVersion, CurrentVersion, false, null)
            : new SelfUpdateStatus(CurrentVersion, updateInfo.TargetFullRelease.Version.ToString(), true, updateInfo);
    }

    public async Task ApplyUpdateAndRestartAsync(SelfUpdateStatus status, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!status.IsUpdateAvailable || status.UpdateInfo is null)
        {
            return;
        }

        progress?.Report($"Downloading cloud-connector-gui {status.LatestVersion}...");
        await updateManager.DownloadUpdatesAsync(status.UpdateInfo, cancelToken: cancellationToken).ConfigureAwait(false);

        progress?.Report("Restarting to finish GUI update...");
        updateManager.ApplyUpdatesAndRestart(status.UpdateInfo.TargetFullRelease);
    }
}
