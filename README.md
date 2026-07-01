# cloud-connector-windows-gui

A small Windows and macOS graphical launcher for `outsystemscc`, built with **.NET MAUI**.

> The screenshot below predates the .NET MAUI conversion and shows the previous WinForms UI.

![OutSystems Cloud Connector GUI screenshot](docs/app-screenshot.png)

The app helps users build and run this command without typing the raw CLI syntax:

```text
outsystemscc --header "token: <token>" [--proxy <proxy>] [-v] <address> R:<local-port>:<remote-host>:<remote-port>...
```

## Features

- Address and token inputs for the Private Gateway values from ODC Portal.
- TCP endpoint grid with local secure-gateway port, remote host, and remote port.
- Optional proxy and verbose logging.
- Start/Stop controls with live stdout/stderr log capture.
- Downloads the matching Windows or macOS `outsystemscc` binary from stable GitHub releases of
  [`tony4outsystems/cloud-connector`](https://github.com/tony4outsystems/cloud-connector).
- Shows the installed connector version and the latest stable version available on GitHub.
- Manual Download / Update Binary button.

On first start, the app installs the connector binary into the current user's local app data
folder. The launcher uses GitHub release JSON from `/releases`, ignores prereleases, selects the
matching OS and CPU architecture archive, and verifies the release SHA-256 digest when GitHub
provides one.

## Build

Install the .NET 10 SDK and the MAUI workload for your platform.

For Windows:

```sh
dotnet workload install maui-windows
./scripts/publish-win-x64.sh
```

The script publishes the self-contained Windows app to `artifacts/win-x64`. The connector binary is
downloaded by the app at runtime.

Building the Windows target requires a Windows machine with the MAUI Windows workload installed.

For macOS:

```sh
dotnet workload install maui-maccatalyst
./scripts/publish-macos.sh
```

The macOS script publishes the self-contained Mac Catalyst app to `artifacts/macos-arm64` on Apple
Silicon and `artifacts/macos-x64` on Intel Macs. You can pass `arm64` or `x64` as the first script
argument to choose a specific runtime.

## Release

GitHub Actions builds and publishes Windows and macOS release packages when a `v*` tag is pushed:

```sh
git tag v1.0.0
git push origin v1.0.0
```

The workflow can also be run manually from GitHub Actions with a tag name. It uploads
`cloud-connector-windows-gui-win-x64.zip` and `cloud-connector-windows-gui-macos-arm64.zip` as both
workflow artifacts and GitHub Release assets.

## Test

```sh
dotnet test tests/cloud-connector-windows-gui.Core.Tests/cloud-connector-windows-gui.Core.Tests.csproj
```
