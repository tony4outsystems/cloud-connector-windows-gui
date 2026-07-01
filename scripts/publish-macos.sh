#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
arch="${1:-$(uname -m)}"

case "$arch" in
  arm64|aarch64)
    runtime="maccatalyst-arm64"
    artifact_arch="arm64"
    ;;
  x64|x86_64|amd64)
    runtime="maccatalyst-x64"
    artifact_arch="x64"
    ;;
  *)
    echo "Unsupported macOS architecture: $arch" >&2
    exit 1
    ;;
esac

publish_dir="$repo_root/artifacts/macos-$artifact_arch"

dotnet workload restore "$repo_root/src/cloud-connector-windows-gui/cloud-connector-windows-gui.csproj"
dotnet test "$repo_root/tests/cloud-connector-windows-gui.Core.Tests/cloud-connector-windows-gui.Core.Tests.csproj"
dotnet publish "$repo_root/src/cloud-connector-windows-gui/cloud-connector-windows-gui.csproj" \
  -f net10.0-maccatalyst \
  -c Release \
  -r "$runtime" \
  --self-contained true \
  -o "$publish_dir"

echo "Published cloud-connector-windows-gui to $publish_dir"
