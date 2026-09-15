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
