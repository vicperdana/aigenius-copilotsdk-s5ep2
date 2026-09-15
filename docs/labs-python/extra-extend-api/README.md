# Extra — Extend the API

> **📎 Extra lab — not Copilot SDK.**
> This covers FastAPI, SQLModel and pytest in the demo app. It exercises
> Copilot as a *coding assistant*, but touches none of the Copilot SDK.
> Optional and independent of the numbered SDK path.

**Goal:** add a new endpoint and its tests using Copilot, keeping the existing
33 tests green and the deliberate code smells intact.

**Time:** ~30 minutes

**Prerequisites:** [Lab 01 — Setup](../01-setup/) complete, Python app runnable.
If you completed the shared [governance-hooks](../../labs/extra-governance-hooks/)
extra, leave those hooks enabled while you work.

## ⚠️ Ground rules

1. **Do not fix the four intentional smells.** Later demos depend on them. If
   Copilot offers to clean up `get_transactions_with_segments`, decline.
2. **Keep all 14 existing tests passing.** New tests add to that number.
3. Follow the existing Python conventions: thin routers, SQLModel / Pydantic
   DTOs, async service methods, pytest fixtures, and the conduct rules in
   [`AGENTS.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/AGENTS.md).

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
cd src/AgentOrchestrator-python
cat app/routers/segments.py
cat app/services/retail_analytics.py
cat app/models.py
cat app/main.py
```

Note the patterns to mirror:

- `APIRouter(prefix="/api/segments", tags=["segments"])`
- Dependency injection through `Depends(get_service)`
- `response_model=...` on route decorators
- `HTTPException(status_code=404)` for missing resources
- Router stays thin; logic lives in `RetailAnalyticsService`
- `app/main.py` seeds through a lifespan handler and mounts the static UI at `/`
  **last**, so it does not shadow `/api` routes

## Step 2 — Know the Python differences

Models live in `app/models.py` and use **SQLModel**. ⚠️ SQLModel skips
validation on `table=True` classes, so constrained transaction fields live on
`TransactionBase`; both `Transaction` (the table) and `TransactionCreate` (the
request body) inherit from it.

JSON is **camelCase** on the wire (`customerId`, not `customer_id`) through
Pydantic's `alias_generator=to_camel` config. This deliberately preserves the
.NET HTTP contract so the same `curl` commands work against either stack.

Tests use pytest with an in-memory SQLite engine and `StaticPool` in
`tests/conftest.py`. `StaticPool` keeps every connection pointed at the same
in-memory database, which is what `OpenConnection()` achieves on the .NET side.
There are 14 pytest tests, matching the 14 xUnit tests in the .NET track.

## Step 3 — Add the DTO

Add this response model to `app/models.py`:

```python
class SegmentSummary(BaseModel):
    """Portfolio-level statistics across all customer segments."""

    model_config = CAMEL_CONFIG

    total_segments: int
    total_customers: int
    weighted_average_retention: float
    highest_retention: str
    lowest_retention: str
```

A `BaseModel` is enough because this is API shape, not a SQLite table. The
shared `CAMEL_CONFIG` keeps the response as `totalSegments` and
`weightedAverageRetention`.

## Step 4 — Add the service method

Ask Copilot, giving it the constraints up front:

```
Add an async get_segment_summary method to RetailAnalyticsService that returns a
SegmentSummary. Weight the average retention by customer_count, not a plain
mean. Handle the empty-segment case without throwing. Follow the existing
conventions in this file. Do not modify any other method.
```

The shape you're aiming for:

```python
async def get_segment_summary(self) -> SegmentSummary:
    segments = await self.get_segments()
    if not segments:
        return SegmentSummary(
            total_segments=0, total_customers=0, weighted_average_retention=0,
            highest_retention="", lowest_retention="",
        )

    total_customers = sum(s.customer_count for s in segments)
    weighted = 0 if total_customers == 0 else (
        sum(s.retention_rate * s.customer_count for s in segments) / total_customers
    )

    return SegmentSummary(
        total_segments=len(segments),
        total_customers=total_customers,
        weighted_average_retention=round(weighted, 2),
        highest_retention=max(segments, key=lambda s: s.retention_rate).name,
        lowest_retention=min(segments, key=lambda s: s.retention_rate).name,
    )
```

⚠️ **Guard the divide.** `total_customers` of zero would throw. An empty table is
unlikely with seeding, but tests can construct one — and a reviewer will ask.

## Step 5 — Add the endpoint

In `app/routers/segments.py`, import `SegmentSummary` and add:

```python
@router.get("/summary", response_model=SegmentSummary)
async def summary(
    service: RetailAnalyticsService = Depends(get_service),
) -> SegmentSummary:
    return await service.get_segment_summary()
```

⚠️ **Route ordering.** Put `/summary` before `/{segment_id}`. FastAPI routes are
checked in declaration order, and the catch-all segment route can otherwise see
`summary` before validation rejects it as a non-integer id.

## Step 6 — Write the tests

Ask Copilot for service tests in `tests/test_retail_analytics.py`:

```
Add pytest tests for get_segment_summary. Cover: the seeded four-segment case,
correct weighted average (not a plain mean), and an empty database returning
zeros without throwing. The fixture starts empty, so seed explicitly.
```

With the seed data:

| Segment | Customers | Retention |
|:--------|----------:|----------:|
| High Value | 150 | 0.92 |
| Regular | 3,200 | 0.78 |
| At Risk | 890 | 0.45 |
| New | 420 | 0.65 |

A plain mean gives `0.70`. The weighted figure is
`(150×0.92 + 3200×0.78 + 890×0.45 + 420×0.65) / 4660 ≈ 0.71`.

```python
async def test_get_segment_summary_weights_retention_by_customer_count(
    service: RetailAnalyticsService,
) -> None:
    await service.seed_data()

    summary = await service.get_segment_summary()

    assert summary.total_segments == 4
    assert summary.total_customers == 4660
    assert summary.highest_retention == "High Value"
    assert summary.lowest_retention == "At Risk"
    assert summary.weighted_average_retention == 0.71
    assert summary.weighted_average_retention != 0.70
```

💡 Also add an empty-database test that asserts zero totals and empty strings for
`highest_retention` and `lowest_retention`.

## Step 7 — Lint and test

```bash
uv run ruff check .
uv run pytest
```

Expected: Ruff exits clean and pytest reports **more than 14** passing, none
failing.

⚠️ If a previously-passing test now fails, something outside your new code
changed. Check the diff:

```bash
git diff --stat
```

Only `app/models.py`, `app/services/retail_analytics.py`,
`app/routers/segments.py`, and the test file should appear.

## Step 8 — Verify it live

```bash
uv run uvicorn app.main:app --port 5070
```

```bash
curl -s http://localhost:5070/api/segments/summary | jq
curl -s http://localhost:5070/api/segments | jq 'length'          # 4
curl -s http://localhost:5070/api/segments/predict/C003 | jq -r .predictedSegment
```

Confirm the summary numbers match the table above.

Because FastAPI derives OpenAPI from your type hints, the new route also appears
in the interactive docs at <http://localhost:5070/docs> with no extra work —
the response model you declared becomes the documented schema:

![FastAPI's Swagger UI for the AgentHQDemo API (Python), listing the chat
endpoints (GET /api/chat/models, POST /api/chat/stream, POST /api/chat, GET
/api/chat/health), the transactions endpoints, and the start of the segments
group.](../../screenshots/python-swagger-ui.png)

💡 The .NET track exposes the same idea through its OpenAPI document. The
difference is that here the schema comes from Pydantic models and Python type
hints rather than C# attributes.

## Step 9 — Re-check the HTTP contract

FastAPI validation differs in one visible way: invalid input returns **HTTP
422** with a Pydantic error body, where the .NET version returns 400 from
`ModelState`.

Real verified invalid-input output:

```bash
$ curl -X POST http://localhost:5070/api/transactions \
    -H 'Content-Type: application/json' \
    -d '{"customerId":"C777","amount":-5,"productCategory":"Grocery","storeId":"S001"}'
{"detail":[{"type":"greater_than_equal","loc":["body","amount"],"msg":"Input should be greater than or equal to 0.01","input":-5,"ctx":{"ge":0.01}}]}
```

A valid create returns **HTTP 201**. One verified run returned:

```json
{"productCategory":"Grocery","customerId":"C777","amount":42.5,"isFlagged":false,"storeId":"S001","id":11,"timestamp":"2026-08-13T04:59:38.478798"}
```

Cleanup and not-found behaviour:

```bash
curl -X DELETE http://localhost:5070/api/transactions/11   # HTTP 204, no body
curl -s http://localhost:5070/api/transactions/999
```

```json
{"detail":"Not Found"}
```

## Step 10 — Review your own change

```bash
copilot -p "Review my uncommitted Python changes for correctness, FastAPI route ordering, SQLModel/Pydantic validation, and pytest coverage. Report only." --allow-all-tools
```

Then update the API table in the root
[`README.md`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/README.md)
to list the new endpoint — docs drift is a review finding too.

## ✅ Checkpoint

- [x] New response DTO, service method, and endpoint added
- [x] Tests cover the weighted average and the empty case
- [x] All original 33 tests still pass
- [x] The four intentional smells are untouched
- [x] Endpoint verified against the running API on port 5070
- [x] Existing validation, create, delete, and 404 behaviour still match expectations

## 💡 Extra credit

Add `GET /api/transactions/summary` — totals by product category and store.
Consider whether the N+1 pattern from `get_transactions_with_segments` would
creep in, and write it so it doesn't.

## Related

- Next: [Lab 07 — Wrap-up](../07-wrap-up/)
- [Python labs index](../README.md)
- [Python app README](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator-python/README.md)
- [Demo: Retail analytics](../../demos-python/)
- [Breakout: Troubleshooting](../../breakouts/troubleshooting.md)
