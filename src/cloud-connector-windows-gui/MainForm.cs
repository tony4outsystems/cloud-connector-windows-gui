using CloudConnectorWindowsGui.Core;

namespace CloudConnectorWindowsGui;

internal sealed class MainForm : Form
{
    private readonly TextBox addressTextBox = new();
    private readonly TextBox tokenTextBox = new();
    private readonly TextBox proxyTextBox = new();
    private readonly CheckBox verboseCheckBox = new();
    private readonly DataGridView endpointsGrid = new();
    private readonly Button startButton = new();
    private readonly Button stopButton = new();
    private readonly Button updateBinaryButton = new();
    private readonly Label binaryVersionLabel = new();
    private readonly Label statusLabel = new();
    private readonly TextBox logTextBox = new();
    private readonly ConnectorProcess connector = new();
    private readonly CloudConnectorBinaryManager binaryManager = new();

    public MainForm()
    {
        Text = "OutSystems Cloud Connector";
        MinimumSize = new Size(920, 700);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildLayout();
        WireEvents();
        SetRunningState(false);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 6
        };
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
            AutoSize = true
        };
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(inputs);

        AddLabeledControl(inputs, "Address", addressTextBox);
        AddLabeledControl(inputs, "Token", tokenTextBox);
        AddLabeledControl(inputs, "Proxy", proxyTextBox);

        tokenTextBox.UseSystemPasswordChar = true;
        proxyTextBox.PlaceholderText = "Optional HTTP CONNECT or SOCKS5 proxy";
        addressTextBox.PlaceholderText = "https://organization.outsystems.app/sg_...";

        verboseCheckBox.Text = "Verbose logs";
        verboseCheckBox.AutoSize = true;
        verboseCheckBox.Margin = new Padding(110, 8, 0, 10);
        inputs.SetColumnSpan(verboseCheckBox, 2);
        inputs.Controls.Add(verboseCheckBox);

        var binaryPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 12)
        };
        root.Controls.Add(binaryPanel);

        updateBinaryButton.Text = "Download / Update Binary";
        ConfigureActionButton(updateBinaryButton, 190);
        binaryVersionLabel.AutoSize = true;
        binaryVersionLabel.Padding = new Padding(12, 7, 0, 0);
        binaryVersionLabel.Text = "Connector binary: not checked";

        binaryPanel.Controls.Add(updateBinaryButton);
        binaryPanel.Controls.Add(binaryVersionLabel);

        ConfigureEndpointGrid();
        root.Controls.Add(endpointsGrid);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 12, 0, 12)
        };
        root.Controls.Add(actions);

        startButton.Text = "Start";
        ConfigureActionButton(startButton, 100);
        stopButton.Text = "Stop";
        ConfigureActionButton(stopButton, 100);
        statusLabel.AutoSize = true;
        statusLabel.Padding = new Padding(16, 7, 0, 0);

        actions.Controls.Add(startButton);
        actions.Controls.Add(stopButton);
        actions.Controls.Add(statusLabel);

        logTextBox.Dock = DockStyle.Fill;
        logTextBox.Multiline = true;
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        logTextBox.Font = new Font(FontFamily.GenericMonospace, 9);
        root.Controls.Add(logTextBox);
    }

    private static void ConfigureActionButton(Button button, int minimumWidth)
    {
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.MinimumSize = new Size(minimumWidth, 36);
        button.Padding = new Padding(10, 5, 10, 5);
        button.Margin = new Padding(3, 3, 6, 3);
    }

    private static void AddLabeledControl(TableLayoutPanel panel, string label, TextBox textBox)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 8, 7)
        });

        textBox.Dock = DockStyle.Top;
        textBox.Margin = new Padding(0, 4, 0, 4);
        panel.Controls.Add(textBox);
    }

    private void ConfigureEndpointGrid()
    {
        endpointsGrid.Dock = DockStyle.Fill;
        endpointsGrid.AllowUserToAddRows = true;
        endpointsGrid.AllowUserToDeleteRows = true;
        endpointsGrid.AutoGenerateColumns = false;
        endpointsGrid.RowHeadersWidth = 28;
        endpointsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        endpointsGrid.MultiSelect = false;
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
            await InstallOrUpdateBinaryAsync(force: false).ConfigureAwait(true);
        };
        FormClosing += async (_, args) =>
        {
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

    private async Task RefreshBinaryVersionAsync()
    {
        updateBinaryButton.Enabled = false;
        try
        {
            var status = await binaryManager.GetVersionStatusAsync().ConfigureAwait(true);
            var current = status.CurrentVersion ?? "not installed";
            var latest = status.LatestVersion;
            var suffix = status.IsLatest ? "up to date" : "update available";
            binaryVersionLabel.Text = $"Connector binary: current {current} / latest {latest} ({suffix})";
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            var current = binaryManager.InstalledVersion ?? "not installed";
            binaryVersionLabel.Text = $"Connector binary: current {current} / latest unavailable";
            AppendLog($"Version check failed: {ex.Message}");
        }
        finally
        {
            updateBinaryButton.Enabled = !connector.IsRunning;
        }
    }

    private void AppendLog(string line)
    {
        logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
    }
}
