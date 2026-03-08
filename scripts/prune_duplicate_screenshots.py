#!/usr/bin/env python3
import argparse
import hashlib
import os
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from typing import Dict, Iterable, List, Sequence, Tuple

from PIL import Image


IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}
SCREENSHOT_PATH_TOKENS = (
    "screenshots/",
    "Tutorial/",
    "play-store-assets/",
)


@dataclass(frozen=True)
class PixelSignature:
    width: int
    height: int
    mode: str
    digest: str


@dataclass(frozen=True)
class DuplicateFinding:
    kind: str
    path: str
    base_path: str


def run_git(args: Sequence[str], check: bool = True) -> str:
    result = subprocess.run(["git", *args], capture_output=True, text=True)
    if check and result.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {result.stderr.strip()}")
    return result.stdout


def is_image_path(path: str) -> bool:
    _, ext = os.path.splitext(path.lower())
    return ext in IMAGE_EXTENSIONS


def is_screenshot_path(path: str) -> bool:
    return any(token in path for token in SCREENSHOT_PATH_TOKENS)


def list_repo_files(ref: str) -> List[str]:
    output = run_git(["ls-tree", "-r", "--name-only", ref])
    return [
        line.strip()
        for line in output.splitlines()
        if line.strip() and is_image_path(line.strip()) and is_screenshot_path(line.strip())
    ]


def parse_name_status(base_ref: str, scope: str) -> List[Tuple[str, str]]:
    if scope == "branch":
        output = run_git(["diff", "--name-status", f"{base_ref}...HEAD"])
    else:
        output = run_git(["diff", "--name-status", base_ref])
    rows: List[Tuple[str, str]] = []
    for line in output.splitlines():
        if not line.strip():
            continue
        parts = line.split("\t")
        status = parts[0]
        if status.startswith("R"):
            path = parts[2]
            rows.append(("R", path))
        elif status.startswith("C"):
            path = parts[2]
            rows.append(("A", path))
        else:
            path = parts[1] if len(parts) > 1 else ""
            rows.append((status[0], path))
    return rows


def list_untracked_images() -> List[str]:
    output = run_git(["ls-files", "--others", "--exclude-standard"])
    return [
        line.strip()
        for line in output.splitlines()
        if line.strip() and is_image_path(line.strip()) and is_screenshot_path(line.strip())
    ]


def load_image_signature_from_file(path: str) -> PixelSignature:
    with Image.open(path) as image:
        rgba = image.convert("RGBA")
        width, height = rgba.size
        payload = rgba.tobytes()
        digest = hashlib.sha256(payload).hexdigest()
    return PixelSignature(width=width, height=height, mode="RGBA", digest=digest)


def load_image_signature_from_ref(ref: str, path: str) -> PixelSignature:
    blob = subprocess.run(["git", "show", f"{ref}:{path}"], capture_output=True)
    if blob.returncode != 0:
        raise RuntimeError(f"Unable to read {ref}:{path}: {blob.stderr.decode().strip()}")

    with tempfile.NamedTemporaryFile(suffix=os.path.splitext(path)[1], delete=True) as temp_file:
        temp_file.write(blob.stdout)
        temp_file.flush()
        return load_image_signature_from_file(temp_file.name)


def build_base_signature_index(base_ref: str, paths: Iterable[str]) -> Dict[PixelSignature, List[str]]:
    index: Dict[PixelSignature, List[str]] = {}
    for path in paths:
        signature = load_image_signature_from_ref(base_ref, path)
        index.setdefault(signature, []).append(path)
    return index


def choose_primary(paths: List[str]) -> str:
    return sorted(paths)[0]


def build_head_catalog(paths: Iterable[str]) -> Dict[PixelSignature, List[str]]:
    catalog: Dict[PixelSignature, List[str]] = {}
    for path in paths:
        if not os.path.exists(path):
            continue
        signature = load_image_signature_from_file(path)
        catalog.setdefault(signature, []).append(path)
    return catalog


def find_duplicates(base_ref: str, scope: str) -> List[DuplicateFinding]:
    changed_rows = parse_name_status(base_ref, scope)
    if scope == "working-tree":
        changed_rows.extend(("A", path) for path in list_untracked_images())

    changed_images = [
        (status, path)
        for status, path in changed_rows
        if path and is_image_path(path) and is_screenshot_path(path)
    ]
    if not changed_images:
        return []

    base_paths = list_repo_files(base_ref)
    base_signature_index = build_base_signature_index(base_ref, base_paths)
    head_paths = run_git(["ls-files"]).splitlines()
    head_image_paths = [path for path in head_paths if is_image_path(path) and is_screenshot_path(path)]
    head_catalog_index = build_head_catalog(head_image_paths)

    findings: List[DuplicateFinding] = []

    for status, path in changed_images:
        if status == "M":
            base_signature = load_image_signature_from_ref(base_ref, path)
            head_signature = load_image_signature_from_file(path)
            if base_signature == head_signature:
                findings.append(DuplicateFinding(kind="modified-identical", path=path, base_path=path))
            continue

        if status in {"A", "R"}:
            head_signature = load_image_signature_from_file(path)
            matches = base_signature_index.get(head_signature, [])
            if not matches:
                repo_matches = [item for item in head_catalog_index.get(head_signature, []) if item != path]
                if repo_matches:
                    matches = repo_matches
            if matches:
                findings.append(
                    DuplicateFinding(
                        kind="added-duplicate",
                        path=path,
                        base_path=choose_primary(matches),
                    )
                )

    return findings


def apply_findings(base_ref: str, findings: List[DuplicateFinding]) -> None:
    modified_identical = [entry for entry in findings if entry.kind == "modified-identical"]
    added_duplicate = [entry for entry in findings if entry.kind == "added-duplicate"]

    if modified_identical:
        paths = [entry.path for entry in modified_identical]
        run_git(["checkout", base_ref, "--", *paths])

    if added_duplicate:
        tracked_output = run_git(["ls-files", "--", *(entry.path for entry in added_duplicate)], check=False)
        tracked_paths = {line.strip() for line in tracked_output.splitlines() if line.strip()}

        git_rm_paths = [entry.path for entry in added_duplicate if entry.path in tracked_paths]
        delete_paths = [entry.path for entry in added_duplicate if entry.path not in tracked_paths]

        if git_rm_paths:
            run_git(["rm", "-f", "--", *git_rm_paths])

        for path in delete_paths:
            if os.path.exists(path):
                os.remove(path)


def print_findings(findings: List[DuplicateFinding]) -> None:
    if not findings:
        print("No pixel-identical screenshot duplicates detected against base branch.")
        return

    print("Detected pixel-identical screenshot duplicates:")
    for item in findings:
        print(f"- kind={item.kind} path={item.path} base={item.base_path}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Prune screenshot duplicates against a base branch by pixel content.")
    parser.add_argument("--base", default="main", help="Base branch/ref to compare against (default: main)")
    parser.add_argument(
        "--mode",
        choices=["report", "check", "apply"],
        default="report",
        help="report: print findings, check: fail on findings, apply: mutate working tree",
    )
    parser.add_argument(
        "--scope",
        choices=["branch", "working-tree"],
        default="working-tree",
        help="branch: compare committed branch against base, working-tree: include unstaged/staged/untracked changes",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    try:
        run_git(["rev-parse", "--verify", args.base])
    except RuntimeError:
        print(f"Base ref '{args.base}' not found. Fetch branches first.", file=sys.stderr)
        return 2

    findings = find_duplicates(args.base, args.scope)
    print_findings(findings)

    if args.mode == "apply" and findings:
        apply_findings(args.base, findings)
        print("Applied duplicate cleanup actions.")
        return 0

    if args.mode == "check" and findings:
        print("Duplicate screenshots remain; failing check mode.", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())