using CloudConnectorGui.Core;

namespace CloudConnectorGui.App;

public sealed class MainWindowController
{
    private readonly ConnectorProcess connector;
    private readonly CloudConnectorBinaryManager binaryManager;
    private readonly SelfUpdateManager selfUpdateManager;
    private readonly GuiConfigurationStore configurationStore;

    public MainWindowController()
        : this(new ConnectorProcess(), new CloudConnectorBinaryManager(), new SelfUpdateManager(), new GuiConfigurationStore())
    {
    }

    public MainWindowController(
        ConnectorProcess connector,
        CloudConnectorBinaryManager binaryManager,
        SelfUpdateManager selfUpdateManager,
        GuiConfigurationStore configurationStore)
    {
        this.connector = connector;
        this.binaryManager = binaryManager;
        this.selfUpdateManager = selfUpdateManager;
        this.configurationStore = configurationStore;
        connector.OutputReceived += line => LogRequested?.Invoke(line);
        connector.Exited += exitCode => ConnectorExited?.Invoke(exitCode);
    }

    public event Action<string>? LogRequested;

    public event Action<int>? ConnectorExited;

    public bool IsConnectorRunning => connector.IsRunning;

    public string ConnectorExecutablePath => binaryManager.ExecutablePath;

    public string? InstalledConnectorVersion => binaryManager.InstalledVersion;

    public GuiConfiguration LoadConfiguration()
    {
        return configurationStore.Load();
    }

    public void SaveConfiguration(MainWindowState state)
    {
        configurationStore.Save(state.ToConfiguration());
    }

    public IReadOnlyList<string> ValidateLaunchOptions(MainWindowState state)
    {
        return ConnectorValidator.Validate(state.ToLaunchOptions());
    }

    public string GetDisplayCommand(MainWindowState state)
    {
        return ConnectorArguments.ToDisplayCommand(Path.GetFileName(binaryManager.ExecutablePath), state.ToLaunchOptions());
    }

    public void StartConnector(MainWindowState state)
    {
        var options = state.ToLaunchOptions();
        configurationStore.Save(state.ToConfiguration());
        connector.Start(binaryManager.ExecutablePath, options);
        state.SetRunning(true);
    }

    public async Task StopConnectorAsync(MainWindowState state)
    {
        await connector.StopAsync().ConfigureAwait(false);
        state.SetRunning(false);
    }

    public async Task<InstallResult> InstallOrUpdateBinaryAsync(
        bool force,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return force
            ? await binaryManager.InstallLatestAsync(progress, cancellationToken).ConfigureAwait(false)
            : await binaryManager.EnsureInstalledAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshBinaryVersionAsync(MainWindowState state, CancellationToken cancellationToken = default)
    {
        var status = await binaryManager.GetVersionStatusAsync(cancellationToken).ConfigureAwait(false);
        var current = status.CurrentVersion ?? "not installed";
        var latest = status.LatestVersion;
        var suffix = status.IsLatest ? "up to date" : "update available";
        state.BinaryVersionText = $"current {current} / latest {latest} ({suffix})";
    }

    public void SetBinaryVersionUnavailable(MainWindowState state)
    {
        var current = binaryManager.InstalledVersion ?? "not installed";
        state.BinaryVersionText = $"current {current} / latest unavailable";
    }

    public async Task CheckSelfUpdateAsync(MainWindowState state, CancellationToken cancellationToken = default)
    {
        if (!IsSelfUpdateCheckDue(state.ToConfiguration()))
        {
            return;
        }

        var status = await selfUpdateManager.GetUpdateStatusAsync(cancellationToken).ConfigureAwait(false);
        state.LastSelfUpdateCheck = DateOnly.FromDateTime(DateTime.UtcNow);
        configurationStore.Save(state.ToConfiguration());

        if (status.IsUpdateAvailable)
        {
            state.ShowSelfUpdate(status);
        }
    }

    public async Task ApplySelfUpdateAsync(
        MainWindowState state,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (state.AvailableSelfUpdate is null)
        {
            return;
        }

        configurationStore.Save(state.ToConfiguration());
        await selfUpdateManager.ApplyUpdateAndRestartAsync(state.AvailableSelfUpdate, progress, cancellationToken).ConfigureAwait(false);
    }

    public static bool IsSelfUpdateCheckDue(GuiConfiguration configuration)
    {
        if (configuration.SelfUpdateCheckInterval == SelfUpdateIntervals.Off)
        {
            return false;
        }

        if (configuration.LastSelfUpdateCheck is null)
        {
            return true;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var nextCheck = configuration.SelfUpdateCheckInterval switch
        {
            SelfUpdateIntervals.Weekly => configuration.LastSelfUpdateCheck.Value.AddDays(7),
            SelfUpdateIntervals.Monthly => configuration.LastSelfUpdateCheck.Value.AddMonths(1),
            _ => configuration.LastSelfUpdateCheck.Value.AddDays(1)
        };

        return today >= nextCheck;
    }
}
