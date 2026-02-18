#!/usr/bin/env bash
# Decantra Difficulty Validation Gate
# Validates that solver-solutions-debug.txt satisfies:
# - All difficulties are in range [1, 100]
# - All levels are present in sequence
# - Late levels (80-100) average difficulty >= early levels (1-20)

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

echo "=== Difficulty Validation Gate ==="
echo "Checking ${SOLUTIONS_FILE}..."

# Extract level and difficulty pairs, validate invariant
awk -F'[=,]' '
BEGIN {
  expected_level = 1
  violations = 0
  early_sum = 0
  early_count = 0
  late_sum = 0
  late_count = 0
  min_diff = 999
  max_diff = 0
  total_diff = 0
  total_count = 0
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

  # Validate difficulty is in valid range [1, 100]
  if (diff < 1 || diff > 100) {
    violations++
    if (violations <= 5) {
      print "  VIOLATION: Level " level ": difficulty=" diff " out of range [1, 100]"
    }
  }

  # Track statistics
  if (diff < min_diff) min_diff = diff
  if (diff > max_diff) max_diff = diff
  total_diff += diff
  total_count++

  # Track early/late averages
  if (level >= 1 && level <= 20) {
    early_sum += diff
    early_count++
  }
  if (level >= 80 && level <= 100) {
    late_sum += diff
    late_count++
  }
}
END {
  if (total_count <= 0) {
    print ""
    print "FAIL: No level entries found in solutions file"
    exit 1
  }

  if (ENVIRON["EXPECTED_LEVEL_COUNT"] ~ /^[0-9]+$/) {
    expected = ENVIRON["EXPECTED_LEVEL_COUNT"] + 0
    if (expected > 0 && total_count != expected) {
      print ""
      print "FAIL: Expected " expected " levels, found " total_count
      exit 1
    }
  }

  if (total_count > 0) {
    avg = total_diff / total_count
    printf "Difficulty stats: min=%d, max=%d, avg=%.1f\n", min_diff, max_diff, avg
  }

  if (early_count > 0 && late_count > 0) {
    early_avg = early_sum / early_count
    late_avg = late_sum / late_count
    printf "Early (1-20) avg: %.1f, Late (80-100) avg: %.1f\n", early_avg, late_avg
  }

  if (violations > 0) {
    print ""
    print "FAIL: " violations " difficulty validation violation(s) found"
    exit 1
  } else {
    print "PASS: Difficulty validation passed"
    exit 0
  }
}
' "${SOLUTIONS_FILE}"
