# Troubleshooting

This page is a practical problem, cause, and fix reference for the Agent HQ
demo. It focuses on issues shown by the repository files rather than generic
.NET or GitHub Actions behaviour.

## `MSB3923: failed to download Copilot CLI`

**Cause**: `GitHub.Copilot.SDK` downloads a matching Copilot CLI binary from
`registry.npmjs.org` at build time. Corporate proxies or offline development
machines can block that download.

**Fix**: Install the CLI globally and let `Directory.Build.props` auto-detect
it:

```bash
npm install -g @github/copilot
dotnet build src/AgentOrchestrator/AgentHQDemo.slnx
```

Override explicitly if needed:

```bash
dotnet build src/AgentOrchestrator/AgentHQDemo.slnx \
  -p:CopilotCliBinaryPath=/path/to/copilot
```

Opt out of local detection and return to the SDK's normal download behaviour:

```bash
dotnet build src/AgentOrchestrator/AgentHQDemo.slnx \
  -p:CopilotUseLocalCli=false
```

## CodeQL reports no results

**Cause**: This repository uses CodeQL **default setup** rather than a
checked-in workflow, so there is no `codeql.yml` in `.github/workflows/` and no
CodeQL job in the Actions tab. If scanning is not enabled for the repository,
nothing runs at all.

**Fix**: Enable it from **Settings > Code security > Code scanning > CodeQL
analysis > Set up > Default**, then check results under the repository's
**Security** tab rather than in Actions.

Code scanning on a private repository requires GitHub Advanced Security. On a
public repository it is available by default. See
[configuring default setup](https://docs.github.com/en/code-security/code-scanning/enabling-code-scanning/configuring-default-setup-for-code-scanning).

## `Model ... is not available`

**Cause**: The signed-in Copilot account cannot use that model id, or the id is
stale.

**Fix**: Do not hardcode model ids in new code. The app fetches the live list
from `/api/chat/models`, which calls `CopilotChatService.ListModelsAsync` and
falls back to a small static catalogue only if the CLI cannot be reached.

## JSON-RPC or `PingResponse` deserialisation error

**Cause**: A Copilot SDK and Copilot CLI version mismatch can produce
JSON-RPC deserialisation failures.

**Fix**: Upgrade `GitHub.Copilot.SDK` and update the Copilot CLI together.
This repo uses SDK v1.0.13. Also check for v1.0.0 API changes:

- the namespace moved from `GitHub.Copilot.SDK` to `GitHub.Copilot`
- `session.On<T>(...)` now needs an explicit type argument

## Port already in use: 5050 or 5051

**Cause**: Another process is already bound to the API port 5050 or the Blazor
port 5051. A stale server from an earlier run can silently serve old code.

**Fix**: Find the process and stop it:

```bash
lsof -ti:5050
lsof -ti:5051
```

Then stop the returned process id and restart the relevant app:

```bash
kill 12345  # replace 12345 with the process id returned by lsof
dotnet run --project src/AgentOrchestrator/AgentHQDemo.Api --urls "http://localhost:5050"
dotnet run --project src/AgentOrchestrator/AgentHQDemo.Web --urls "http://localhost:5051"
```

## Blazor UI shows an old or invalid model

**Cause**: The selected model is stored in browser localStorage under the web
app. That value can outlive the available model list.

**Fix**: The app resets the stored value in `Home.razor` when the selected
model no longer appears in the runtime model list. If the UI still looks stale,
clear site data for `localhost:5051`.

## `GitHub Actions hosted runners are disabled`

**Cause**: Organisation or enterprise policy has disabled hosted runners. This
is not a repository build or workflow syntax problem.

**Fix**: Ask an organisation or enterprise administrator to enable hosted
runners or provide an approved runner option for this repository.

## Build succeeds but tests cannot find the solution

**Cause**: The solution is not at the repository root. It lives at
`src/AgentOrchestrator/AgentHQDemo.slnx`.

**Fix**: Pass the solution path explicitly:

```bash
dotnet restore src/AgentOrchestrator/AgentHQDemo.slnx
dotnet build src/AgentOrchestrator/AgentHQDemo.slnx
dotnet test src/AgentOrchestrator/AgentHQDemo.slnx
```

This is also what `.github/workflows/ci.yml` uses for restore, build, and test.

## Related

- [Architecture](./architecture.md)
- [Hooks and governance](./hooks-and-governance.md)
- [`Directory.Build.props`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/Directory.Build.props)
- [CI workflow](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/.github/workflows/ci.yml)
- [Review instructions](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/.github/copilot-review-instructions.md)
