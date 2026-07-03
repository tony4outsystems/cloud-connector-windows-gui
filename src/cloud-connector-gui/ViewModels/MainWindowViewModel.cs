using System.Collections.ObjectModel;
using Avalonia.Controls;
using CloudConnectorGui.App;
using CloudConnectorGui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CloudConnectorGui.Views;

namespace CloudConnectorGui.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly MainWindowController controller = new();
    private readonly MainWindowState state = new();
    private readonly string logFilePath = Path.Combine(AppContext.BaseDirectory, "cloud-connector-gui.log");
    private bool logFileErrorShown;

    public Window? OwnerWindow { get; set; }

    public bool IsMacOS { get; } = OperatingSystem.IsMacOS();

    public IReadOnlyList<string> SelfUpdateIntervalOptions { get; } = SelfUpdateIntervals.All;

    public ObservableCollection<EndpointRowViewModel> Endpoints { get; } = [];

    [ObservableProperty]
    private string address = string.Empty;

    [ObservableProperty]
    private string token = string.Empty;

    [ObservableProperty]
    private string proxy = string.Empty;

    [ObservableProperty]
    private bool verbose;

    [ObservableProperty]
    private string selfUpdateCheckInterval = SelfUpdateIntervals.Daily;

    [ObservableProperty]
    private string statusText = "Stopped";

    [ObservableProperty]
    private string binaryVersionText = "Connector binary: not checked";

    [ObservableProperty]
    private string selfUpdateBannerText = string.Empty;

    [ObservableProperty]
    private bool isSelfUpdateBannerVisible;

    [ObservableProperty]
    private string logText = string.Empty;

    [ObservableProperty]
    private bool canStart = true;

    [ObservableProperty]
    private bool canStop;

    [ObservableProperty]
    private bool canEditConfiguration = true;

    [ObservableProperty]
    private bool canUpdateBinary = true;

    [ObservableProperty]
    private bool canApplySelfUpdate;

    public MainWindowViewModel()
    {
        controller.LogRequested += line => AppendLog(line);
        controller.ConnectorExited += exitCode =>
        {
            AppendLog($"outsystemscc exited with code {exitCode}");
            state.SetRunning(false);
            RenderState();
        };
    }

    public bool IsConnectorRunning => controller.IsConnectorRunning;

    public void Initialize()
    {
        LoadConfiguration();
    }

    public async Task OnShownAsync()
    {
        await RefreshBinaryVersionAsync().ConfigureAwait(true);
        await CheckSelfUpdateAsync().ConfigureAwait(true);
    }

    public void Save()
    {
        SaveConfiguration();
    }

    private void LoadConfiguration()
    {
        try
        {
            state.ApplyConfiguration(controller.LoadConfiguration());
            ApplyStateToProperties();
            RenderState();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            AppendLog($"Configuration load failed: {ex.Message}");
        }
    }

    private void SaveConfiguration()
    {
        try
        {
            CaptureStateFromProperties();
            controller.SaveConfiguration(state);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppendLog($"Configuration save failed: {ex.Message}");
        }
    }

    private void CaptureStateFromProperties()
    {
        state.Address = Address;
        state.Token = Token;
        state.Proxy = Proxy;
        state.Verbose = Verbose;
        state.SelfUpdateCheckInterval = SelfUpdateCheckInterval;
        state.Endpoints.Clear();
        state.Endpoints.AddRange(ReadEndpoints());
    }

    private void ApplyStateToProperties()
    {
        Address = state.Address;
        Token = state.Token;
        Proxy = state.Proxy;
        Verbose = state.Verbose;
        SelfUpdateCheckInterval = state.SelfUpdateCheckInterval;

        Endpoints.Clear();
        foreach (var endpoint in state.Endpoints)
        {
            Endpoints.Add(new EndpointRowViewModel(endpoint));
        }
    }

    private IReadOnlyList<Endpoint> ReadEndpoints()
    {
        return Endpoints
            .Where(row => !row.IsEmpty)
            .Select(row => row.ToEndpoint())
            .ToList();
    }

    [RelayCommand]
    private async Task StartConnectorAsync()
    {
        CaptureStateFromProperties();
        var validationErrors = controller.ValidateLaunchOptions(state);
        if (validationErrors.Count > 0)
        {
            await ShowMessageAsync(string.Join(Environment.NewLine, validationErrors), "Cannot start connector").ConfigureAwait(true);
            return;
        }

        try
        {
            if (!File.Exists(controller.ConnectorExecutablePath))
            {
                await ShowMessageAsync("The connector binary is not installed yet. Use Download / Update Binary first.", "Cannot start connector").ConfigureAwait(true);
                return;
            }

            LogText = string.Empty;
            AppendLog(controller.GetDisplayCommand(state));
            controller.StartConnector(state);
            RenderState();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            await ShowMessageAsync(ex.Message, "Cannot start connector").ConfigureAwait(true);
            state.SetRunning(false);
            RenderState();
        }
    }

    [RelayCommand]
    private async Task StopConnectorAsync()
    {
        CanStop = false;
        AppendLog("Stopping outsystemscc...");
        await controller.StopConnectorAsync(state).ConfigureAwait(true);
        RenderState();
    }

    [RelayCommand]
    private async Task InstallOrUpdateBinaryAsync()
    {
        if (controller.IsConnectorRunning)
        {
            await ShowMessageAsync("Stop the connector before updating the binary.", "Connector is running").ConfigureAwait(true);
            return;
        }

        CanUpdateBinary = false;
        CanStart = false;
        var previousStatus = StatusText;
        try
        {
            var progress = new Progress<string>(message =>
            {
                StatusText = message;
                AppendLog(message);
            });

            var result = await controller.InstallOrUpdateBinaryAsync(force: true, progress).ConfigureAwait(true);

            if (result.Installed)
            {
                AppendLog($"Installed outsystemscc {result.Version}.");
            }

            await RefreshBinaryVersionAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            AppendLog($"Binary install failed: {ex.Message}");
            await ShowMessageAsync(ex.Message, "Cannot install connector binary").ConfigureAwait(true);
        }
        finally
        {
            StatusText = previousStatus;
            state.SetRunning(controller.IsConnectorRunning);
            RenderState();
        }
    }

    private async Task RefreshBinaryVersionAsync()
    {
        CanUpdateBinary = false;
        try
        {
            await controller.RefreshBinaryVersionAsync(state).ConfigureAwait(true);
            RenderState();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            controller.SetBinaryVersionUnavailable(state);
            RenderState();
            AppendLog($"Version check failed: {ex.Message}");
        }
        finally
        {
            CanUpdateBinary = !controller.IsConnectorRunning;
        }
    }

    private async Task CheckSelfUpdateAsync()
    {
        CaptureStateFromProperties();
        try
        {
            await controller.CheckSelfUpdateAsync(state).ConfigureAwait(true);
            RenderState();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            AppendLog($"GUI update check failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ApplySelfUpdateAsync()
    {
        if (state.AvailableSelfUpdate is null)
        {
            return;
        }

        if (controller.IsConnectorRunning)
        {
            await ShowMessageAsync("Stop the connector before updating the GUI.", "Connector is running").ConfigureAwait(true);
            return;
        }

        var previousStatus = StatusText;
        try
        {
            var progress = new Progress<string>(message =>
            {
                StatusText = message;
                AppendLog(message);
            });

            CaptureStateFromProperties();
            await controller.ApplySelfUpdateAsync(state, progress).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            AppendLog($"GUI update failed: {ex.Message}");
            await ShowMessageAsync(ex.Message, "Cannot update GUI").ConfigureAwait(true);
            StatusText = previousStatus;
            RenderState();
        }
    }

    [RelayCommand]
    private async Task OpenConfigurationAsync()
    {
        if (OwnerWindow is not null)
        {
            await ConfigurationWindow.ShowAsync(OwnerWindow, this).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task OpenAboutAsync()
    {
        if (OwnerWindow is not null)
        {
            await AboutWindow.ShowAsync(OwnerWindow).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void HideSelfUpdateBanner()
    {
        state.HideSelfUpdate();
        RenderState();
    }

    [RelayCommand]
    private void AddEndpoint()
    {
        Endpoints.Add(new EndpointRowViewModel());
    }

    [RelayCommand]
    private void RemoveEndpoint(EndpointRowViewModel? row)
    {
        if (row is not null)
        {
            Endpoints.Remove(row);
        }
    }

    private void RenderState()
    {
        CanStart = state.CanStart;
        CanStop = state.CanStop;
        StatusText = state.StatusText;
        BinaryVersionText = state.BinaryVersionText;
        CanEditConfiguration = state.CanEditConfiguration;
        CanUpdateBinary = state.CanUpdateBinary;
        CanApplySelfUpdate = state.CanApplySelfUpdate;
        SelfUpdateBannerText = state.SelfUpdateBannerText;
        IsSelfUpdateBannerVisible = state.IsSelfUpdateBannerVisible;
    }

    private void AppendLog(string line)
    {
        var timestamp = DateTime.Now;
        LogText += $"[{timestamp:HH:mm:ss}] {line}{Environment.NewLine}";

        try
        {
            File.AppendAllText(logFilePath, $"[{timestamp:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (logFileErrorShown)
            {
                return;
            }

            logFileErrorShown = true;
            LogText += $"[{DateTime.Now:HH:mm:ss}] Could not write log file {logFilePath}: {ex.Message}{Environment.NewLine}";
        }
    }

    private async Task ShowMessageAsync(string message, string title)
    {
        await MessageDialogWindow.ShowAsync(OwnerWindow, title, message).ConfigureAwait(true);
    }
}
