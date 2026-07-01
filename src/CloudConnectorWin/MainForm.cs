using CloudConnectorWin.Core;

namespace CloudConnectorWin;

internal sealed class MainForm : Form
{
    private readonly TextBox addressTextBox = new();
    private readonly TextBox tokenTextBox = new();
    private readonly TextBox proxyTextBox = new();
    private readonly CheckBox verboseCheckBox = new();
    private readonly DataGridView endpointsGrid = new();
    private readonly Button startButton = new();
    private readonly Button stopButton = new();
    private readonly Label statusLabel = new();
    private readonly TextBox logTextBox = new();
    private readonly ConnectorProcess connector = new();

    public MainForm()
    {
        Text = "OutSystems Cloud Connector";
        MinimumSize = new Size(920, 700);
        StartPosition = FormStartPosition.CenterScreen;

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
            RowCount = 5
        };
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
        startButton.Width = 100;
        stopButton.Text = "Stop";
        stopButton.Width = 100;
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
            HeaderText = "Local secure-gateway port",
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
        connector.OutputReceived += line => BeginInvoke(() => AppendLog(line));
        connector.Exited += exitCode => BeginInvoke(() =>
        {
            AppendLog($"outsystemscc exited with code {exitCode}");
            SetRunningState(false);
        });
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

        var executablePath = Path.Combine(AppContext.BaseDirectory, "outsystemscc.exe");
        try
        {
            logTextBox.Clear();
            AppendLog(ConnectorArguments.ToDisplayCommand("outsystemscc.exe", options));
            connector.Start(executablePath, options);
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
    }

    private void AppendLog(string line)
    {
        logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
    }
}

