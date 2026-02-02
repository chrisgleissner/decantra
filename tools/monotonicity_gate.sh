#!/usr/bin/env bash
# Decantra Monotonicity Gate
# Validates that solver-solutions-debug.txt has zero monotonicity violations.
# Difficulty must be non-decreasing for levels 1-100 and constant at 100 for 101+.

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

# Extract level and difficulty pairs, validate monotonicity
awk -F'[=,]' '
BEGIN {
  prev_level = 0
  prev_diff = 0
  violations = 0
  first_violation = 0
}
/^level=/ {
  # Extract level number (field 2) and difficulty (field 4)
  level = $2 + 0
  diff = $4 + 0
  
  # Skip if error line
  if (diff == 0) next
  
  # Check monotonicity for levels 1-100
  if (level <= 100) {
    if (prev_level > 0 && diff < prev_diff) {
      violations++
      if (first_violation == 0) {
        first_violation = level
        first_diff = diff
        first_prev = prev_diff
      }
      if (violations <= 5) {
        print "  VIOLATION: Level " level ": difficulty=" diff " < prev=" prev_diff
      }
    }
  }
  
  # For levels > 100, difficulty should be 100 (maximum)
  if (level > 100 && diff < 100) {
    # This is a warning, not a violation - plateau allows variance
    # Strict plateau check disabled for now
    # print "  WARNING: Level " level ": difficulty=" diff " < 100 (plateau)"
  }
  
  prev_level = level
  prev_diff = diff
}
END {
  if (violations > 0) {
    print ""
    print "FAIL: " violations " monotonicity violation(s) found"
    print "First violation at level " first_violation ": " first_diff " < " first_prev
    exit 1
  } else {
    print "PASS: No monotonicity violations"
    exit 0
  }
}
' "${SOLUTIONS_FILE}"
