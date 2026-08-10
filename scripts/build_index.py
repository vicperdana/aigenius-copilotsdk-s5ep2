#!/usr/bin/env python3
"""Generate docs/index.md from the root README.md.

The README is the single source of truth. On the site it becomes the landing
page, which means two things have to change:

  * ``docs/...`` links must lose the ``docs/`` prefix, because the site is
    already rooted at ``docs/``.
  * Links to files outside ``docs/`` (source, LICENSE, AGENTS.md and so on)
    have no page on the site, so they are rewritten to absolute GitHub URLs.

Run from the repository root::

    python3 scripts/build_index.py

``docs/index.md`` is generated and gitignored — never edit it by hand.
"""

from __future__ import annotations

import re
from pathlib import Path

REPO = "https://github.com/vicperdana/aigenius-copilotsdk-s5ep2"
BLOB = f"{REPO}/blob/main"

ROOT = Path(__file__).resolve().parent.parent
README = ROOT / "README.md"
INDEX = ROOT / "docs" / "index.md"
README_EDIT_URL = f"{REPO}/edit/main/README.md"

LINK = re.compile(r"(!?\[[^\]]*\]\()([^)]+)(\))")


def rewrite(target: str) -> str:
    """Rewrite one README link target for the site."""
    if target.startswith(("http://", "https://", "#", "mailto:")):
        return target

    path, _, anchor = target.partition("#")
    anchor = f"#{anchor}" if anchor else ""

    if not path:
        return target

    # Links into docs/ become site-relative: the site root *is* docs/.
    if path.startswith("docs/"):
        stripped = path[len("docs/") :]
        # A bare "docs/" would strip to nothing and emit an empty link, so
        # send it to the docs landing page instead.
        return (stripped or "labs/README.md") + anchor

    # Everything else lives outside the site — point at GitHub.
    # Strip only a leading "./" — lstrip("./") would also eat the dot in
    # paths like ".vscode/mcp.json".
    clean = path[2:] if path.startswith("./") else path
    return f"{BLOB}/{clean}{anchor}"


def main() -> int:
    content = README.read_text(encoding="utf-8")
    content = LINK.sub(lambda m: m.group(1) + rewrite(m.group(2)) + m.group(3), content)

    front_matter = f"---\nedit_url: {README_EDIT_URL}\n---\n\n"
    banner = (
        "<!-- Generated from README.md by scripts/build_index.py. "
        "Do not edit by hand. -->\n\n"
    )

    INDEX.parent.mkdir(parents=True, exist_ok=True)
    INDEX.write_text(front_matter + banner + content, encoding="utf-8")
    print(f"wrote {INDEX.relative_to(ROOT)} ({len(content)} chars)")
    return 0


def on_config(config):
    """Regenerate the landing page before MkDocs collects files.

    ``docs/index.md`` is gitignored, so on a clean checkout it does not exist
    yet. This must run before file collection — a later event such as
    ``on_page_context`` fires after nav resolution, by which point MkDocs has
    already failed to find ``index.md``.
    """
    main()
    return config


def on_page_context(context, page, config, nav):
    """Allow generated pages to override MkDocs' computed edit URL."""
    if page.meta.get("edit_url"):
        page.edit_url = page.meta["edit_url"]
    return context


if __name__ == "__main__":
    raise SystemExit(main())
