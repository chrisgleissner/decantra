#!/usr/bin/env python3
import argparse
import datetime as dt
import hashlib
import json
import os
import pathlib
import re
import shutil
import subprocess
import tempfile
from typing import Any, Dict, List, Optional


def run(cmd: List[str]) -> Dict[str, Any]:
    try:
        completed = subprocess.run(cmd, capture_output=True, text=True, check=False)
        return {
            "ok": completed.returncode == 0,
            "code": completed.returncode,
            "stdout": completed.stdout.strip(),
            "stderr": completed.stderr.strip(),
            "cmd": " ".join(cmd),
        }
    except FileNotFoundError:
        return {"ok": False, "code": 127, "stdout": "", "stderr": f"command not found: {cmd[0]}", "cmd": " ".join(cmd)}


def sha256_file(path: pathlib.Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def parse_aapt_badging(apk_path: pathlib.Path) -> Dict[str, Any]:
    aapt = shutil.which("aapt")
    if not aapt:
        return {"available": False, "reason": "aapt not found"}

    result = run([aapt, "dump", "badging", str(apk_path)])
    if not result["ok"]:
        return {"available": False, "reason": result["stderr"] or "aapt dump failed"}

    pkg_match = re.search(r"package: name='([^']+)' versionCode='([^']+)' versionName='([^']+)'", result["stdout"])
    launch_match = re.search(r"launchable-activity: name='([^']+)'", result["stdout"])

    return {
        "available": True,
        "packageName": pkg_match.group(1) if pkg_match else None,
        "versionCode": pkg_match.group(2) if pkg_match else None,
        "versionName": pkg_match.group(3) if pkg_match else None,
        "launchableActivity": launch_match.group(1) if launch_match else None,
    }


def adb_installed_package(package_name: str, device: Optional[str]) -> Dict[str, Any]:
    adb = shutil.which("adb")
    if not adb:
        return {"available": False, "reason": "adb not found"}

    base_cmd = [adb]
    if device:
        base_cmd += ["-s", device]

    dumpsys = run(base_cmd + ["shell", "dumpsys", "package", package_name])
    if not dumpsys["ok"]:
        return {
            "available": False,
            "reason": dumpsys["stderr"] or dumpsys["stdout"] or "dumpsys failed",
        }

    version_code = None
    version_name = None
    path_match = None

    for line in dumpsys["stdout"].splitlines():
        if version_name is None and "versionName=" in line:
            m = re.search(r"versionName=([^\s]+)", line)
            if m:
                version_name = m.group(1)
        if version_code is None and "versionCode=" in line:
            m = re.search(r"versionCode=(\d+)", line)
            if m:
                version_code = m.group(1)

    pm_path = run(base_cmd + ["shell", "pm", "path", package_name])
    if pm_path["ok"]:
        for line in pm_path["stdout"].splitlines():
            if line.startswith("package:"):
                path_match = line.split("package:", 1)[1].strip()
                break

    pulled_hash = None
    pulled_path = None
    if path_match:
        with tempfile.TemporaryDirectory(prefix="decantra-pull-") as tmp:
            local_path = pathlib.Path(tmp) / "base.apk"
            pull = run(base_cmd + ["pull", path_match, str(local_path)])
            if pull["ok"] and local_path.exists() and local_path.stat().st_size > 0:
                pulled_hash = sha256_file(local_path)
                pulled_path = str(local_path)

    return {
        "available": True,
        "packageName": package_name,
        "versionCode": version_code,
        "versionName": version_name,
        "installedApkPath": path_match,
        "installedApkSha256": pulled_hash,
        "device": device,
        "temporaryPulledPath": pulled_path,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Collect Decantra release provenance and artifact hashes.")
    parser.add_argument("--repo-root", default=".", help="Repository root path")
    parser.add_argument("--tag", default="1.0.1", help="Tag to resolve")
    parser.add_argument("--artifact", action="append", default=[], help="Artifact path (APK/AAB) to hash")
    parser.add_argument("--package", default="uk.gleissner.decantra", help="Android package name for adb inspection")
    parser.add_argument("--device", default=None, help="Optional adb device serial")
    parser.add_argument("--out", default="doc/release/release-provenance.json", help="Output JSON path")
    args = parser.parse_args()

    repo_root = pathlib.Path(args.repo_root).resolve()
    out_path = pathlib.Path(args.out)
    if not out_path.is_absolute():
        out_path = repo_root / out_path
    out_path.parent.mkdir(parents=True, exist_ok=True)

    def git(*git_args: str) -> Dict[str, Any]:
        return run(["git", "-C", str(repo_root), *git_args])

    tag_sha = git("rev-list", "-n", "1", args.tag)
    head_sha = git("rev-parse", "HEAD")
    head_desc = git("describe", "--tags", "--always", "--dirty")
    branch = git("rev-parse", "--abbrev-ref", "HEAD")

    project_version_path = repo_root / "ProjectSettings" / "ProjectVersion.txt"
    unity_version = None
    if project_version_path.exists():
        for line in project_version_path.read_text(encoding="utf-8", errors="ignore").splitlines():
            if line.startswith("m_EditorVersion:"):
                unity_version = line.split(":", 1)[1].strip()
                break

    artifacts: List[Dict[str, Any]] = []
    for artifact in args.artifact:
        artifact_path = pathlib.Path(artifact)
        if not artifact_path.is_absolute():
            artifact_path = repo_root / artifact_path

        entry: Dict[str, Any] = {
            "path": str(artifact_path),
            "exists": artifact_path.exists(),
        }
        if artifact_path.exists() and artifact_path.is_file():
            entry["sizeBytes"] = artifact_path.stat().st_size
            entry["sha256"] = sha256_file(artifact_path)
            suffix = artifact_path.suffix.lower()
            if suffix == ".apk":
                entry["apkMetadata"] = parse_aapt_badging(artifact_path)
            else:
                entry["apkMetadata"] = {"available": False, "reason": f"unsupported suffix {suffix}"}

        artifacts.append(entry)

    output = {
        "generatedAtUtc": dt.datetime.utcnow().isoformat() + "Z",
        "repoRoot": str(repo_root),
        "tag": {
            "name": args.tag,
            "commit": tag_sha["stdout"] if tag_sha["ok"] else None,
            "resolved": tag_sha["ok"],
            "error": None if tag_sha["ok"] else (tag_sha["stderr"] or tag_sha["stdout"]),
        },
        "head": {
            "commit": head_sha["stdout"] if head_sha["ok"] else None,
            "describe": head_desc["stdout"] if head_desc["ok"] else None,
            "branch": branch["stdout"] if branch["ok"] else None,
        },
        "buildInputs": {
            "unityVersion": unity_version,
            "workflowPath": ".github/workflows/build.yml",
            "androidBuildPath": "Assets/Decantra/App/Editor/AndroidBuild.cs",
            "projectVersionPath": "ProjectSettings/ProjectVersion.txt",
        },
        "artifacts": artifacts,
        "installedPackage": adb_installed_package(args.package, args.device),
    }

    out_path.write_text(json.dumps(output, indent=2), encoding="utf-8")
    print(str(out_path))


if __name__ == "__main__":
    main()
