#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Apply release-only files and patches from the Arcane private repository."""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
from pathlib import Path


BLOCKED_PARTS = {".git", ".private", "bin", "obj", "release"}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--target", default=Path.cwd(), type=Path)
    args = parser.parse_args()

    source = args.source.resolve(strict=True)
    target = args.target.resolve(strict=True)
    engine = target / "RobustToolbox"

    if not (target / ".git").exists():
        raise SystemExit(f"Target is not a Git checkout: {target}")
    if not (engine / ".git").exists():
        raise SystemExit("RobustToolbox submodule is not initialized")
    if source == target:
        raise SystemExit("Private source must not be the target checkout")

    verify_engine_compatibility(source, engine)

    copied = apply_overlay(source / "overlay", target)
    content_patches = apply_patches(source / "patches" / "content", target)
    engine_patches = apply_patches(source / "patches" / "engine", engine)

    if copied + content_patches + engine_patches == 0:
        raise SystemExit(
            "Private repository contains no overlay files or patches"
        )

    run_git(target, "diff", "--check")
    run_git(engine, "diff", "--check")

    summary = (
        "Arcane private overlay applied: "
        f"{copied} file(s), {content_patches} content patch(es), "
        f"{engine_patches} engine patch(es)."
    )
    print(summary)

    if summary_path := os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(summary_path, "a", encoding="utf-8") as stream:
            stream.write(f"### Private release overlay\n\n{summary}\n")


def apply_overlay(overlay: Path, target: Path) -> int:
    if not overlay.exists():
        return 0
    if not overlay.is_dir() or overlay.is_symlink():
        raise SystemExit(f"Overlay must be a regular directory: {overlay}")

    copied = 0
    for source_path in sorted(overlay.rglob("*")):
        if source_path.is_symlink():
            raise SystemExit(f"Symlinks are not allowed in overlay: {source_path}")
        if source_path.is_dir():
            continue

        relative = source_path.relative_to(overlay)
        validate_relative_path(relative)
        destination = target / relative
        validate_destination(destination, target)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_path, destination)
        copied += 1

    return copied


def apply_patches(directory: Path, checkout: Path) -> int:
    if not directory.exists():
        return 0
    if not directory.is_dir() or directory.is_symlink():
        raise SystemExit(f"Patch path must be a regular directory: {directory}")

    patches = sorted(directory.glob("*.patch"))
    for patch in patches:
        if patch.is_symlink():
            raise SystemExit(f"Symlink patches are not allowed: {patch}")

        run_git(checkout, "apply", "--check", "--whitespace=error-all", str(patch))
        run_git(checkout, "apply", "--whitespace=error-all", str(patch))

    return len(patches)


def validate_relative_path(path: Path) -> None:
    if path.is_absolute() or ".." in path.parts:
        raise SystemExit(f"Unsafe overlay path: {path}")
    if any(part in BLOCKED_PARTS for part in path.parts):
        raise SystemExit(f"Blocked overlay path: {path}")


def validate_destination(destination: Path, target: Path) -> None:
    resolved = destination.resolve(strict=False)
    if resolved != target and target not in resolved.parents:
        raise SystemExit(f"Overlay destination escapes target checkout: {destination}")


def verify_engine_compatibility(source: Path, engine: Path) -> None:
    engine_overlay = source / "overlay" / "RobustToolbox"
    engine_patches = source / "patches" / "engine"
    changes_engine = (
        engine_overlay.exists()
        or (engine_patches.exists() and any(engine_patches.glob("*.patch")))
    )
    if not changes_engine:
        return

    compatibility_file = source / "compat" / "engine-commit"
    if not compatibility_file.is_file() or compatibility_file.is_symlink():
        raise SystemExit(
            "Private engine changes require compat/engine-commit"
        )

    expected = compatibility_file.read_text(encoding="utf-8").strip().lower()
    actual = git_output(engine, "rev-parse", "HEAD").lower()
    if expected != actual:
        raise SystemExit(
            f"Private engine changes target {expected}, but checkout is {actual}"
        )


def run_git(checkout: Path, *args: str) -> None:
    subprocess.run(
        ["git", "-C", str(checkout), *args],
        check=True,
        text=True,
    )


def git_output(checkout: Path, *args: str) -> str:
    return subprocess.check_output(
        ["git", "-C", str(checkout), *args],
        text=True,
    ).strip()


if __name__ == "__main__":
    main()
