# Lab 01 — Setup

**Goal:** set up the Python track for the Copilot SDK, using the Agent HQ demo
app as the runnable vehicle for SDK sessions, streaming, and samples.

**Time:** ~15 minutes

## Prerequisites

You only need one implementation track for the labs. This page uses the Python
sibling under `src/AgentOrchestrator-python/`; the .NET track remains under
`src/AgentOrchestrator/`.

Install these before you start:

- **Python 3.11 or later**
- **[uv](https://docs.astral.sh/uv/getting-started/installation/)** for
  dependency management and command execution
- **[GitHub Copilot CLI](https://github.com/github/copilot-cli)**, signed in
- `git`, `curl`, and `jq`

⚠️ You do **not** need the .NET SDK for this track. The Python SDK package is
`github-copilot-sdk` version **1.0.13**, imported as `copilot`, and the project
pins it in
[`pyproject.toml`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/pyproject.toml).

## Step 1 — Clone and inspect

```bash
git clone https://github.com/vicperdana/aigenius-copilotsdk-s5ep2.git
cd aigenius-copilotsdk-s5ep2
```

The Python implementation lives beside the .NET implementation:

```text
src/AgentOrchestrator-python/
```

Read the Python track overview when you want the full map:

[`src/AgentOrchestrator-python/README.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/README.md)

It mirrors the .NET app feature for feature: same API contract, same seed data,
same deliberate code smells, and the same Copilot SDK learning path.

⚠️ **Pick one track.** You can work Python or .NET without installing both. They
can also run simultaneously because the ports do not overlap: .NET uses 5050
and 5051, while Python uses **5070** for both API and UI.

## Step 2 — Restore dependencies

From the repository root:

```bash
cd src/AgentOrchestrator-python
uv sync
```

`uv sync` creates the virtual environment and installs the application
dependencies, development dependencies, FastAPI, SQLModel, pytest, ruff, and
the Copilot SDK.

⚠️ Run later Python commands from `src/AgentOrchestrator-python` unless the
command explicitly changes directory for you. There is no solution file and no
root-level Python package to run.

## Step 3 — Run the tests

```bash
uv run pytest
```

For the compact CI-style check:

```bash
uv run pytest -q
```

Expected: 33 tests pass. One verified run produced:

```text
.................................                                        [100%]
33 passed in 2.16s
```

That is 14 domain tests, 12 MCP server tests, and 3 model-visibility tests —
all matching the .NET suite one-for-one — plus 4 Python-only contract tests that
guard the browser/API request shape.

Remember that number. Later labs ask you to extend behaviour without breaking
these 33 tests.

## Step 4 — Run the linter

```bash
uv run ruff check .
```

Expected:

```text
All checks passed!
```

The linter configuration lives in `pyproject.toml`.

## Step 5 — Start the API and UI

In your first terminal:

```bash
uv run uvicorn app.main:app --port 5070
```

Open <http://localhost:5070>. You should see the empty chat UI:

![The Retail Analytics Assistant chat UI in its empty state: a dark header with
the model dropdown set to Claude Haiku 4.5, a welcome heading, five suggested
retail questions, and the message input at the
bottom.](../../screenshots/python-chat-ui-empty.png)

⚠️ Unlike the .NET track, there is **no separate UI server**. FastAPI serves the
REST API, chat stream, static HTML, and JavaScript from the same process on port
5070.

💡 **Why 5070 and not 5060?** Chrome, Edge, and Firefox refuse to open port 5060
— it is the SIP port and sits on the browsers' blocked-port list, so the page
fails with `ERR_UNSAFE_PORT` even though `curl` works fine. If you change the
port, avoid 5060, 5061, and 6000.

For edit-refresh development, add `--reload`:

```bash
uv run uvicorn app.main:app --port 5070 --reload
```

⚠️ **Port already in use?** A server from an earlier run may still be alive and
serve stale code. Stop the process listening on 5070 before restarting.

## Step 6 — Verify the health endpoint

In a second terminal, still from `src/AgentOrchestrator-python`:

```bash
curl http://localhost:5070/api/chat/health
```

Expected:

```json
{"status":"healthy","service":"CopilotChat","availableModels":["claude-haiku-4.5","gpt-4.1","gpt-5","claude-sonnet-4.5","claude-opus-4.5","gemini-2.5-pro"]}
```

This endpoint does not call the model. It proves the application is up and shows
the static fallback catalogue used if live model discovery fails.

## Step 7 — Verify the REST API

The Python API keeps the same camelCase JSON contract as .NET. That means
`customerId`, `productCategory`, and `topFeatures`, not Python's internal
`customer_id`, `product_category`, and `top_features`.

Run:

```bash
curl -s http://localhost:5070/api/transactions | jq 'length'
curl -s http://localhost:5070/api/segments | jq 'length'
curl http://localhost:5070/api/segments/predict/C003
```

Expected facts:

- `GET /api/transactions` returns 10 rows
- `GET /api/segments` returns 4 rows
- The prediction call returns:

```json
{"customerId":"C003","predictedSegment":"High Value","confidence":0.89,"topFeatures":["high_total_spend","multi_category","total_1700"]}
```

That camelCase contract is implemented in
[`app/models.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/models.py)
so the same `curl` examples work against both tracks.

## Step 8 — Verify streaming works

This proves the Copilot SDK is wired up, the CLI is signed in, and the API can
stream model output back to the browser or terminal.

```bash
curl -sN -X POST http://localhost:5070/api/chat/stream \
  -H 'Content-Type: application/json' \
  -d '{"prompt":"Reply with exactly: streaming works","model":"claude-haiku-4.5"}'
```

Expected stream shape:

```text
data: {"content": "streaming works"}

data: [DONE]
```

Three things matter: each event starts with `data: `, each event is followed by
a blank line, and the stream ends with `data: [DONE]`.

⚠️ The field is `prompt`, not `message`. An unrecognised key is silently
ignored, so a typo sends an empty prompt and you get a generic greeting back
rather than an error.

⚠️ If you see `data: {"error": "..."}` instead, the server reached the SDK but
the SDK could not complete the request. Common causes are not being signed in to
the Copilot CLI or choosing a model your account cannot use.

## Step 9 — Verify the SDK lab samples

Every later Python SDK lab uses the `sdk_labs` module. Run one real sample now:

```bash
uv run python -m sdk_labs tools
```

Expected:

```text
== Lab 03: tools ==

Model: claude-haiku-4.5
Prompt: How much has customer C003 spent in total?

  [tool] get_customer_total(C003) -> $1,700.00

Assistant: Customer C003 has spent a total of **$1,700.00** across 2 transactions.
```

The command dispatcher lives in
[`sdk_labs/__main__.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/sdk_labs/__main__.py).
Later labs use `uv run python -m sdk_labs events`,
`uv run python -m sdk_labs sessions`, and `uv run python -m sdk_labs mcp`.

## Step 10 — Try the UI

Back in the browser at <http://localhost:5070>, open the **Model** dropdown and
send a message to watch the SSE rendering path.

💡 The request body uses `prompt`, matching `ChatRequest`. An earlier revision
posted `message` here; because Pydantic drops unknown keys, the UI streamed a
reply to an empty prompt and nothing failed loudly. `tests/test_chat_contract.py`
now asserts the field `app.js` sends is the field the API reads.

## ✅ Checkpoint

You should now have:

- [x] Python dependencies restored with `uv sync`
- [x] 33/33 tests passing
- [x] Ruff passing
- [x] API and UI running together on port 5070
- [x] REST endpoints returning seeded camelCase data
- [x] A live streamed response from a real model
- [x] The SDK lab samples reachable through `uv run python -m sdk_labs ...`

## 💡 Extra credit

Query the model endpoint and count what your account offers:

```bash
curl -s http://localhost:5070/api/chat/models | jq 'length'
```

Then inspect the fallback catalogue in
[`app/routers/chat.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/routers/chat.py).
The live list is the source of truth; Lab 02 explains why the static list is only
a fallback.

The count excludes internal-only models: `_is_internal_only` filters any model
whose display name contains "internal" (e.g. `GPT-5.6 Sol Fast (Internal only)`)
so demos and screenshots do not leak your account's access scope.

## Related

- Next: [Lab 02 — First chat](../02-first-chat/)
- [Demo: Copilot SDK integration](../../demos-python/01-copilot-sdk-integration.md)
- [Troubleshooting](../../breakouts/troubleshooting.md)
