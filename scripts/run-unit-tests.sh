#!/usr/bin/env bash
#
# Runs the unit and architecture tests. These need no Docker and no database,
# so they stay fast enough to run on every save.
#
#   ./scripts/run-unit-tests.sh [extra dotnet test arguments]

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

dotnet test \
  --filter "Category=Unit" \
  --logger "console;verbosity=normal" \
  --nologo \
  "$@"
