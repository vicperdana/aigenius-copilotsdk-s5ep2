#!/usr/bin/env bash
# AI Genius S5E2 — reset the code-along starter to its shipped state.
#
# Rehearsing edits the starter. Run this after every rehearsal, or the best
# beat in the session (Phase 3 with no tool registered) is gone.
#
# Run from the repository root.
set -euo pipefail

dn="src/AgentOrchestrator/samples/SdkLabs/CodeAlong.cs"
py="src/AgentOrchestrator-python/sdk_labs/code_along.py"

changed=0

if [[ -f "$dn" ]]; then
  # Match the indented CODE line only — never a mention inside a comment.
  if grep -qE '^\s+Tools = \[totalTool\]\s*$' "$dn"; then
    perl -0pi -e 's/^[ \t]*var totalTool = CopilotTool\.DefineTool\(GetCustomerTotal\);\n\n//mg' "$dn"
    perl -pi -e 's/^(\s+)Tools = \[totalTool\][ \t]*$/$1Tools = []/' "$dn"
    echo "  reset: $dn"
    changed=1
  else
    echo "  clean: $dn"
  fi
fi

if [[ -f "$py" ]]; then
  if grep -qE '^\s+tools=\[get_customer_total\],\s*$' "$py"; then
    perl -pi -e 's/^(\s+)tools=\[get_customer_total\],[ \t]*$/$1tools=[],/' "$py"
    echo "  reset: $py"
    changed=1
  else
    echo "  clean: $py"
  fi
fi

echo
if [[ "$changed" -eq 1 ]]; then
  echo "Starter restored. Verify with: bash scripts/preflight-codealong.sh"
else
  echo "Nothing to do — starter already in its shipped state."
fi

# The block-level reset above only restores the marked TYPE-THIS-LIVE regions.
# Anything improvised OUTSIDE those blocks during a rehearsal survives it — and
# pre-flight would still pass, because it only greps for the empty tools list.
# So verify against git, which is the only real source of truth.
#
# NOTE: this script calls `exit 1` on a dirty starter. Run it, don't `source` it.
echo
if root="$(git rev-parse --show-toplevel 2>/dev/null)"; then
  # Resolve the starter paths from the repo root, so running this from a
  # subdirectory can't silently skip both files and report a false "clean".
  dirty=""
  missing=""
  for f in "$dn" "$py"; do
    if [[ -f "$root/$f" ]]; then
      # Compare against HEAD, not the index. Plain `git diff` is worktree vs
      # index, so a rehearsal edit that was `git add`ed would pass as clean.
      git -C "$root" diff --quiet HEAD -- "$f" 2>/dev/null || dirty="$dirty $f"
    else
      missing="$missing $f"
    fi
  done

  if [[ -n "$dirty" ]]; then
    echo "❌ Starter still differs from HEAD:"
    for f in $dirty; do echo "     $f"; done
    echo
    echo "   The block reset does not undo edits made outside the marked regions."
    echo "   Restore fully with:"
    for f in $dirty; do echo "     git -C \"$root\" checkout -- $f"; done
    exit 1
  fi

  if [[ -n "$missing" ]]; then
    echo "⚠️  Could not verify (file not found from repo root):"
    for f in $missing; do echo "     $f"; done
    echo "   Expected if you only installed one track. Otherwise, check your install."
  fi

  echo "✅ Starter matches HEAD."
  echo "   (This compares against the last commit — it cannot detect drift you"
  echo "    have already committed. Diff against main if you're unsure.)"
else
  echo "⚠️  Not a git repo — could not verify the starter against a committed baseline."
fi
