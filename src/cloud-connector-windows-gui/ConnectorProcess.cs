using System.Diagnostics;

using CloudConnectorWindowsGui.Core;

namespace CloudConnectorWindowsGui;

internal sealed class ConnectorProcess
{
    private Process? process;

    public bool IsRunning => process is { HasExited: false };

    public event Action<string>? OutputReceived;
    public event Action<int>? Exited;

    public void Start(string executablePath, LaunchOptions options)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("The connector is already running.");
        }

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("outsystemscc.exe is not installed. Download the connector binary first.", executablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in ConnectorArguments.Build(options))
        {
            startInfo.ArgumentList.Add(argument);
        }

        process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        process.OutputDataReceived += (_, args) => Emit(args.Data);
        process.ErrorDataReceived += (_, args) => Emit(args.Data);
        process.Exited += (_, _) =>
        {
            var exitCode = process?.ExitCode ?? -1;
            Exited?.Invoke(exitCode);
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start outsystemscc.exe.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    public async Task StopAsync()
    {
        if (process is null || process.HasExited)
        {
            return;
        }

        try
        {
            process.CloseMainWindow();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
    }

    private void Emit(string? line)
    {
        if (!string.IsNullOrEmpty(line))
        {
            OutputReceived?.Invoke(line);
        }
    }
}
