using System.Collections.ObjectModel;
using System.Windows.Input;

using CloudConnectorWindowsGui.Core;
using CloudConnectorWindowsGui.Services;

namespace CloudConnectorWindowsGui.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ConnectorProcess connector;
    private readonly CloudConnectorBinaryManager binaryManager;

    private string address = string.Empty;
    private string token = string.Empty;
    private string proxy = string.Empty;
    private bool verbose;
    private bool isRunning;
    private bool isBusy;
    private static readonly Color RunningColor = Color.FromArgb("#3BB273");
    private static readonly Color StoppedColor = Color.FromArgb("#E5484D");

    private string statusText = "Stopped";
    private Color statusColor = StoppedColor;
    private string binaryVersionText = "Connector binary: not checked";

    public MainViewModel(ConnectorProcess connector, CloudConnectorBinaryManager binaryManager)
    {
        this.connector = connector;
        this.binaryManager = binaryManager;

        connector.OutputReceived += line => MainThread.BeginInvokeOnMainThread(() => AppendLog(line));
        connector.Exited += exitCode => MainThread.BeginInvokeOnMainThread(() =>
        {
            AppendLog($"outsystemscc exited with code {exitCode}");
            SetRunningState(false);
        });

        StartCommand = new Command(async () => await StartConnectorAsync(), () => !IsRunning && !IsBusy);
        StopCommand = new Command(async () => await StopConnectorAsync(), () => IsRunning);
        AddEndpointCommand = new Command(AddEndpoint, () => !IsRunning);
        RemoveEndpointCommand = new Command<EndpointRowViewModel>(row => RemoveEndpoint(row), _ => !IsRunning);
        UpdateBinaryCommand = new Command(async () => await InstallOrUpdateBinaryAsync(force: true), () => !IsRunning && !IsBusy);

        Endpoints = new ObservableCollection<EndpointRowViewModel>();
        AddEndpoint();
    }

    public ObservableCollection<EndpointRowViewModel> Endpoints { get; }

    public ObservableCollection<string> LogLines { get; } = new();

    public ICommand StartCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand AddEndpointCommand { get; }

    public ICommand RemoveEndpointCommand { get; }

    public ICommand UpdateBinaryCommand { get; }

    public string Address
    {
        get => address;
        set => SetProperty(ref address, value);
    }

    public string Token
    {
        get => token;
        set => SetProperty(ref token, value);
    }

    public string Proxy
    {
        get => proxy;
        set => SetProperty(ref proxy, value);
    }

    public bool Verbose
    {
        get => verbose;
        set => SetProperty(ref verbose, value);
    }

    public bool IsRunning
    {
        get => isRunning;
        private set
        {
            if (SetProperty(ref isRunning, value))
            {
                RaiseAllCanExecuteChanged();
                foreach (var endpoint in Endpoints)
                {
                    endpoint.IsReadOnly = value;
                }
            }
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                RaiseAllCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public Color StatusColor
    {
        get => statusColor;
        private set => SetProperty(ref statusColor, value);
    }

    public string BinaryVersionText
    {
        get => binaryVersionText;
        private set => SetProperty(ref binaryVersionText, value);
    }

    public async Task InitializeAsync()
    {
        await RefreshBinaryVersionAsync().ConfigureAwait(true);
        await InstallOrUpdateBinaryAsync(force: false).ConfigureAwait(true);
    }

    public async Task<bool> StopIfRunningAsync()
    {
        if (!connector.IsRunning)
        {
            return false;
        }

        await StopConnectorAsync().ConfigureAwait(true);
        return true;
    }

    private void AddEndpoint()
    {
        Endpoints.Add(new EndpointRowViewModel { IsReadOnly = IsRunning });
    }

    private void RemoveEndpoint(EndpointRowViewModel? row)
    {
        if (row is not null)
        {
            Endpoints.Remove(row);
        }
    }

    private async Task StartConnectorAsync()
    {
        var options = ReadOptions();
        var validationErrors = ConnectorValidator.Validate(options);
        if (validationErrors.Count > 0)
        {
            await ShowAlertAsync("Cannot start connector", string.Join(Environment.NewLine, validationErrors)).ConfigureAwait(true);
            return;
        }

        try
        {
            if (!File.Exists(binaryManager.ExecutablePath))
            {
                await ShowAlertAsync("Cannot start connector", "The connector binary is not installed yet. Use Download / Update Binary first.").ConfigureAwait(true);
                return;
            }

            ClearLog();
            AppendLog(ConnectorArguments.ToDisplayCommand(binaryManager.ExecutableName, options));
            connector.Start(binaryManager.ExecutablePath, options);
            SetRunningState(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            await ShowAlertAsync("Cannot start connector", ex.Message).ConfigureAwait(true);
            SetRunningState(false);
        }
    }

    private async Task StopConnectorAsync()
    {
        AppendLog("Stopping outsystemscc...");
        await connector.StopAsync().ConfigureAwait(true);
        SetRunningState(false);
    }

    private LaunchOptions ReadOptions()
    {
        return new LaunchOptions(
            Address,
            Token,
            Endpoints.Select(row => row.ToEndpoint()).ToList(),
            Proxy,
            Verbose);
    }

    private void SetRunningState(bool running)
    {
        IsRunning = running;
        StatusText = running ? "Running" : "Stopped";
        StatusColor = running ? RunningColor : StoppedColor;
    }

    private async Task InstallOrUpdateBinaryAsync(bool force)
    {
        if (connector.IsRunning)
        {
            await ShowAlertAsync("Connector is running", "Stop the connector before updating the binary.").ConfigureAwait(true);
            return;
        }

        IsBusy = true;
        var previousStatus = StatusText;
        try
        {
            var progress = new Progress<string>(message =>
            {
                StatusText = message;
                AppendLog(message);
            });

            var result = force
                ? await binaryManager.InstallLatestAsync(progress).ConfigureAwait(true)
                : await binaryManager.EnsureInstalledAsync(progress).ConfigureAwait(true);

            if (result.Installed)
            {
                AppendLog($"Installed outsystemscc {result.Version}.");
            }

            await RefreshBinaryVersionAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            AppendLog($"Binary install failed: {ex.Message}");
            if (force)
            {
                await ShowAlertAsync("Cannot install connector binary", ex.Message).ConfigureAwait(true);
            }
        }
        finally
        {
            StatusText = previousStatus;
            IsBusy = false;
        }
    }

    private async Task RefreshBinaryVersionAsync()
    {
        try
        {
            var status = await binaryManager.GetVersionStatusAsync().ConfigureAwait(true);
            var current = status.CurrentVersion ?? "not installed";
            var latest = status.LatestVersion;
            var suffix = status.IsLatest ? "up to date" : "update available";
            BinaryVersionText = $"Connector binary: current {current} / latest {latest} ({suffix})";
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            var current = binaryManager.InstalledVersion ?? "not installed";
            BinaryVersionText = $"Connector binary: current {current} / latest unavailable";
            AppendLog($"Version check failed: {ex.Message}");
        }
    }

    private void ClearLog()
    {
        LogLines.Clear();
    }

    private void AppendLog(string line)
    {
        LogLines.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
    }

    private static Task ShowAlertAsync(string title, string message)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        return page is null ? Task.CompletedTask : page.DisplayAlert(title, message, "OK");
    }

    private void RaiseAllCanExecuteChanged()
    {
        ((Command)StartCommand).ChangeCanExecute();
        ((Command)StopCommand).ChangeCanExecute();
        ((Command)AddEndpointCommand).ChangeCanExecute();
        ((Command)RemoveEndpointCommand).ChangeCanExecute();
        ((Command)UpdateBinaryCommand).ChangeCanExecute();
    }
}
