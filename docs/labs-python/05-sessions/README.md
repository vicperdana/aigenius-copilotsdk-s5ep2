# Lab 05 — Sessions

**Goal:** give a conversation a stable identity so it survives process
restarts, and understand what session persistence does and does not buy you.

**Time:** ~20 minutes

**Prerequisites:** [Lab 04](../04-events/) complete.

## Step 1 — The problem

Everything so far has been stateless. Each run creates a fresh session, sends a
prompt, and throws the context away. Restart the process and the model has no
idea what you talked about.

That is fine for a one-shot sample and useless for a real assistant. A retail
analyst who asks three follow-up questions expects the fourth to still be about
the same customer.

## Step 2 — Run the sessions sample

```bash
cd src/AgentOrchestrator-python
uv run python -m sdk_labs sessions
```

Add `--model <id>` to override the model:

```bash
uv run python -m sdk_labs sessions --model gpt-5-mini
```

Verified output:

```text
== Lab 05: sessions ==

Model: claude-haiku-4.5
Session id: sdklabs-75e6b8f216d0451b

--- Turn 1 (new session) ---
You: Remember this: my favourite retail segment is 'At Risk'. Reply with just OK.
Assistant: OK

Session closed.

--- Turn 2 (resumed session) ---
You: Which retail segment did I say was my favourite?
Assistant: Your favourite retail segment is 'At Risk'.

--- Session metadata ---
  id=sdklabs-75e6b8f216d0451b metadata retrieved
```

The session id is random per run, so yours will differ. The important proof is
not the id value — it is that turn 2 remembered `'At Risk'` *after the first
session had been closed*.

This run proves resume-after-disposal inside one process. It does not, by
itself, prove cross-device hand-off or restart recovery.

## Step 3 — Give the session an id

Open
[`sessions_sample.py`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/sdk_labs/sessions_sample.py).
The first turn passes an explicit `session_id`:

```python
session_id = f"sdklabs-{uuid.uuid4().hex}"[:24]

session = await client.create_session(
    session_id=session_id,
    model=model_id,
    streaming=False,
)
async with session:
    await send_and_print(
        session,
        "Remember this: my favourite retail segment is 'At Risk'. Reply with just OK.",
    )
```

That id is the key to everything else in this lab. Without it the SDK still
creates a session, but you have no handle to come back to.

The `async with session:` block then closes the session. The next turn is not a
continuation of a live object — it is a genuine resume of a closed one.

## Step 4 — Resume the session

The second turn uses the same id and a different SDK call:

```python
resumed = await client.resume_session(session_id, model=model_id, streaming=False)
async with resumed:
    await send_and_print(resumed, "Which retail segment did I say was my favourite?")
```

💡 **This is simpler than the .NET equivalent.** In C# you must construct a
`ResumeSessionConfig` and pass it as a required second argument; omitting it is
a compile error. Python has no `ResumeSessionConfig`; it takes the same settings
as ordinary keyword arguments, and `session_id` is the only positional one:

```python
await client.resume_session(session_id)                       # valid
await client.resume_session(session_id, model="gpt-5")        # valid
await client.resume_session(session_id, streaming=False)      # valid
```

⚠️ **`session_id` is positional here, but a keyword on create.** Note the
asymmetry: `create_session(session_id=...)` versus
`resume_session(session_id)`. Everything after the session id in
`resume_session` is keyword-only.

To make the proof stronger, run the resume path in a **second process** using
the id printed by the first run:

```bash
uv run python -m sdk_labs sessions --resume sdklabs-75e6b8f216d0451b
```

That genuinely crosses a process boundary. Resuming from another machine
additionally requires access to the same session persistence store, a
compatible runtime, and proper authorisation.

## Step 5 — Discover stored sessions

The sample also asks the SDK for metadata:

```python
metadata = await client.get_session_metadata(session_id)
```

It returns `SessionMetadata | None` — `None` when no stored session matches, so
check before using it:

```python
print(
    "  (no metadata returned)"
    if metadata is None
    else f"  id={session_id} metadata retrieved"
)
```

To browse stored sessions instead of starting from a known id, use
`list_sessions`:

```python
sessions = await client.list_sessions()          # -> list[SessionMetadata]
```

A common pattern is:

1. List sessions for the signed-in user
2. Let the user choose one, or select the most recent
3. Pass that id to `resume_session(id, ...)`

## Step 6 — Connect it to the architecture

Session persistence enables:

- **Hand-off across devices and clients** — start in one place, continue in
  another
- **Crash recovery** — resume after a process restart instead of rebuilding
  context from scratch
- **Auditability** — stable ids make it easier to inspect, organise, and trace
  conversations

In the demo app today, the browser keeps chat history in `localStorage` (see
[`app.js`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/app/static/app.js)).
That works for one browser on one device, but it cannot move the conversation
elsewhere. Open the app on a phone and the history is gone.

SDK sessions are the fix. The UI can store a session id instead of the whole
conversation, and an authorised client using the same session store can resume
the same server-side session.

⚠️ **A session id is not an access control.** Treat ids as identifiers, not
secrets or capabilities. Your app still needs normal user authentication and
authorisation before resuming a stored conversation.

The Python track uses PyPI `github-copilot-sdk` **1.0.13**, imported as
`copilot`, and requires Python 3.11 or later. You can see those requirements in
[`pyproject.toml`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/pyproject.toml).

## ⚠️ Traps

- **Id collisions:** the session id is your key. Reusing an id resumes the old
  conversation instead of creating a clean one.
- **Opaque ids:** random ids work for demos, but real systems should be able to
  map ids back to users, cases, or workflows.
- **Ids are not permissions:** knowing or guessing an id must not be enough to
  access a conversation; enforce authorisation separately.
- **Confidential data in ids:** never put tokens, email addresses, or customer
  details in a session id.
- **Assuming metadata exists:** `get_session_metadata` returns `None` for an
  unknown id; guard before dereferencing it.
- **Forgetting to close:** the sample uses `async with` so turn 1 is genuinely
  closed before turn 2 resumes. Skip that and you have not proved anything.

## 💡 Extra credit

1. Add a third turn after the metadata call, resuming the same id again and
   asking another question about the original `'At Risk'` message
2. Run the `--resume` path from a second terminal to prove cross-process resume
   for yourself
3. List stored sessions and resume the most recent one:

   ```python
   sessions = await client.list_sessions()
   if sessions:
       resumed = await client.resume_session(sessions[0].id, model=model_id)
   ```

4. Change the id to a stable string such as `"analyst-demo"` and observe that
   re-running the sample now continues one long-lived conversation

## ✅ Checkpoint

You can now explain:

- [x] Why process restarts lose context without persistence
- [x] How `create_session(session_id=...)` gives your app a stable key
- [x] That `resume_session(id, ...)` takes plain keyword arguments in Python,
      with no separate config object as .NET requires
- [x] How `get_session_metadata` and `list_sessions` help discover stored
      sessions, and that metadata can be `None`
- [x] Why SDK sessions are the right foundation for cross-device chat history
- [x] Why cross-process and cross-device resume also depend on shared storage,
      compatible runtime behaviour, and authorisation

## Related

- Previous: [Lab 04 — Events](../04-events/)
- Next: [Lab 06 — MCP](../06-mcp/)
- [Demo: Copilot SDK integration](../../demos-python/01-copilot-sdk-integration.md)
- [Troubleshooting](../../breakouts/troubleshooting.md)
