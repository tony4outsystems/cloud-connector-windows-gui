using CloudConnectorWindowsGui.Core;

namespace CloudConnectorWindowsGui;

internal sealed class MainForm : Form
{
    private const int MinimumEndpointsGridHeight = 220;
    private const int MinimumLogHeight = 240;

    private readonly TextBox addressTextBox = new();
    private readonly TextBox tokenTextBox = new();
    private readonly TextBox proxyTextBox = new();
    private readonly CheckBox verboseCheckBox = new();
    private readonly ComboBox autoUpdateComboBox = new();
    private readonly DataGridView endpointsGrid = new();
    private readonly Button startButton = new();
    private readonly Button stopButton = new();
    private readonly Button updateBinaryButton = new();
    private readonly Label binaryVersionLabel = new();
    private readonly Label statusLabel = new();
    private readonly TextBox logTextBox = new();
    private readonly ConnectorProcess connector = new();
    private readonly CloudConnectorBinaryManager binaryManager = new();
    private readonly GuiConfigurationStore configurationStore = new();
    private readonly TableLayoutPanel root = new();
    private readonly string logFilePath = Path.Combine(
        Path.GetDirectoryName(Application.ExecutablePath) ?? AppContext.BaseDirectory,
        "cloud-connector-windows-gui.log");
    private bool logFileErrorShown;
    private DateOnly? lastUpdateCheck;

    public MainForm()
    {
        Text = "OutSystems Cloud Connector";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        Size = new Size(1000, 840);
        MinimumSize = new Size(920, 800);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildLayout();
        WireEvents();
        SetRunningState(false);

        Load += (_, _) =>
        {
            LoadConfiguration();
            ApplyMinimumSize();
        };
    }

    private void ApplyMinimumSize()
    {
        var requiredContentHeight = root.GetPreferredSize(new Size(ClientSize.Width, 0)).Height;
        var chromeHeight = Height - ClientSize.Height;
        var requiredHeight = requiredContentHeight + chromeHeight;

        MinimumSize = new Size(MinimumSize.Width, Math.Max(MinimumSize.Height, requiredHeight));
    }

    private void BuildLayout()
    {
        root.Dock = DockStyle.Fill;
        root.Padding = new Padding(16);
        root.ColumnCount = 1;
        root.RowCount = 6;
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        Controls.Add(root);

        var header = new Label
        {
            Text = "OutSystems Cloud Connector",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        root.Controls.Add(header);

        var inputs = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(inputs);

        AddLabeledControl(inputs, "Address", addressTextBox);
        AddLabeledControl(inputs, "Token", tokenTextBox);
        AddLabeledControl(inputs, "Proxy", proxyTextBox);
        AddLabeledControl(inputs, "Auto update", autoUpdateComboBox);

        tokenTextBox.UseSystemPasswordChar = true;
        proxyTextBox.PlaceholderText = "Optional HTTP CONNECT or SOCKS5 proxy";
        addressTextBox.PlaceholderText = "https://organization.outsystems.app/sg_...";
        autoUpdateComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        autoUpdateComboBox.Items.AddRange(["daily", "weekly", "monthly", "off"]);
        autoUpdateComboBox.SelectedItem = "daily";

        verboseCheckBox.Text = "Verbose logs";
        verboseCheckBox.AutoSize = true;
        verboseCheckBox.MinimumSize = new Size(0, 32);
        verboseCheckBox.Margin = new Padding(110, 8, 0, 10);
        inputs.SetColumnSpan(verboseCheckBox, 2);
        inputs.Controls.Add(verboseCheckBox);

        var binaryPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 4, 0, 12)
        };
        binaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        binaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(binaryPanel);

        updateBinaryButton.Text = "Download / Update Binary";
        ConfigureActionButton(updateBinaryButton, 190);
        binaryVersionLabel.AutoSize = true;
        binaryVersionLabel.Anchor = AnchorStyles.Left;
        binaryVersionLabel.Margin = new Padding(12, 0, 0, 0);
        binaryVersionLabel.Text = "Connector binary: not checked";

        binaryPanel.Controls.Add(updateBinaryButton, 0, 0);
        binaryPanel.Controls.Add(binaryVersionLabel, 1, 0);

        ConfigureEndpointGrid();
        root.Controls.Add(endpointsGrid);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 12, 0, 12)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(actions);

        startButton.Text = "Start";
        ConfigureActionButton(startButton, 100);
        stopButton.Text = "Stop";
        ConfigureActionButton(stopButton, 100);
        statusLabel.AutoSize = true;
        statusLabel.Anchor = AnchorStyles.Left;
        statusLabel.Margin = new Padding(16, 0, 0, 0);

        actions.Controls.Add(startButton, 0, 0);
        actions.Controls.Add(stopButton, 1, 0);
        actions.Controls.Add(statusLabel, 2, 0);

        logTextBox.Dock = DockStyle.Fill;
        logTextBox.Multiline = true;
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        logTextBox.Font = new Font(FontFamily.GenericMonospace, 9);
        logTextBox.MinimumSize = new Size(0, MinimumLogHeight);
        root.Controls.Add(logTextBox);
    }

    private static void ConfigureActionButton(Button button, int minimumWidth)
    {
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.MinimumSize = new Size(minimumWidth, 40);
        button.Padding = new Padding(10, 5, 10, 5);
        button.Margin = new Padding(3, 3, 6, 3);
    }

    private static void AddLabeledControl(TableLayoutPanel panel, string label, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 8, 7)
        });

        control.Dock = DockStyle.Top;
        control.MinimumSize = new Size(0, 32);
        control.Margin = new Padding(0, 4, 0, 4);
        panel.Controls.Add(control);
    }

    private void ConfigureEndpointGrid()
    {
        endpointsGrid.Dock = DockStyle.Fill;
        endpointsGrid.MinimumSize = new Size(0, MinimumEndpointsGridHeight);
        endpointsGrid.AllowUserToAddRows = true;
        endpointsGrid.AllowUserToDeleteRows = true;
        endpointsGrid.AutoGenerateColumns = false;
        endpointsGrid.RowHeadersWidth = 28;
        endpointsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        endpointsGrid.MultiSelect = false;
        endpointsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        endpointsGrid.RowTemplate.Height = 30;
        endpointsGrid.BackgroundColor = SystemColors.Window;
        endpointsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Local port",
            Name = "LocalPort",
            Width = 190
        });
        endpointsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Remote host",
            Name = "RemoteHost",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        endpointsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Remote port",
            Name = "RemotePort",
            Width = 130
        });
        endpointsGrid.Columns.Add(new DataGridViewButtonColumn
        {
            HeaderText = string.Empty,
            Name = "Remove",
            Text = "Remove",
            UseColumnTextForButtonValue = true,
            Width = 90
        });
        endpointsGrid.CellContentClick += EndpointsGrid_CellContentClick;
    }

    private void EndpointsGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || endpointsGrid.Columns[e.ColumnIndex].Name != "Remove")
        {
            return;
        }

        var row = endpointsGrid.Rows[e.RowIndex];
        if (row.IsNewRow)
        {
            return;
        }

        endpointsGrid.Rows.Remove(row);
    }

    private void WireEvents()
    {
        startButton.Click += (_, _) => StartConnector();
        stopButton.Click += async (_, _) => await StopConnectorAsync().ConfigureAwait(true);
        updateBinaryButton.Click += async (_, _) => await InstallOrUpdateBinaryAsync(force: true).ConfigureAwait(true);
        connector.OutputReceived += line => BeginInvoke(() => AppendLog(line));
        connector.Exited += exitCode => BeginInvoke(() =>
        {
            AppendLog($"outsystemscc exited with code {exitCode}");
            SetRunningState(false);
        });
        Shown += async (_, _) =>
        {
            await RefreshBinaryVersionAsync().ConfigureAwait(true);
            await InstallOrUpdateBinaryOnScheduleAsync().ConfigureAwait(true);
        };
        FormClosing += async (_, args) =>
        {
            SaveConfiguration();
            if (connector.IsRunning)
            {
                args.Cancel = true;
                await StopConnectorAsync().ConfigureAwait(true);
                Close();
            }
        };
    }

    private void StartConnector()
    {
        var options = ReadOptions();
        var validationErrors = ConnectorValidator.Validate(options);
        if (validationErrors.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, validationErrors), "Cannot start connector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SaveConfiguration(options);

            if (!File.Exists(binaryManager.ExecutablePath))
            {
                MessageBox.Show("The connector binary is not installed yet. Use Download / Update Binary first.", "Cannot start connector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            logTextBox.Clear();
            AppendLog(ConnectorArguments.ToDisplayCommand("outsystemscc.exe", options));
            connector.Start(binaryManager.ExecutablePath, options);
            SetRunningState(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            MessageBox.Show(ex.Message, "Cannot start connector", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetRunningState(false);
        }
    }

    private async Task StopConnectorAsync()
    {
        stopButton.Enabled = false;
        AppendLog("Stopping outsystemscc...");
        await connector.StopAsync().ConfigureAwait(true);
        SetRunningState(false);
    }

    private LaunchOptions ReadOptions()
    {
        return new LaunchOptions(
            addressTextBox.Text,
            tokenTextBox.Text,
            ReadEndpoints(),
            proxyTextBox.Text,
            verboseCheckBox.Checked);
    }

    private void LoadConfiguration()
    {
        try
        {
            ApplyConfiguration(configurationStore.Load());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            AppendLog($"Configuration load failed: {ex.Message}");
        }
    }

    private void ApplyConfiguration(GuiConfiguration configuration)
    {
        addressTextBox.Text = configuration.Address;
        tokenTextBox.Text = configuration.Token;
        proxyTextBox.Text = configuration.Proxy;
        verboseCheckBox.Checked = configuration.Verbose;
        autoUpdateComboBox.SelectedItem = configuration.AutoUpdate;
        if (autoUpdateComboBox.SelectedItem is null)
        {
            autoUpdateComboBox.SelectedItem = "daily";
        }

        lastUpdateCheck = configuration.LastUpdateCheck;
        endpointsGrid.Rows.Clear();

        foreach (var endpoint in configuration.Endpoints)
        {
            endpointsGrid.Rows.Add(endpoint.LocalPort, endpoint.RemoteHost, endpoint.RemotePort);
        }
    }

    private void SaveConfiguration()
    {
        SaveConfiguration(ReadOptions());
    }

    private void SaveConfiguration(LaunchOptions options)
    {
        try
        {
            configurationStore.Save(ReadConfiguration(options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppendLog($"Configuration save failed: {ex.Message}");
        }
    }

    private GuiConfiguration ReadConfiguration(LaunchOptions options)
    {
        return GuiConfiguration.FromLaunchOptions(options, new GuiConfiguration
        {
            AutoUpdate = Convert.ToString(autoUpdateComboBox.SelectedItem) ?? "daily",
            LastUpdateCheck = lastUpdateCheck
        });
    }

    private IReadOnlyList<Endpoint> ReadEndpoints()
    {
        var endpoints = new List<Endpoint>();
        foreach (DataGridViewRow row in endpointsGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var localPort = Convert.ToString(row.Cells["LocalPort"].Value) ?? string.Empty;
            var remoteHost = Convert.ToString(row.Cells["RemoteHost"].Value) ?? string.Empty;
            var remotePort = Convert.ToString(row.Cells["RemotePort"].Value) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(localPort) && string.IsNullOrWhiteSpace(remoteHost) && string.IsNullOrWhiteSpace(remotePort))
            {
                continue;
            }

            endpoints.Add(new Endpoint(localPort, remoteHost, remotePort));
        }

        return endpoints;
    }

    private void SetRunningState(bool running)
    {
        startButton.Enabled = !running;
        stopButton.Enabled = running;
        statusLabel.Text = running ? "Running" : "Stopped";
        endpointsGrid.ReadOnly = running;
        addressTextBox.ReadOnly = running;
        tokenTextBox.ReadOnly = running;
        proxyTextBox.ReadOnly = running;
        autoUpdateComboBox.Enabled = !running;
        verboseCheckBox.Enabled = !running;
        updateBinaryButton.Enabled = !running;
    }

    private async Task InstallOrUpdateBinaryAsync(bool force)
    {
        if (connector.IsRunning)
        {
            MessageBox.Show("Stop the connector before updating the binary.", "Connector is running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        updateBinaryButton.Enabled = false;
        startButton.Enabled = false;
        var previousStatus = statusLabel.Text;
        try
        {
            var progress = new Progress<string>(message =>
            {
                statusLabel.Text = message;
                AppendLog(message);
            });

            var result = force
                ? await binaryManager.InstallLatestAsync(progress).ConfigureAwait(true)
                : await binaryManager.EnsureInstalledAsync(progress).ConfigureAwait(true);

            if (result.Installed)
            {
                AppendLog($"Installed outsystemscc {result.Version}.");
            }

            if (!force)
            {
                lastUpdateCheck = DateOnly.FromDateTime(DateTime.UtcNow);
                SaveConfiguration();
            }

            await RefreshBinaryVersionAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            AppendLog($"Binary install failed: {ex.Message}");
            if (force)
            {
                MessageBox.Show(ex.Message, "Cannot install connector binary", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            statusLabel.Text = previousStatus;
            SetRunningState(connector.IsRunning);
        }
    }

    private async Task InstallOrUpdateBinaryOnScheduleAsync()
    {
        var configuration = ReadConfiguration(ReadOptions());
        if (!IsAutoUpdateDue(configuration))
        {
            AppendLog($"Auto update is {configuration.AutoUpdate}; skipping startup update check.");
            return;
        }

        await InstallOrUpdateBinaryAsync(force: false).ConfigureAwait(true);
    }

    private static bool IsAutoUpdateDue(GuiConfiguration configuration)
    {
        if (configuration.AutoUpdate == "off")
        {
            return false;
        }

        if (configuration.LastUpdateCheck is null)
        {
            return true;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var nextCheck = configuration.AutoUpdate switch
        {
            "weekly" => configuration.LastUpdateCheck.Value.AddDays(7),
            "monthly" => configuration.LastUpdateCheck.Value.AddMonths(1),
            _ => configuration.LastUpdateCheck.Value.AddDays(1)
        };

        return today >= nextCheck;
    }

    private async Task RefreshBinaryVersionAsync()
    {
        updateBinaryButton.Enabled = false;
        try
        {
            var status = await binaryManager.GetVersionStatusAsync().ConfigureAwait(true);
            var current = status.CurrentVersion ?? "not installed";
            var latest = status.LatestVersion;
            var suffix = status.IsLatest ? "up to date" : "update available";
            binaryVersionLabel.Text = $"current {current} / latest {latest} ({suffix})";
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            var current = binaryManager.InstalledVersion ?? "not installed";
            binaryVersionLabel.Text = $"current {current} / latest unavailable";
            AppendLog($"Version check failed: {ex.Message}");
        }
        finally
        {
            updateBinaryButton.Enabled = !connector.IsRunning;
        }
    }

    private void AppendLog(string line)
    {
        var timestamp = DateTime.Now;
        var logLine = $"[{timestamp:HH:mm:ss}] {line}";
        logTextBox.AppendText($"{logLine}{Environment.NewLine}");

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
            logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Could not write log file {logFilePath}: {ex.Message}{Environment.NewLine}");
        }
    }
}
