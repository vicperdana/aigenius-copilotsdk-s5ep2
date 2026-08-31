#!/usr/bin/env bash
# AI Genius S5E2 — pre-flight for the live code-along.
# Run from the repository root. Exits non-zero if anything blocking is wrong.
set -uo pipefail

pass=0
warn=0
fail=0

ok()   { echo "  ✅ $1"; pass=$((pass+1)); }
note() { echo "  ⚠️  $1"; warn=$((warn+1)); }
bad()  { echo "  ❌ $1"; fail=$((fail+1)); }

echo
echo "════════════════════════════════════════════════════════════"
echo " AI Genius S5E2 — code-along pre-flight"
echo "════════════════════════════════════════════════════════════"

echo
echo "── Toolchain ───────────────────────────────────────────────"

if command -v copilot >/dev/null 2>&1; then
  ok "Copilot CLI: $(copilot --version 2>/dev/null | head -1)"
else
  bad "Copilot CLI not found — the SDK talks to this. Install @github/copilot."
fi

if command -v dotnet >/dev/null 2>&1; then
  v="$(dotnet --version 2>/dev/null)"
  case "$v" in
    10.*) ok ".NET SDK: $v" ;;
    *)    note ".NET SDK is $v — the repo targets 10.x" ;;
  esac
else
  note ".NET SDK not found (fine if you're presenting the Python track)"
fi

if command -v uv >/dev/null 2>&1; then
  ok "uv: $(uv --version 2>/dev/null)"
else
  note "uv not found (fine if you're presenting the .NET track)"
fi

echo
echo "── Code-along files ────────────────────────────────────────"

dn="src/AgentOrchestrator/samples/SdkLabs"
py="src/AgentOrchestrator-python/sdk_labs"

for f in "$dn/CodeAlong.cs" "$dn/CodeAlongFinal.cs"; do
  [[ -f "$f" ]] && ok "present: $f" || note "missing: $f"
done
for f in "$py/code_along.py" "$py/code_along_final.py"; do
  [[ -f "$f" ]] && ok "present: $f" || note "missing: $f"
done

echo
echo "── Starter is in its SHIPPED state ─────────────────────────"
echo "   (Phase 3 must start with NO tool registered — that's the"
echo "    'it rummaged my filesystem' beat.)"

if [[ -f "$dn/CodeAlong.cs" ]]; then
  # Anchor to the indented code line, not any mention in a comment.
  if grep -qE '^\s+Tools = \[\]\s*$' "$dn/CodeAlong.cs"; then
    ok ".NET starter: Phase 3 has an empty Tools list"
  else
    bad ".NET starter: Phase 3 already registers the tool — run scripts/reset-codealong.sh"
  fi
fi

if [[ -f "$py/code_along.py" ]]; then
  if grep -qE '^\s+tools=\[\],\s*$' "$py/code_along.py"; then
    ok "Python starter: Phase 3 has an empty tools list"
  else
    bad "Python starter: Phase 3 already registers the tool — run scripts/reset-codealong.sh"
  fi
fi

echo
echo "── Fallbacks actually register the tool ────────────────────"

if [[ -f "$dn/CodeAlongFinal.cs" ]]; then
  grep -q 'Tools = \[totalTool\]' "$dn/CodeAlongFinal.cs" \
    && ok ".NET fallback registers the tool" \
    || bad ".NET fallback does NOT register the tool — your parachute has a hole"
fi

if [[ -f "$py/code_along_final.py" ]]; then
  grep -q 'tools=\[get_customer_total\]' "$py/code_along_final.py" \
    && ok "Python fallback registers the tool" \
    || bad "Python fallback does NOT register the tool — your parachute has a hole"
fi

echo
echo "── Demo app ────────────────────────────────────────────────"

check_url() {
  if curl -fsS --max-time 4 "$1" >/dev/null 2>&1; then ok "$2"; else note "$2 — not responding (start it BEFORE you go live)"; fi
}

check_url "http://localhost:5050/api/chat/health" ".NET API on 5050"
check_url "http://localhost:5051"                  "Blazor UI on 5051"
check_url "http://localhost:5070"                  "Python app on 5070"

count="$(curl -fsS --max-time 4 http://localhost:5050/api/transactions 2>/dev/null | tr ',' '\n' | grep -c transactionId || true)"
[[ "${count:-0}" -gt 0 ]] && ok "seed data reachable" || note "seed data not verified (API may be down)"

echo
echo "── Network (Phase 5 / MCP) ─────────────────────────────────"
# The endpoint only accepts POST, so a 405 still proves reachability.
# Only a connection/DNS failure (empty code) means genuinely blocked.
mcp_code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 8 https://learn.microsoft.com/api/mcp 2>/dev/null)"
if [[ -n "$mcp_code" && "$mcp_code" != "000" ]]; then
  ok "learn.microsoft.com/api/mcp reachable (HTTP $mcp_code)"
else
  note "MCP endpoint unreachable from here — have the Phase 5 recording ready"
fi

echo
echo "════════════════════════════════════════════════════════════"
printf "  %d passed, %d warnings, %d blocking\n" "$pass" "$warn" "$fail"
if [[ "$fail" -gt 0 ]]; then
  echo "  ❌ Fix the blocking items before you present."
  echo "════════════════════════════════════════════════════════════"
  exit 1
fi
echo "  ✅ Good to go. Break a leg."
echo "════════════════════════════════════════════════════════════"
