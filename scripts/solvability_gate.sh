#!/usr/bin/env bash
# Decantra Solvability Gate
# Validates that all levels in solver-solutions-debug.txt are solvable.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "${SCRIPT_DIR}")"
SOLUTIONS_FILE="${ROOT_DIR}/solver-solutions-debug.txt"
EXPECTED_LEVEL_COUNT="${DECANTRA_EXPECTED_LEVELS:-1000}"

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
  if [[ "${LEVEL_COUNT}" -le 0 ]]; then
    echo ""
    echo "FAIL: No levels were parsed from ${SOLUTIONS_FILE}"
    exit 1
  fi

  if [[ "${EXPECTED_LEVEL_COUNT}" =~ ^[0-9]+$ ]] && [[ "${EXPECTED_LEVEL_COUNT}" -gt 0 ]] && [[ "${LEVEL_COUNT}" -ne "${EXPECTED_LEVEL_COUNT}" ]]; then
    echo ""
    echo "FAIL: Expected ${EXPECTED_LEVEL_COUNT} levels, found ${LEVEL_COUNT}"
    exit 1
  fi

  echo "PASS: All ${LEVEL_COUNT} levels are solvable"
  exit 0
fi
