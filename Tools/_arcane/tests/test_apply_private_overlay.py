# SPDX-License-Identifier: AGPL-3.0-or-later

from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "apply_private_overlay.py"


class ApplyPrivateOverlayTest(unittest.TestCase):
    def test_applies_nested_overlay_and_patches(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "arcane"
            engine = target / "RobustToolbox"
            private = target / ".private" / "arcane"

            initialize_repository(target, "content.txt", "public\n")
            initialize_repository(engine, "engine.txt", "public\n")

            overlay_file = private / "overlay" / "Private" / "guard.txt"
            overlay_file.parent.mkdir(parents=True)
            overlay_file.write_text("guard\n", encoding="utf-8")

            write_patch(
                private / "patches" / "content" / "0001-content.patch",
                "content.txt",
            )
            write_patch(
                private / "patches" / "engine" / "0001-engine.patch",
                "engine.txt",
            )
            compatibility = private / "compat" / "engine-commit"
            compatibility.parent.mkdir(parents=True)
            compatibility.write_text(
                git_output(engine, "rev-parse", "HEAD") + "\n",
                encoding="utf-8",
            )

            subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--source",
                    str(private),
                    "--target",
                    str(target),
                ],
                check=True,
            )

            self.assertEqual(
                (target / "Private" / "guard.txt").read_text(encoding="utf-8"),
                "guard\n",
            )
            self.assertEqual(
                (target / "content.txt").read_text(encoding="utf-8"),
                "private\n",
            )
            self.assertEqual(
                (engine / "engine.txt").read_text(encoding="utf-8"),
                "private\n",
            )


def initialize_repository(repository: Path, filename: str, contents: str) -> None:
    repository.mkdir(parents=True)
    run_git(repository, "init")
    run_git(repository, "config", "user.email", "test@example.invalid")
    run_git(repository, "config", "user.name", "Arcane Overlay Test")
    (repository / filename).write_text(contents, encoding="utf-8")
    run_git(repository, "add", filename)
    run_git(repository, "commit", "-m", "Initial")


def write_patch(path: Path, filename: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        f"""diff --git a/{filename} b/{filename}
--- a/{filename}
+++ b/{filename}
@@ -1 +1 @@
-public
+private
""",
        encoding="utf-8",
    )


def run_git(repository: Path, *args: str) -> None:
    subprocess.run(
        ["git", "-C", str(repository), *args],
        check=True,
        stdout=subprocess.DEVNULL,
    )


def git_output(repository: Path, *args: str) -> str:
    return subprocess.check_output(
        ["git", "-C", str(repository), *args],
        text=True,
    ).strip()


if __name__ == "__main__":
    unittest.main()
