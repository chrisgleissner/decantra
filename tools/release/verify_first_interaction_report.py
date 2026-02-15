#!/usr/bin/env python3
import argparse
import json
import pathlib
import sys


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify Decantra first-interaction shift report.")
    parser.add_argument("--report", required=True, help="Path to report.json")
    parser.add_argument("--max-dy", type=float, default=1.0, help="Maximum allowed absolute dy")
    parser.add_argument("--require-files", nargs="*", default=[], help="Additional files that must exist")
    args = parser.parse_args()

    report_path = pathlib.Path(args.report)
    if not report_path.exists():
        print(f"Report not found: {report_path}", file=sys.stderr)
        return 2

    data = json.loads(report_path.read_text(encoding="utf-8"))

    max_abs_dy = data.get("maxAbsDy")
    if max_abs_dy is None:
        max_abs_dy = data.get("maxBottleDeltaY")

    if max_abs_dy is None:
        print("Report missing maxAbsDy/maxBottleDeltaY", file=sys.stderr)
        return 3

    passed = bool(data.get("pass", False)) and float(max_abs_dy) <= float(args.max_dy)

    missing = [p for p in args.require_files if not pathlib.Path(p).exists()]
    if missing:
        print("Missing required files:", file=sys.stderr)
        for p in missing:
            print(f"  - {p}", file=sys.stderr)
        return 4

    print(json.dumps({
        "report": str(report_path),
        "maxAbsDy": float(max_abs_dy),
        "allowed": float(args.max_dy),
        "passField": bool(data.get("pass", False)),
        "verdict": "PASS" if passed else "FAIL",
    }, indent=2))

    return 0 if passed else 5


if __name__ == "__main__":
    raise SystemExit(main())
