#!/usr/bin/env bash
set -euo pipefail

COVERAGE_PATH="${1:-$(pwd)/Coverage}"
MIN_COVERAGE="${2:-0.8}"

SUMMARY_PATH="$(find "${COVERAGE_PATH}" -name Summary.xml | head -n 1)"
if [ -z "${SUMMARY_PATH}" ]; then
  echo "Coverage Summary.xml not found under ${COVERAGE_PATH}"
  exit 1
fi

python3 - "${SUMMARY_PATH}" "${MIN_COVERAGE}" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
min_cov = float(sys.argv[2])

tree = ET.parse(path)
root = tree.getroot()

def find_value():
    attr_candidates = {"line-rate", "LineCoverage", "lineCoverage", "LineCoveragePercent", "line_coverage"}
    tag_candidates = {"linecoverage", "line-coverage", "line_coverage"}

    for attr in attr_candidates:
        if attr in root.attrib:
            return root.attrib[attr]

    for elem in root.iter():
        for attr in attr_candidates:
            if attr in elem.attrib:
                return elem.attrib[attr]

        if elem.text:
            tag = elem.tag.lower()
            if tag in tag_candidates:
                return elem.text.strip()

    return None

raw = find_value()
if raw is None:
    print("Unable to parse line coverage from Summary.xml")
    sys.exit(2)

value = float(raw)
if value > 1:
    value /= 100.0

print(f"Line coverage: {value:.3f} (min {min_cov:.3f})")
if value < min_cov:
    sys.exit(1)
PY
