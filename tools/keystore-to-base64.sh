#!/usr/bin/env bash
set -euo pipefail

# Encodes a keystore file as a single-line base64 string.
#
# Useful for storing the keystore in CI secrets (e.g., GitHub Actions)
# where binary files cannot be used directly.
#
# Usage:
#   ./scripts/keystore-to-base64.sh [keystore-path]
#
# Defaults:
#   keystore-path = ./release.keystore
#
# Output goes to stdout. Redirect to a file if needed:
#   ./scripts/keystore-to-base64.sh > keystore.b64

KEYSTORE_FILE="${1:-release.keystore}"

die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

[[ -f "$KEYSTORE_FILE" ]] || die "Keystore not found: $KEYSTORE_FILE"
command -v base64 >/dev/null 2>&1 || die "base64 not found."

base64 -w 0 "$KEYSTORE_FILE"
printf '\n'
