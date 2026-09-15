#!/usr/bin/env bash
#
# Runs everything: unit and architecture tests first (fast, no dependencies),
# then the integration suite against a throwaway SQL Server in Docker.
#
#   ./scripts/run-all-tests.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/run-unit-tests.sh"
"$SCRIPT_DIR/run-integration-tests.sh"
