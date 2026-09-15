#!/usr/bin/env bash
#
# Makes sure a Docker daemon is available, starting Docker Desktop if it is
# installed but not running. Sourced by the other scripts; safe to run directly.
#
# Exits non-zero with installation instructions if Docker is not installed,
# because installing it needs administrator rights and a licence acceptance
# that a script must not make on your behalf.

set -euo pipefail

DOCKER_WAIT_SECONDS="${DOCKER_WAIT_SECONDS:-90}"

log()  { printf '\033[0;36m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[0;33m/!\\\033[0m %s\n' "$*"; }
fail() { printf '\033[0;31mxxx\033[0m %s\n' "$*" >&2; }

install_instructions() {
  local os
  os="$(uname -s)"

  fail "Docker is not installed, and the integration tests cannot run without it."
  echo
  case "$os" in
    Darwin)
      echo "  Install Docker Desktop (includes the daemon, CLI and Compose):"
      echo "      brew install --cask docker"
      echo "  or download it from https://www.docker.com/products/docker-desktop/"
      echo
      if [ "$(uname -m)" = "arm64" ]; then
        echo "  You are on Apple Silicon. After installing, open Docker Desktop and enable"
        echo "      Settings > General > \"Use Rosetta for x86/amd64 emulation\""
        echo "  The SQL Server image is published for amd64 only and needs this to run."
      fi
      echo
      echo "  Lighter alternative, if you prefer no Docker Desktop:"
      echo "      brew install colima docker && colima start --arch x86_64 --memory 4"
      ;;
    Linux)
      echo "  Install Docker Engine: https://docs.docker.com/engine/install/"
      echo "  Then allow your user to run it without sudo:"
      echo "      sudo usermod -aG docker \"\$USER\" && newgrp docker"
      ;;
    *)
      echo "  Install Docker Desktop: https://www.docker.com/products/docker-desktop/"
      ;;
  esac
  echo
  echo "  Then re-run this script. Unit tests need none of this:"
  echo "      ./scripts/run-unit-tests.sh"
  echo
  exit 1
}

start_docker_desktop() {
  case "$(uname -s)" in
    Darwin)
      if [ -d "/Applications/Docker.app" ]; then
        log "Docker is installed but not running. Starting Docker Desktop..."
        open -a Docker
        return 0
      fi
      if command -v colima >/dev/null 2>&1; then
        log "Docker is installed but not running. Starting Colima..."
        colima start
        return 0
      fi
      ;;
    Linux)
      if command -v systemctl >/dev/null 2>&1; then
        log "Docker is installed but not running. Starting the docker service..."
        sudo systemctl start docker || true
        return 0
      fi
      ;;
  esac
  return 1
}

ensure_docker() {
  command -v docker >/dev/null 2>&1 || install_instructions

  if docker info >/dev/null 2>&1; then
    log "Docker is running ($(docker version --format '{{.Server.Version}}' 2>/dev/null || echo 'version unknown'))."
    return 0
  fi

  if ! start_docker_desktop; then
    fail "Docker is installed but the daemon is not reachable, and this script could not start it."
    echo "  Start Docker manually, then re-run."
    exit 1
  fi

  log "Waiting up to ${DOCKER_WAIT_SECONDS}s for the Docker daemon..."
  local waited=0
  until docker info >/dev/null 2>&1; do
    if [ "$waited" -ge "$DOCKER_WAIT_SECONDS" ]; then
      fail "Docker did not become ready within ${DOCKER_WAIT_SECONDS}s."
      echo "  Open Docker Desktop, wait for it to report 'Running', then re-run."
      exit 1
    fi
    sleep 2
    waited=$((waited + 2))
    printf '.'
  done
  printf '\n'
  log "Docker is ready."
}

# Only run automatically when executed directly, not when sourced.
if [ "${BASH_SOURCE[0]}" = "${0}" ]; then
  ensure_docker
fi
