# Extra — Extend the API

> **📎 Extra lab — not Copilot SDK.**
> This covers ASP.NET Core, EF Core and xUnit in the demo app. It exercises
> Copilot as a *coding assistant*, but touches none of the Copilot SDK.
> Optional and independent of the numbered SDK path.

**Goal:** add a new endpoint and its tests using Copilot, keeping the existing
14 tests green and the deliberate code smells intact.

**Time:** ~30 minutes

**Prerequisites:** [Extra — Governance hooks](../extra-governance-hooks/) complete, both services
runnable.

## ⚠️ Ground rules

1. **Do not fix the four intentional smells.** Later demos depend on them. If
   Copilot offers to clean up `GetTransactionsWithSegmentsAsync`, decline.
2. **Keep all 14 existing tests passing.** New tests add to that number.
3. Follow the conventions in
   [`copilot-instructions.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/.github/copilot-instructions.md) —
   file-scoped namespaces, primary constructors, `async`/`Async` suffix,
   `CancellationToken`, `record` DTOs.

## What you'll build

`GET /api/segments/summary` — portfolio-level statistics across all segments:

```json
{
  "totalSegments": 4,
  "totalCustomers": 4660,
  "weightedAverageRetention": 0.71,
  "highestRetention": "High Value",
  "lowestRetention": "At Risk"
}
```

Deliberately *not* a trivial passthrough: the weighted average has to weight
retention by customer count, which is exactly the kind of thing worth a test.

## Step 1 — Study the existing shape

```bash
cat src/AgentOrchestrator/AgentHQDemo.Api/Controllers/SegmentsController.cs
```

Note the patterns to mirror:

- Primary constructor injection: `SegmentsController(RetailAnalyticsService service)`
- `[HttpGet("{id:int}")]` route constraints
- `ActionResult<T>` returns, `NotFound()` for missing resources
- Controller stays thin; logic lives in the service

## Step 2 — Look at how tests are written

```bash
cat src/AgentOrchestrator/tests/AgentHQDemo.Tests/RetailAnalyticsServiceTests.cs
```

This test class builds an **in-memory SQLite** context:

```csharp
var options = new DbContextOptionsBuilder<RetailDbContext>()
    .UseSqlite("DataSource=:memory:")
    .Options;

_db = new RetailDbContext(options);
_db.Database.OpenConnection();
_db.Database.EnsureCreated();
```

The `OpenConnection()` call matters — an in-memory SQLite database only lives
as long as its connection is open. Close it and your schema vanishes mid-test.

## Step 3 — Add the DTO

Create `src/AgentOrchestrator/AgentHQDemo.Api/Models/SegmentSummary.cs`:

```csharp
namespace AgentHQDemo.Api.Models;

/// <summary>
/// Portfolio-level statistics across all customer segments.
/// </summary>
public record SegmentSummary(
    int TotalSegments,
    int TotalCustomers,
    decimal WeightedAverageRetention,
    string HighestRetention,
    string LowestRetention);
```

A `record` because it's an immutable DTO — the house style.

## Step 4 — Add the service method

Ask Copilot, giving it the constraints up front:

```
Add a GetSegmentSummaryAsync method to RetailAnalyticsService that returns a
SegmentSummary. Weight the average retention by CustomerCount, not a plain
mean. Handle the empty-segment case without throwing. Accept an optional
CancellationToken and pass it to async EF Core calls. Follow the existing
conventions in this file. Do not modify any other method.
```

The shape you're aiming for:

```csharp
public async Task<SegmentSummary> GetSegmentSummaryAsync(
    CancellationToken cancellationToken = default)
{
    var segments = await _db.Segments.ToListAsync(cancellationToken);

    if (segments.Count == 0)
        return new SegmentSummary(0, 0, 0m, string.Empty, string.Empty);

    var totalCustomers = segments.Sum(s => s.CustomerCount);
    var weighted = totalCustomers == 0
        ? 0m
        : segments.Sum(s => s.RetentionRate * s.CustomerCount) / totalCustomers;

    return new SegmentSummary(
        segments.Count,
        totalCustomers,
        Math.Round(weighted, 2),
        segments.OrderByDescending(s => s.RetentionRate).First().Name,
        segments.OrderBy(s => s.RetentionRate).First().Name);
}
```

⚠️ **Guard the divide.** `totalCustomers` of zero would throw. An empty table is
unlikely with seeding, but tests can construct one — and a reviewer will ask.

## Step 5 — Add the endpoint

In `SegmentsController`:

```csharp
[HttpGet("summary")]
public async Task<ActionResult<SegmentSummary>> GetSummary(
    CancellationToken cancellationToken)
{
    return Ok(await service.GetSegmentSummaryAsync(cancellationToken));
}
```

⚠️ **Route ordering.** `summary` must not be captured by another route. It's
safe here because the sibling route is constrained to `{id:int}` — had it been
a bare `{id}`, `/api/segments/summary` would try to bind "summary" as an id and
fail. Ask Copilot about route precedence if you're unsure.

## Step 6 — Write the tests

```
Add xUnit tests to RetailAnalyticsServiceTests for GetSegmentSummaryAsync.
Cover: the seeded four-segment case, correct weighted average (not a plain
mean), and an empty database returning zeros without throwing. Follow the
existing in-memory SQLite setup in this class. Remember that the constructor
starts with an empty database, so seed the seeded-case tests explicitly.
```

The weighted-average case is the one worth care. With the seed data:

| Segment | Customers | Retention |
|:--------|----------:|----------:|
| High Value | 150 | 0.92 |
| Regular | 3,200 | 0.78 |
| At Risk | 890 | 0.45 |
| New | 420 | 0.65 |

A plain mean gives `0.70`. The weighted figure is
`(150×0.92 + 3200×0.78 + 890×0.45 + 420×0.65) / 4660 ≈ 0.71`.

Those differ — which is precisely why the test is worth writing. Assert the
weighted value and a naive implementation fails loudly.

```csharp
[Fact]
public async Task GetSegmentSummaryAsync_WeightsRetentionByCustomerCount()
{
    await _service.SeedDataAsync();

    var summary = await _service.GetSegmentSummaryAsync();

    Assert.Equal(4, summary.TotalSegments);
    Assert.Equal(4660, summary.TotalCustomers);
    Assert.Equal("High Value", summary.HighestRetention);
    Assert.Equal("At Risk", summary.LowestRetention);
    Assert.Equal(0.71m, summary.WeightedAverageRetention);
    Assert.NotEqual(0.70m, summary.WeightedAverageRetention);
}
```

💡 The in-memory database starts empty. Seed it in tests that assert the four
sample segments; leave it empty for the zero-segment case.

## Step 7 — Build and test

```bash
dotnet build src/AgentOrchestrator/AgentHQDemo.slnx
dotnet test  src/AgentOrchestrator/AgentHQDemo.slnx
```

Expected: a clean build and **more than 14** passing, none failing.

⚠️ If a previously-passing test now fails, something outside your new code
changed. Check the diff:

```bash
git diff --stat
```

Only `SegmentSummary.cs`, `RetailAnalyticsService.cs`, `SegmentsController.cs`,
and the test file should appear.

## Step 8 — Verify it live

```bash
dotnet run --project src/AgentOrchestrator/AgentHQDemo.Api --urls "http://localhost:5050"
```

```bash
curl -s http://localhost:5050/api/segments/summary | jq
```

Confirm the numbers match the table above, and that the existing endpoints
still behave:

```bash
curl -s http://localhost:5050/api/segments | jq 'length'          # 4
curl -s http://localhost:5050/api/segments/predict/C003 | jq -r .predictedSegment
```

## Step 9 — Review your own change

Close the loop with Lab 03's agent:

```bash
copilot --agent dotnet-reviewer -p "Review my uncommitted changes for correctness, async usage, and adherence to .github/copilot-instructions.md. Report only." --allow-all-tools
```

Then update the API table in the root [`README.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/README.md) to list
the new endpoint — docs drift is a review finding too.

## ✅ Checkpoint

- [x] New `record` DTO, service method, and endpoint added
- [x] Tests cover the weighted average and the empty case
- [x] All original 14 tests still pass
- [x] The four intentional smells are untouched
- [x] Endpoint verified against the running API

## 💡 Extra credit

Add `GET /api/transactions/summary` — totals by product category and store.
Consider whether the N+1 pattern from `GetTransactionsWithSegmentsAsync` would
creep in, and write it so it doesn't.

## Related

- Next: [Lab 07 — Wrap-up](../07-wrap-up/)
- [Demo: Retail analytics](../../demos/03-retail-analytics.md)
- [Breakout: Architecture](../../breakouts/architecture.md)
