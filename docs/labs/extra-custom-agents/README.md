# Extra — Custom agents and code review

> **📎 Extra lab — not Copilot SDK.**
> This covers `.agent.md` files, a **Copilot CLI** feature, not the Copilot
> SDK. It's genuinely useful, but optional and independent of the numbered
> SDK path. Start with [Lab 01](../01-setup/) if you're here for the SDK.

**Goal:** use the repository's custom agents to discover the four deliberate
code smells, and understand how agents, instructions, and skills combine to
encode a team's review standards.

**Time:** ~20 minutes

**Prerequisites:** [Lab 02](../02-first-chat/) complete. You need the GitHub
Copilot CLI signed in, or VS Code with Copilot Chat.

## ⚠️ Read this first

The issues you are about to find are **intentional**. They exist so reviews
have something real to catch. **Do not fix them** — Lab 05, the demo script,
and the review-instructions file all assume they're still present.

Your job here is to *detect and describe*, not repair.

## Step 1 — Look at what's checked in

```bash
ls .github/agents/
cat .github/agents/dotnet-reviewer.agent.md
```

Each agent is a Markdown file with YAML frontmatter:

```yaml
---
name: dotnet-reviewer
description: Senior .NET code reviewer specializing in C# best practices, security, and performance
tools: ['agent', 'read', 'search']
model: claude-sonnet-4.6
---
```

- **`name`** — how you invoke it
- **`description`** — tells Copilot *when* to reach for this agent
- **`tools`** — capabilities it's allowed; these are read-only reviewers, so no
  `edit` or `bash`
- **`model`** — optionally pins a specific model

Four agents ship here:

| Agent | Purpose |
|:------|:--------|
| `dotnet-reviewer` | C# best practices, security, performance |
| `security-scanner` | Vulnerabilities and compliance issues |
| `pr-summary` | Generates PR descriptions from a diff |
| `accessibility-auditor` | WCAG compliance for UI code |

## Step 2 — Understand the shared context

Agents don't work in isolation. Two instruction files apply repo-wide:

```bash
head -40 .github/copilot-instructions.md
head -30 .github/copilot-review-instructions.md
```

- [`copilot-instructions.md`](../../../.github/copilot-instructions.md) —
  coding standards every agent follows (file-scoped namespaces, async
  conventions, `Result<T>` over exceptions, and so on)
- [`copilot-review-instructions.md`](../../../.github/copilot-review-instructions.md) —
  review-specific context: the SDK namespace move, the SSE flush requirement,
  and an explicit list of the intentional smells so reviewers don't report them
  as new bugs

This layering is the point: standards live in version control, so every
reviewer — human or agent — applies the same ones.

## Step 3 — Review the service with an agent

Run the .NET reviewer against the file that holds most of the issues:

```bash
copilot --agent dotnet-reviewer -p "Review src/AgentOrchestrator/AgentHQDemo.Api/Services/RetailAnalyticsService.cs for performance and correctness issues. List each with severity and a suggested fix, but do not modify any files." --allow-all-tools
```

In VS Code Copilot Chat the equivalent is:

```
@dotnet-reviewer review RetailAnalyticsService.cs for performance and correctness issues
```

## Step 4 — Check the findings against the answer key

A good review should surface all four. The answer key:

| # | Issue | Where | Why it matters |
|:--|:------|:------|:---------------|
| 1 | **N+1 query** | `GetTransactionsWithSegmentsAsync` | Loads all transactions, then calls `PredictSegmentAsync` per row — one extra round trip each. Fine for 10 seed rows, fatal at 10 million. |
| 2 | **Missing null check** | `GetTransactionAsync` | Returns without guarding a missing id, so callers can dereference null. |
| 3 | **No input validation** | `AddTransactionAsync` | Accepts unvalidated input — negative amounts, empty customer ids, absurd values all persist. |
| 4 | **Hardcoded threshold** | `PredictSegmentAsync` | A magic number decides segment membership. Changing business rules means a redeploy. |

The N+1 is visible directly in the source — the comment even flags it:

```csharp
foreach (var txn in transactions)
{
    // N+1: querying segments for every single transaction
    var segment = await PredictSegmentAsync(txn.CustomerId);
    ...
}
```

💡 How many did the agent find? Agents are probabilistic — a run that finds
three of four is normal. That's a useful discussion point: agents accelerate
review, they don't replace the reviewer.

## Step 5 — Chain to the security scanner

Different agent, different lens. The security scanner should focus on issue 3:

```bash
copilot --agent security-scanner -p "Scan src/AgentOrchestrator/AgentHQDemo.Api/Controllers/TransactionsController.cs and the service it calls for input validation and injection risks. Report findings only, make no edits." --allow-all-tools
```

Compare the two outputs. The .NET reviewer weighs performance; the security
scanner weighs trust boundaries. Same code, different priorities — which is
exactly why they're separate agents rather than one general one.

## Step 6 — Audit the UI for accessibility

```bash
copilot --agent accessibility-auditor -p "Audit src/AgentOrchestrator/AgentHQDemo.Web/Components/ChatInput.razor and Header.razor for WCAG issues. Report only." --allow-all-tools
```

Look for label associations, keyboard operability, and focus management on the
streaming message region.

## Step 7 — Generate a PR summary

With uncommitted changes present, `pr-summary` drafts a description:

```bash
copilot --agent pr-summary -p "Summarise the current git diff as a pull request description." --allow-all-tools
```

## ✅ Checkpoint

- [x] You can explain the `.agent.md` frontmatter fields
- [x] You found the four intentional issues — **and left them in place**
- [x] You saw two agents reach different conclusions about the same code
- [x] You understand how instruction files give every agent shared standards

## 💡 Extra credit

Write a fifth agent. Create `.github/agents/test-writer.agent.md` with a
`description` explaining when it should trigger and `tools: ['read', 'search']`.
Ask it to propose (not write) tests for `PredictSegmentAsync`.

## Related

- Next: [Extra — Governance hooks](../extra-governance-hooks/)
- [Breakout: Custom agents](../../breakouts/custom-agents.md)
- [Breakout: Skills](../../breakouts/skills.md)
- [Demo: Retail analytics](../../demos/03-retail-analytics.md)
