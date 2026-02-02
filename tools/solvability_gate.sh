#!/usr/bin/env bash
# Decantra Solvability Gate
# Validates that all levels in solver-solutions-debug.txt are solvable.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "${SCRIPT_DIR}")"
SOLUTIONS_FILE="${ROOT_DIR}/solver-solutions-debug.txt"

if [[ ! -f "${SOLUTIONS_FILE}" ]]; then
  echo "ERROR: ${SOLUTIONS_FILE} not found"
  echo "Run './build --generate-solutions' first"
  exit 1
fi

echo "=== Solvability Gate ==="
echo "Checking ${SOLUTIONS_FILE}..."

# Count unsolvable/error levels
UNSOLVABLE=$(grep -c "UNSOLVABLE\|ERROR\|TIMEOUT" "${SOLUTIONS_FILE}" || true)

if [[ "${UNSOLVABLE}" -gt 0 ]]; then
  echo ""
  echo "FAIL: ${UNSOLVABLE} unsolvable/error level(s) found:"
  grep -n "UNSOLVABLE\|ERROR\|TIMEOUT" "${SOLUTIONS_FILE}" | head -10
  if [[ "${UNSOLVABLE}" -gt 10 ]]; then
    echo "  ... and $((UNSOLVABLE - 10)) more"
  fi
  exit 1
else
  # Also verify expected level count
  LEVEL_COUNT=$(grep -c "^level=" "${SOLUTIONS_FILE}" || true)
  echo "PASS: All ${LEVEL_COUNT} levels are solvable"
  exit 0
fi
