#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
connector_root="$(cd "$repo_root/../cloud-connector" && pwd)"
publish_dir="$repo_root/artifacts/win-x64"

dotnet test "$repo_root/tests/CloudConnectorWin.Core.Tests/CloudConnectorWin.Core.Tests.csproj"
dotnet publish "$repo_root/src/CloudConnectorWin/CloudConnectorWin.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o "$publish_dir"

(
  cd "$connector_root"
  GOOS=windows GOARCH=amd64 CGO_ENABLED=0 go build -o "$publish_dir/outsystemscc.exe" .
)

echo "Published Cloud Connector Windows Launcher to $publish_dir"
