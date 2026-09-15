#!/usr/bin/env bash
#
# Runs the integration test suite against a real, throwaway SQL Server in Docker.
#
#   ./scripts/run-integration-tests.sh              # run them all
#   ./scripts/run-integration-tests.sh --filter X   # extra `dotnet test` arguments
#
# What it does:
#   1. Makes sure Docker is installed and running (starting it if it is installed).
#   2. Pulls the pinned SQL Server image, so the first run does not look hung.
#   3. Runs `dotnet test --filter Category=Integration`.
#
# Testcontainers creates the container on a random port with a generated
# password and destroys it when the run ends. No developer or deployed database
# is ever touched, and there is nothing to clean up afterwards.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# shellcheck source=./ensure-docker.sh
source "$SCRIPT_DIR/ensure-docker.sh"

# Keep this in step with SqlServerFixture.DefaultImage.
SQL_IMAGE="${ETIMESHEET_TEST_SQL_IMAGE:-mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04}"

ensure_docker

PLATFORM_ARGS=()
if [ "$(uname -m)" = "arm64" ] || [ "$(uname -m)" = "aarch64" ]; then
  # The SQL Server image is published for amd64 only. Pulling that variant
  # explicitly means the local cache holds an image the container can start,
  # via Rosetta on macOS or qemu/binfmt on Linux.
  PLATFORM_ARGS=(--platform linux/amd64)
  warn "arm64 host detected: SQL Server runs under emulation here."
  warn "If the container fails to start, enable Docker Desktop > Settings > General >"
  warn "\"Use Rosetta for x86/amd64 emulation\", or set ETIMESHEET_TEST_SQL_IMAGE to a"
  warn "natively built image."
fi

if docker image inspect "$SQL_IMAGE" >/dev/null 2>&1; then
  log "SQL Server image already present: $SQL_IMAGE"
else
  log "Pulling $SQL_IMAGE (about 1.5 GB, first run only)..."
  docker pull "${PLATFORM_ARGS[@]}" "$SQL_IMAGE"
fi

log "Running integration tests..."
cd "$ROOT_DIR"

# Starting SQL Server takes a while, especially emulated; give the host room.
export ETIMESHEET_TEST_SQL_IMAGE="$SQL_IMAGE"

dotnet test \
  --filter "Category=Integration" \
  --logger "console;verbosity=normal" \
  --nologo \
  "$@"

log "Integration tests finished. The SQL Server container has been removed."
