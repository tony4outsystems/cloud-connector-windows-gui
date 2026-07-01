#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
publish_dir="$repo_root/artifacts/win-x64"

dotnet test "$repo_root/tests/cloud-connector-windows-gui.Core.Tests/cloud-connector-windows-gui.Core.Tests.csproj"
dotnet publish "$repo_root/src/cloud-connector-windows-gui/cloud-connector-windows-gui.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o "$publish_dir"

echo "Published cloud-connector-windows-gui to $publish_dir"
