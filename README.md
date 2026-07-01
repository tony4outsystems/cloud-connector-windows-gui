# Cloud Connector Windows Launcher

A small Windows-only graphical launcher for `outsystemscc.exe`.

The app helps users build and run this command without typing the raw CLI syntax:

```text
outsystemscc.exe --header "token: <token>" [--proxy <proxy>] [-v] <address> R:<local-port>:<remote-host>:<remote-port>...
```

## Features

- Address and token inputs for the Private Gateway values from ODC Portal.
- TCP endpoint grid with local secure-gateway port, remote host, and remote port.
- Optional proxy and verbose logging.
- Start/Stop controls with live stdout/stderr log capture.
- Bundles `outsystemscc.exe` beside the launcher for simple Windows deployment.

## Build

Install .NET 10 SDK and Go, then run:

```sh
./scripts/publish-win-x64.sh
```

The script publishes the self-contained Windows app to `artifacts/win-x64` and cross-builds `../cloud-connector/outsystemscc.exe` into the same folder.

## Test

```sh
dotnet test tests/CloudConnectorWin.Core.Tests/CloudConnectorWin.Core.Tests.csproj
```

