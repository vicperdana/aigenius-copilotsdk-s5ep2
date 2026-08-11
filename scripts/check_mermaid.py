#!/usr/bin/env python3
"""Validate every ```mermaid block under docs/ by actually rendering it.

Mermaid failures are invisible until a browser tries to draw the diagram — the
Markdown looks fine, MkDocs builds clean, and the page ships with a red parse
error where a diagram should be. This renders each block with mermaid-cli so a
broken diagram fails the build instead.

Two traps this catches, both hit in this repo:

  * A quoted participant alias (``participant X as "Y"``) is a parse error.
  * HTML entities such as ``&lt;`` contain a semicolon, and ``;`` is a statement
    separator in Mermaid, so the statement is cut short. Angle brackets cannot
    be escaped this way — avoid them in diagram text.

Requires Node. Run from the repository root::

    python3 scripts/check_mermaid.py
"""

from __future__ import annotations

import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"
BLOCK = re.compile(r"```mermaid\n(.*?)```", re.S)
MERMAID_CLI = "@mermaid-js/mermaid-cli@11"


def main() -> int:
    if shutil.which("npx") is None:
        print("npx not found — skipping mermaid validation")
        return 0

    blocks: list[tuple[Path, int, str]] = []
    for page in sorted(DOCS.rglob("*.md")):
        for index, match in enumerate(BLOCK.finditer(page.read_text(encoding="utf-8")), 1):
            blocks.append((page, index, match.group(1)))

    if not blocks:
        print("no mermaid blocks found")
        return 0

    failures: list[str] = []
    with tempfile.TemporaryDirectory() as tmp:
        tmpdir = Path(tmp)
        for page, index, source in blocks:
            src = tmpdir / f"diagram{len(failures)}_{index}.mmd"
            src.write_text(source, encoding="utf-8")
            result = subprocess.run(
                ["npx", "-y", MERMAID_CLI, "-i", str(src), "-o", str(src.with_suffix(".svg"))],
                capture_output=True,
                text=True,
                timeout=300,
            )
            label = f"{page.relative_to(ROOT)} (block {index})"
            if result.returncode != 0:
                detail = (result.stderr or result.stdout).strip().splitlines()
                snippet = "\n      ".join(detail[:4])
                failures.append(f"  {label}\n      {snippet}")
            else:
                print(f"  ok   {label}")

    if failures:
        print(f"\n{len(failures)} mermaid block(s) failed to render:\n")
        print("\n".join(failures))
        return 1

    print(f"\nall {len(blocks)} mermaid block(s) render")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
