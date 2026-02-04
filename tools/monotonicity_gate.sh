#!/usr/bin/env bash
# Decantra Difficulty Invariant Gate
# Validates that solver-solutions-debug.txt satisfies:
# - Levels 1..200: difficulty == level
# - Levels >= 201: difficulty == 100

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "${SCRIPT_DIR}")"
SOLUTIONS_FILE="${ROOT_DIR}/solver-solutions-debug.txt"

if [[ ! -f "${SOLUTIONS_FILE}" ]]; then
  echo "ERROR: ${SOLUTIONS_FILE} not found"
  echo "Run './build --generate-solutions' first"
  exit 1
fi

echo "=== Monotonicity Gate ==="
echo "Checking ${SOLUTIONS_FILE}..."

# Extract level and difficulty pairs, validate invariant
awk -F'[=,]' '
BEGIN {
  expected_level = 1
  violations = 0
}
/^level=/ {
  # Extract level number (field 2) and difficulty (field 4)
  level = $2 + 0
  diff = $4 + 0
  
  # Skip if error line
  if (diff == 0) next
  
  # Validate level sequence (no gaps/regressions)
  if (level != expected_level) {
    violations++
    if (violations <= 5) {
      print "  VIOLATION: Level sequence mismatch at " level " (expected " expected_level ")"
    }
    expected_level = level
  }
  expected_level++

  # Validate invariant
  if (level <= 200) {
    if (diff != level) {
      violations++
      if (violations <= 5) {
        print "  VIOLATION: Level " level ": difficulty=" diff " expected=" level
      }
    }
  } else {
    if (diff != 100) {
      violations++
      if (violations <= 5) {
        print "  VIOLATION: Level " level ": difficulty=" diff " expected=100"
      }
    }
  }
}
END {
  if (violations > 0) {
    print ""
    print "FAIL: " violations " difficulty invariant violation(s) found"
    exit 1
  } else {
    print "PASS: Difficulty invariant satisfied"
    exit 0
  }
}
' "${SOLUTIONS_FILE}"
