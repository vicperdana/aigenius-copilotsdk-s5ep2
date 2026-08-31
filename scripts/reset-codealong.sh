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
echo
if git rev-parse --git-dir >/dev/null 2>&1; then
  dirty=""
  for f in "$dn" "$py"; do
    [[ -f "$f" ]] || continue
    git diff --quiet -- "$f" 2>/dev/null || dirty="$dirty $f"
  done

  if [[ -n "$dirty" ]]; then
    echo "❌ Starter still differs from the committed version:"
    for f in $dirty; do echo "     $f"; done
    echo
    echo "   The block reset does not undo edits made outside the marked regions."
    echo "   Restore fully with:"
    for f in $dirty; do echo "     git checkout -- $f"; done
    exit 1
  fi

  echo "✅ Starter is identical to the committed version."
else
  echo "⚠️  Not a git repo — could not verify the starter against a committed baseline."
fi
