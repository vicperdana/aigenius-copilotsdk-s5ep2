# Lab 01 — Setup

**Goal:** set up to work with the Copilot SDK, using the Agent HQ demo app as
the runnable vehicle for SDK sessions, streaming, and samples.

**Time:** ~15 minutes

## Prerequisites

See the [labs README](../README.md#prerequisites). In short: .NET 10 SDK, the
GitHub Copilot CLI (signed in), plus `curl` and `jq`.

## Step 1 — Clone and inspect

```bash
git clone https://github.com/vicperdana/aigenius-copilotsdk-s5ep2.git
cd aigenius-copilotsdk-s5ep2
```

Take a moment to look around:

```bash
ls
```

The layout follows Microsoft Build session-repo conventions — the .NET
implementation lives under `src/AgentOrchestrator/`, which holds both projects
and the tests.

⚠️ **There is no solution file at the repository root.** It lives at
`src/AgentOrchestrator/AgentHQDemo.slnx`, so build and test commands name it
explicitly. A bare `dotnet build` from the root will fail with `MSB1003`.

## Step 2 — Restore and build

```bash
dotnet restore src/AgentOrchestrator/AgentHQDemo.slnx
dotnet build   src/AgentOrchestrator/AgentHQDemo.slnx
```

Expected:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

⚠️ **If you get `MSB3923: Failed to download file ... registry.npmjs.org`**,
your network blocks the npm registry. The Copilot SDK downloads a matching CLI
binary at build time. Install the CLI globally instead and rebuild —
[`Directory.Build.props`](../../../Directory.Build.props) will detect and reuse it:

```bash
npm install -g @github/copilot
```

See [troubleshooting](../../breakouts/troubleshooting.md) for the full set of
overrides.

## Step 3 — Run the tests

```bash
dotnet test src/AgentOrchestrator/AgentHQDemo.slnx
```

Expected:

```
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14
```

Remember that number. Lab 05 asks you to add tests without breaking these.

## Step 4 — Start the API

In your first terminal:

```bash
dotnet run --project src/AgentOrchestrator/AgentHQDemo.Api --urls "http://localhost:5050"
```

On first run the SQLite database is created and seeded automatically — you'll
see EF Core `CREATE TABLE` and `INSERT` statements, then:

```
Now listening on: http://localhost:5050
Application started.
```

## Step 5 — Start the Blazor UI

In a **second** terminal:

```bash
dotnet run --project src/AgentOrchestrator/AgentHQDemo.Web --urls "http://localhost:5051"
```

Then open <http://localhost:5051>.

⚠️ **Port already in use?** A server from an earlier run may still be alive and
will silently serve stale code. Find and stop it:

```bash
lsof -ti:5050        # prints a PID if something is listening
kill <PID>
```

## Step 6 — Verify the REST API

In a third terminal:

```bash
curl -s http://localhost:5050/api/chat/health | jq
curl -s http://localhost:5050/api/transactions | jq 'length'
curl -s http://localhost:5050/api/segments | jq '.[].name'
curl -s http://localhost:5050/api/segments/predict/C003 | jq
```

Expected: health reports `"status":"healthy"`, 10 transactions, four segment
names (High Value, Regular, At Risk, New), and a prediction like:

```json
{
  "customerId": "C003",
  "predictedSegment": "High Value",
  "confidence": 0.89,
  "topFeatures": ["high_total_spend", "multi_category", "total_1700"]
}
```

## Step 7 — Verify streaming works

This is the real test — it proves the Copilot SDK is wired up and authenticated:

```bash
curl -N -X POST http://localhost:5050/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{"prompt":"Reply with just the word OK","model":"claude-haiku-4.5"}'
```

Expected — chunks arriving progressively, then a terminator:

```
data: {"content":"OK"}

data: [DONE]
```

⚠️ **If you see `data: {"error":"..."}` instead**, the SDK reached the CLI but
something failed. Two common causes:

- `Model "..." is not available` — your account can't use that model id. Ask
  the API which models you actually have:
  `curl -s http://localhost:5050/api/chat/models | jq '.[].id'`
- A JSON-RPC or deserialization error — your SDK and CLI versions have drifted
  apart. Run `copilot --version` and check
  [troubleshooting](../../breakouts/troubleshooting.md).

## Step 8 — Verify the SDK labs samples

Every later SDK lab uses the samples project, so build it once and confirm the
CLI entry point is reachable:

```bash
dotnet build src/AgentOrchestrator/samples/SdkLabs
dotnet run --project src/AgentOrchestrator/samples/SdkLabs
```

Expected: the build succeeds, then running with no arguments prints the usage
banner listing the five sample commands:

```
tools
events
permissions
sessions
mcp
```

That confirms the SDK loaded, the project can execute, and the lab commands are
available for the next steps.

## Step 9 — Try the UI

Back in the browser at <http://localhost:5051>:

1. Open the **Model** dropdown — it's populated at runtime from your account,
   so the list is whatever you can genuinely use
2. Ask: *"Which customer segment has the lowest retention?"*
3. Watch the response stream in token by token

## ✅ Checkpoint

You should now have:

- [x] A clean build, 14/14 tests passing
- [x] API on 5050, UI on 5051
- [x] REST endpoints returning seeded data
- [x] A live streamed response from a real model
- [x] The SDK labs samples project building and printing its command banner

## 💡 Extra credit

Query the models endpoint and count what your account offers:

```bash
curl -s http://localhost:5050/api/chat/models | jq 'length'
```

Compare that with the static fallback list in `ChatController.AvailableModels`.
The live list is the source of truth — Lab 02 explains why that matters.

## Related

- Next: [Lab 02 — First chat](../02-first-chat/)
- [Demo: Copilot SDK integration](../../demos/01-copilot-sdk-integration.md)
- [Troubleshooting](../../breakouts/troubleshooting.md)
