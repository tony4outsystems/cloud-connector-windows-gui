# cloud-connector-gui

A small cross-platform (Windows, macOS, Linux) graphical launcher for the `outsystemscc` connector.

![Cloud Connector GUI screenshot](docs/app-screenshot.png)

The app helps users build and run this command without typing the raw CLI syntax:

```text
outsystemscc.exe --header "token: <token>" [--proxy <proxy>] [-v] <address> R:<local-port>:<remote-host>:<remote-port>...
```

## Features

- Address and token inputs for the Private Gateway values from ODC Portal.
- TCP endpoint grid with local secure-gateway port, remote host, and remote port.
- Optional proxy and verbose logging.
- Start/Stop controls with live stdout/stderr log capture.
- Downloads the `outsystemscc` binary (Windows, macOS, or Linux, matching the host CPU
  architecture) from stable GitHub releases of
  [`tony4outsystems/cloud-connector`](https://github.com/tony4outsystems/cloud-connector).
- Shows the installed connector version and the latest stable version available on GitHub.
- Manual Download / Update Binary button.
- Self-updates the GUI itself via [Velopack](https://velopack.io) — see [Release](#release)
  below. This is a separate, unrelated mechanism from the connector-binary download above.

On first start, the app installs the connector binary into the current user's local app data
folder. The launcher uses GitHub release JSON from `/releases`, ignores prereleases, selects the
matching platform/architecture archive, and verifies the release SHA-256 digest when GitHub
provides one.

The GUI's own configuration (`cloud-connector-gui.toml`) and log file are also stored in the
current user's local app data folder rather than next to the executable, so they survive
GUI self-updates (which replace the entire install directory).

## Build

Install .NET 10 SDK, then run:

```sh
dotnet test tests/cloud-connector-gui.Core.Tests/cloud-connector-gui.Core.Tests.csproj
dotnet publish src/cloud-connector-gui/cloud-connector-gui.csproj \
  -c Release \
  -r <rid> \
  --self-contained true \
  -o artifacts/<rid>
```

Replace `<rid>` with your target runtime identifier: `win-x64`, `osx-x64`, `osx-arm64`, or
`linux-x64`. The publish command writes the self-contained app to `artifacts/<rid>`. The connector
binary is downloaded by the app at runtime.

## Release

GitHub Actions builds and publishes release packages for Windows, macOS (x64 and arm64), and
Linux when a `v*` tag is pushed:

```sh
git tag v1.0.0
git push origin v1.0.0
```

For each platform the workflow publishes a self-contained build, then packages it with the
[Velopack](https://velopack.io) `vpk` CLI (`vpk pack --noInst`) into a portable build with no
installer (Windows and macOS both produce an installer by default unless `--noInst` is passed;
Linux's AppImage output has no installer concept either way). Each platform/architecture
(`win-x64`, `osx-x64`, `osx-arm64`, `linux-x64`) is packaged as its own Velopack release
channel and uploaded to the same GitHub Release via `vpk upload github --merge --publish`,
alongside a plain workflow artifact of the packaged output for convenience.

The macOS build is ad-hoc signed (`--signAppIdentity -`) — a free, local, certificate-less
signature. This isn't the same as a paid Apple Developer ID / notarized signature (no such
signing is configured), but it's required: unsigned `.app` bundles fail Apple Silicon's
mandatory code-signing check and macOS reports them as "damaged and can't be opened" instead of
the usual "unidentified developer" prompt. Windows and Linux builds remain fully unsigned.

Once installed, running GUIs periodically check this GitHub Release feed and show an in-app
banner to download and apply the update in place — there's no separate installer download
needed for subsequent updates.

## Test

```sh
dotnet test tests/cloud-connector-gui.Core.Tests/cloud-connector-gui.Core.Tests.csproj
```
