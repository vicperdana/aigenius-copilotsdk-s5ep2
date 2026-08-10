# Hooks and Governance

This page documents the governance hooks under `.github/hooks/`. The hooks add
session logging, a pre-tool security gate, post-tool audit logging, and a
session summary for the Agent HQ demo.

## `retail-governance.json` schema

`.github/hooks/retail-governance.json` declares a simple hook configuration:

- `version`: numeric configuration version, currently `1`.
- `hooks`: a map where each key is a lifecycle event name.
- Each event value is an array of command entries.
- Each command entry contains:
  - `type`: currently `command`.
  - `bash`: script path to execute.
  - `cwd`: working directory for the command.
  - `timeoutSec`: maximum runtime in seconds.

Illustrative entry:

```json
{
  "type": "command",
  "bash": "./.github/hooks/scripts/security-gate.sh",
  "cwd": ".",
  "timeoutSec": 15
}
```

## Lifecycle events

The governance file wires four lifecycle events to four shell scripts:

| Event | Script | Purpose |
|---|---|---|
| `sessionStart` | `.github/hooks/scripts/session-init.sh` | Initialise session log |
| `preToolUse` | `.github/hooks/scripts/security-gate.sh` | Allow or deny tool use |
| `postToolUse` | `.github/hooks/scripts/audit-logger.sh` | Append JSONL audit event |
| `sessionEnd` | `.github/hooks/scripts/session-end.sh` | Write session summary |

## Script behaviour

### `session-init.sh`

`session-init.sh` reads hook JSON from stdin. It extracts `.source`,
`.timestamp`, and `.cwd` with `jq`, creates `logs/`, and appends a decorated
`SESSION START` block to `logs/session.log` with UTC time, source, cwd, and
the current operating-system user.

### `security-gate.sh`

`security-gate.sh` is the `preToolUse` gate. It reads JSON on stdin and
extracts `.toolName` and `.toolArgs` with `jq`.

For `bash`, it denies commands matching destructive patterns:

- `rm -rf /`
- `rm -rf .`
- `DROP TABLE`
- `DROP DATABASE`
- `format `
- `mkfs.`
- fork-bomb syntax containing `:(){`

It also denies bash commands that reference credential or secret paths and
terms:

- `.env`
- `credentials`
- `secrets`
- `.pem`
- `.key`
- `password`

For `edit` and `create`, it extracts `.toolArgs.path`. Paths that do not match
the hook's allow-list expression — `src/`, `tests/`, `docs/`, or `.github/` —
are denied.

Allowed operations emit:

```json
{"permissionDecision":"allow"}
```

Denied operations append a line to `logs/security-denials.log` and emit this
shape:

```json
{"permissionDecision":"deny","permissionDecisionReason":"Access to credential/secret files blocked by security policy"}
```

### `audit-logger.sh`

`audit-logger.sh` is the `postToolUse` hook. It reads `.toolName`,
`.toolArgs`, `.timestamp`, and `.cwd`, truncates the stringified tool arguments
to 500 characters, categorises the operation, creates `logs/`, and appends one
JSON object per line to `logs/agent-audit.jsonl`.

Categories are:

| Tool name | Category |
|---|---|
| `bash` | `command-execution` |
| `edit` | `code-edit` |
| `create` | `file-creation` |
| `view` | `code-read` |
| `grep`, `glob` | `code-search` |
| anything else | `other` |

The audit record format is:

```json
{
  "timestamp": "input timestamp",
  "logged_at": "UTC write time",
  "tool": "tool name",
  "category": "operation category",
  "args": "first 500 characters of toolArgs",
  "cwd": "working directory"
}
```

### `session-end.sh`

`session-end.sh` reads `.reason` from stdin, counts lines in
`logs/agent-audit.jsonl` and `logs/security-denials.log` when those files
exist, then appends a decorated `SESSION END` block to `logs/session.log`.

## Fixed: `preToolUse` was never running

`retail-governance.json` previously pointed `preToolUse` at
`./.github/hooks/scripts/security-gate-notworking.sh`, which does not exist on
disk. The real script is `security-gate.sh`, so the gate silently never
executed. This has been corrected.

The lesson is that hook misconfiguration can fail silently. Always verify that
a gate actually fires, especially for deny paths that are meant to enforce
security controls.

## Manual hook tests

Run a script directly by piping representative hook JSON into it from the repo
root:

```bash
echo '{"toolName":"bash","toolArgs":{"command":"cat .env"}}' | \
  ./.github/hooks/scripts/security-gate.sh
```

Expected result: a denial JSON object and a new line in
`logs/security-denials.log`.

You can test an allowed path the same way:

```bash
echo '{"toolName":"create","toolArgs":{"path":"docs/example.md"}}' | \
  ./.github/hooks/scripts/security-gate.sh
```

## Temporarily disabling hooks

For a short local experiment, remove the relevant event entry or set that event
array to `[]` in `.github/hooks/retail-governance.json`, then restore it before
committing. Prefer disabling the smallest hook possible; for example, empty
only `preToolUse` if you are testing the security gate configuration.

## Related

- [Architecture](./architecture.md)
- [Troubleshooting](./troubleshooting.md)
- [Governance config](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/.github/hooks/retail-governance.json)
- [Hook scripts](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/tree/main/.github/hooks/scripts)
- [Repository agent guidelines](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/AGENTS.md)
