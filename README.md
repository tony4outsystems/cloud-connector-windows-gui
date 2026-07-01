# cloud-connector-windows-gui

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
- Downloads the Windows `outsystemscc.exe` binary from stable GitHub releases of
  [`tony4outsystems/cloud-connector`](https://github.com/tony4outsystems/cloud-connector).
- Shows the installed connector version and the latest stable version available on GitHub.
- Manual Download / Update Binary button.

On first start, the app installs the connector binary into the current user's local app data
folder. The launcher uses GitHub release JSON from `/releases`, ignores prereleases, selects the
matching Windows archive for the current CPU architecture, and verifies the release SHA-256 digest
when GitHub provides one.

## Build

Install .NET 10 SDK, then run:

```sh
./scripts/publish-win-x64.sh
```

The script publishes the self-contained Windows app to `artifacts/win-x64`. The connector binary is
downloaded by the app at runtime.

## Test

```sh
dotnet test tests/cloud-connector-windows-gui.Core.Tests/cloud-connector-windows-gui.Core.Tests.csproj
```
