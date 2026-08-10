#!/usr/bin/env python3
"""Rewrite links that point outside docs/ to absolute GitHub URLs.

Markdown under ``docs/`` links to things that live outside it — source files,
``AGENTS.md``, ``.github/`` config. Those work when browsing the repository on
GitHub, but on the generated site there is no such page, so MkDocs reports them
as broken.

Absolute GitHub URLs work in both contexts, so this rewrites them in place.
Idempotent: already-absolute links are left alone, but GitHub links that point
back into this repository are validated so typos do not become silent 404s.

Run from the repository root::

    python3 scripts/rewrite_doc_links.py          # apply
    python3 scripts/rewrite_doc_links.py --check  # report only, non-zero if work needed
"""

from __future__ import annotations

from dataclasses import dataclass
import re
import sys
from pathlib import Path
from urllib.parse import quote, unquote, urlsplit

REPO = "https://github.com/vicperdana/aigenius-copilotsdk-s5ep2"
BLOB = f"{REPO}/blob/main"
TREE = f"{REPO}/tree/main"

GITHUB_HOST = "github.com"
GITHUB_REPO_PATH = "/vicperdana/aigenius-copilotsdk-s5ep2"
GITHUB_BLOB_PREFIX = f"{GITHUB_REPO_PATH}/blob/main/"
GITHUB_TREE_PREFIX = f"{GITHUB_REPO_PATH}/tree/main/"

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"

LINK = re.compile(r"(!?\[[^\]]*\]\()([^)]+)(\))")


@dataclass(frozen=True)
class MissingTarget:
    page: Path
    target: str
    resolved: str
    reason: str

    def format(self) -> str:
        page = self.page.relative_to(ROOT)
        return f"{page}: {self.target} -> {self.resolved} ({self.reason})"


def split_destination(target: str) -> tuple[str, str, bool]:
    """Return the link destination, optional title suffix, and angle-bracket use."""
    if target.startswith("<"):
        end = target.find(">")
        if end != -1:
            return target[1:end], target[end + 1 :], True

    match = re.match(r"([^ \t\n]+)(.*)", target, flags=re.DOTALL)
    if not match:
        return target, "", False
    return match.group(1), match.group(2), False


def join_destination(destination: str, suffix: str, angled: bool) -> str:
    if angled:
        return f"<{destination}>{suffix}"
    return f"{destination}{suffix}"


def repo_url_target(destination: str) -> tuple[str, str] | None:
    """Map an absolute GitHub URL for this repo to (kind, repo-relative path)."""
    parsed = urlsplit(destination)
    if parsed.scheme != "https" or parsed.netloc != GITHUB_HOST:
        return None

    path = unquote(parsed.path)
    if path.startswith(GITHUB_BLOB_PREFIX):
        return "blob", path[len(GITHUB_BLOB_PREFIX) :]
    if path.startswith(GITHUB_TREE_PREFIX):
        return "tree", path[len(GITHUB_TREE_PREFIX) :]
    return None


def validate_repo_path(
    rel_path: str, kind: str, page: Path, target: str
) -> MissingTarget | None:
    candidate = (ROOT / rel_path).resolve(strict=False)

    try:
        rel = candidate.relative_to(ROOT)
    except ValueError:
        return MissingTarget(page, target, str(candidate), "target escapes repository")

    if kind == "blob":
        exists = candidate.is_file()
        reason = "expected file does not exist"
    elif kind == "tree":
        exists = candidate.is_dir()
        reason = "expected directory does not exist"
    else:
        exists = candidate.exists()
        reason = "target does not exist"

    if exists:
        return None
    return MissingTarget(page, target, rel.as_posix(), reason)


def github_url_for(path: Path, anchor: str) -> str:
    rel = path.relative_to(ROOT).as_posix()
    encoded = quote(rel, safe="/")
    base = TREE if path.is_dir() else BLOB
    return f"{base}/{encoded}{anchor}"


def rewrite_destination(destination: str, page: Path) -> tuple[str, MissingTarget | None]:
    """Rewrite one link destination if it escapes docs/, validating targets first."""
    if destination.startswith("#"):
        return destination, None

    parsed = urlsplit(destination)
    if parsed.scheme in {"http", "https"}:
        repo_target = repo_url_target(destination)
        if repo_target is None:
            return destination, None
        kind, rel_path = repo_target
        return destination, validate_repo_path(rel_path, kind, page, destination)

    if parsed.scheme:
        return destination, None

    path, _, anchor_text = destination.partition("#")
    anchor = f"#{anchor_text}" if anchor_text else ""
    if not path:
        return destination, None

    decoded_path = unquote(path)
    resolved = (page.parent / decoded_path).resolve(strict=False)

    try:
        resolved.relative_to(DOCS)
        return destination, None
    except ValueError:
        pass

    try:
        resolved.relative_to(ROOT)
    except ValueError:
        missing = MissingTarget(
            page, destination, str(resolved), "target escapes repository"
        )
        return destination, missing

    if not resolved.exists():
        missing = MissingTarget(
            page,
            destination,
            resolved.relative_to(ROOT).as_posix(),
            "target does not exist",
        )
        return destination, missing

    return github_url_for(resolved, anchor), None


def rewrite(target: str, page: Path) -> tuple[str, MissingTarget | None]:
    """Rewrite one Markdown link target if it escapes docs/."""
    destination, suffix, angled = split_destination(target)
    updated, missing = rewrite_destination(destination, page)
    if missing is not None:
        return target, missing
    return join_destination(updated, suffix, angled), None


def main() -> int:
    check_only = "--check" in sys.argv
    changed: list[str] = []
    missing_targets: list[MissingTarget] = []

    for page in sorted(DOCS.rglob("*.md")):
        original = page.read_text(encoding="utf-8")

        def replace(match: re.Match[str]) -> str:
            updated, missing = rewrite(match.group(2), page)
            if missing is not None:
                missing_targets.append(missing)
            return f"{match.group(1)}{updated}{match.group(3)}"

        updated = LINK.sub(replace, original)
        if updated != original:
            changed.append(str(page.relative_to(ROOT)))
            if not check_only:
                page.write_text(updated, encoding="utf-8")

    verb = "would rewrite" if check_only else "rewrote"
    print(f"{verb} {len(changed)} file(s)")
    for path in changed:
        print(f"  {path}")

    print(f"missing target(s): {len(missing_targets)}")
    for missing in missing_targets:
        print(f"  {missing.format()}")

    if missing_targets:
        return 1
    return 1 if (check_only and changed) else 0


if __name__ == "__main__":
    raise SystemExit(main())
