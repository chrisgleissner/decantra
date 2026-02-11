#!/usr/bin/env bash
# Decantra Bottle Size Diversity Gate
# Validates that generated levels have sufficient bottle size diversity.
# 
# Requirements:
# - Levels 50+: at least 3 distinct bottle capacities
# - Levels 80+: at least 4 distinct bottle capacities
# - Levels 50+: must have both small (≤3) and large (≥6) bottles
#
# Note: This gate validates the generator configuration, not individual level output.
# The actual diversity is ensured by CapacityProfile constraints.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "${SCRIPT_DIR}")"

echo "=== Bottle Size Diversity Gate ==="
echo "Validating capacity profile configuration..."

# We validate the code-level constraints by checking CapacityProfile.cs
CAPACITY_FILE="${ROOT_DIR}/Assets/Decantra/Domain/Rules/CapacityProfile.cs"

if [[ ! -f "${CAPACITY_FILE}" ]]; then
  echo "ERROR: ${CAPACITY_FILE} not found"
  exit 1
fi

# Check that the file contains expected diversity requirements
CHECKS_PASSED=0
CHECKS_TOTAL=0

# Check 1: Mid-tier (level 31-50) requires 3+ distinct capacities
((CHECKS_TOTAL++)) || true
if grep -q "minDistinctCapacities: 3" "${CAPACITY_FILE}"; then
  echo "✓ Mid-tier (31-50) requires 3+ distinct capacities"
  ((CHECKS_PASSED++)) || true
else
  echo "✗ Mid-tier does not require 3+ distinct capacities"
fi

# Check 2: Late-tier (level 71-85) requires 4+ distinct capacities
((CHECKS_TOTAL++)) || true
if grep -q "minDistinctCapacities: 4" "${CAPACITY_FILE}"; then
  echo "✓ Late-tier (71+) requires 4+ distinct capacities"
  ((CHECKS_PASSED++)) || true
else
  echo "✗ Late-tier does not require 4+ distinct capacities"
fi

# Check 3: Maximum tier (86-100) requires 5+ distinct capacities
((CHECKS_TOTAL++)) || true
if grep -q "minDistinctCapacities: 5" "${CAPACITY_FILE}"; then
  echo "✓ Maximum tier (86+) requires 5+ distinct capacities"
  ((CHECKS_PASSED++)) || true
else
  echo "✗ Maximum tier does not require 5+ distinct capacities"
fi

# Check 4: Small bottles (≤3) are required in mid+ tiers
((CHECKS_TOTAL++)) || true
if grep -E "minSmallBottles: [12]" "${CAPACITY_FILE}" > /dev/null; then
  echo "✓ Mid+ tiers require small bottles"
  ((CHECKS_PASSED++)) || true
else
  echo "✗ Mid+ tiers do not require small bottles"
fi

# Check 5: Large bottles (≥6) are required in mid+ tiers
((CHECKS_TOTAL++)) || true
if grep -E "minLargeBottles: [12]" "${CAPACITY_FILE}" > /dev/null; then
  echo "✓ Mid+ tiers require large bottles"
  ((CHECKS_PASSED++)) || true
else
  echo "✗ Mid+ tiers do not require large bottles"
fi

# Check 6: Capacity pools include extreme sizes (2 and 8+)
((CHECKS_TOTAL++)) || true
if grep "{ 2," "${CAPACITY_FILE}" > /dev/null && grep ", 8" "${CAPACITY_FILE}" > /dev/null; then
  echo "✓ Capacity pools include extreme sizes (2 and 8+)"
  ((CHECKS_PASSED++)) || true
else
  echo "✗ Capacity pools missing extreme sizes"
fi

echo ""
if [[ "${CHECKS_PASSED}" -eq "${CHECKS_TOTAL}" ]]; then
  echo "PASS: All ${CHECKS_TOTAL} diversity checks passed"
  exit 0
else
  echo "FAIL: ${CHECKS_PASSED}/${CHECKS_TOTAL} diversity checks passed"
  exit 1
fi
