# The retail domain

This walkthrough documents the retail analytics data model, SQLite setup, seed
data, and REST endpoints used by the demo. It also calls out the deliberate code
smells that are present for code-review demonstrations.

## Domain models

The API models live under
[`AgentHQDemo.Api/Models`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/tree/main/src/AgentOrchestrator/AgentHQDemo.Api/Models).

[`Transaction`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Models/Transaction.cs)
represents one retail purchase. It has an integer `Id`, `CustomerId`, `Amount`,
`ProductCategory`, `StoreId`, `Timestamp`, and `IsFlagged`. The model includes
Data Annotations such as `Required`, `StringLength`, and `Range`, which ASP.NET
Core model binding can use when controllers check `ModelState`.

[`CustomerSegment`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Models/CustomerSegment.cs)
represents an analytics segment. It stores the segment name, description,
customer count, average monthly spend, and retention rate.

[`SegmentPrediction`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Models/SegmentPrediction.cs)
is an immutable record returned by prediction calls. It contains the customer
ID, predicted segment, confidence score, and top feature names.

## Database and startup seeding

[`RetailDbContext`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Data/RetailDbContext.cs)
is a small EF Core context with two sets:

```csharp
public DbSet<Transaction> Transactions => Set<Transaction>();
public DbSet<CustomerSegment> Segments => Set<CustomerSegment>();
```

[`Program.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Program.cs)
configures SQLite with `Data Source=retail.db`, registers
`RetailAnalyticsService`, and seeds data during application startup:

```csharp
await db.Database.EnsureCreatedAsync();
await service.SeedDataAsync();
```

`RetailAnalyticsService.SeedDataAsync` is idempotent for normal demo restarts:
it returns immediately when any transaction already exists. On a fresh database
it inserts the sample transactions and segments, then saves the changes.

## Seed data

The seed contains 10 transactions across customers `C001` to `C005`:

| Customer | Seeded pattern |
| --- | --- |
| `C001` | Grocery and Electronics purchases totalling 335.49 |
| `C002` | Grocery and Health purchases totalling 47.50 |
| `C003` | Electronics and Fashion purchases totalling 1,700.00 |
| `C004` | Two low-value Grocery purchases totalling 21.49 |
| `C005` | Electronics and Fashion purchases totalling 995.00 |

It also creates four customer segments:

| Segment | Customer count | Average monthly spend | Retention rate | Description |
| --- | ---: | ---: | ---: | --- |
| High Value | 150 | $850 | 92% | Top 10% spenders with strong loyalty indicators |
| Regular | 3,200 | $180 | 78% | Consistent monthly shoppers across categories |
| At Risk | 890 | $95 | 45% | Declining purchase frequency over past 90 days |
| New | 420 | $120 | 65% | Joined within the last 90 days |

## REST endpoints

[`TransactionsController`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Controllers/TransactionsController.cs)
exposes transaction read, create, and delete endpoints:

| Endpoint | Returns |
| --- | --- |
| `GET /api/transactions` | All `Transaction` records. |
| `GET /api/transactions/{id}` | One `Transaction`, or `404` if the controller receives `null`. |
| `POST /api/transactions` | Creates a transaction and returns `201 Created` with the saved record. |
| `DELETE /api/transactions/{id}` | `204 No Content` when deleted, or `404` when not found. |

[`SegmentsController`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Controllers/SegmentsController.cs)
exposes segment and prediction endpoints:

| Endpoint | Returns |
| --- | --- |
| `GET /api/segments` | All `CustomerSegment` records. |
| `GET /api/segments/{id}` | One `CustomerSegment`, or `404` when not found. |
| `GET /api/segments/predict/{customerId}` | A `SegmentPrediction` for that customer. |

The chat endpoints are covered in
[Streaming responses over SSE](./02-sse-streaming.md), because they are part of
the Copilot streaming path rather than the retail data API.

## Segment prediction logic

[`RetailAnalyticsService.PredictSegmentAsync`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Services/RetailAnalyticsService.cs)
loads all transactions for a customer and derives total spend, average spend,
and purchase frequency. It then applies these rules in order:

1. No transactions: return `New` with confidence `0.5` and `no_history`.
2. Total spend above `1000`: return `High Value` with confidence `0.89`.
3. Frequency of three or more: return `Regular` with confidence `0.75`.
4. Average spend below `50`: return `At Risk` with confidence `0.62`.
5. Otherwise: return `Regular` with confidence `0.55`.

The returned `TopFeatures` array explains the rule inputs, such as total spend,
frequency, or average spend.

## Four deliberate code smells

These are intentional demo material for code-review sessions. Do not present
them as accidental bugs to fix during this demo; use them as examples of what a
reviewer should notice and explain.

### 1. N+1 query in `RetailAnalyticsService.GetTransactionsWithSegmentsAsync`

What it is: the method loads all transactions, then loops through each
transaction and calls `PredictSegmentAsync`, which runs another database query
for that customer.

Why it is a problem: the number of queries grows with the number of
transactions. That is acceptable in tiny seed data, but can become slow and
expensive against real retail volumes.

What a reviewer should say: "This is an N+1 query pattern. Consider batching the
customer transaction data or calculating segment predictions from data already
loaded for the request."

### 2. Missing null check in `RetailAnalyticsService.GetTransactionAsync`

What it is: the service uses `FindAsync(id)` and suppresses nullable flow with
`!`, returning `Task<Transaction>` even though the database can return no row.

Why it is a problem: callers cannot tell from the signature that `null` is
possible, and future code could dereference the result before checking it.

What a reviewer should say: "The service contract should reflect the not-found
case, for example by returning `Transaction?` or a result type, and callers
should handle that explicitly."

### 3. No input validation in `RetailAnalyticsService.AddTransactionAsync`

What it is: the service accepts the supplied `Transaction`, stamps
`Timestamp = DateTime.UtcNow`, and saves it without checking amount, customer ID,
category, or store values.

Why it is a problem: `TransactionsController.Create` checks `ModelState`, but
the service itself can still be called directly by tests, other services, or
future endpoints. Invalid domain data could bypass controller validation.

What a reviewer should say: "Do not rely only on controller validation for
domain invariants. Validate the transaction in the service or centralise the
rules so every caller gets the same protection."

### 4. Hardcoded threshold in `RetailAnalyticsService.PredictSegmentAsync`

What it is: the high-value rule uses the literal threshold `1000` in code.

Why it is a problem: business thresholds change by market, season, and retailer.
A magic number hidden in code is hard to audit, tune, or explain to business
stakeholders.

What a reviewer should say: "Move the high-value threshold into configuration or
a policy object, name it clearly, and cover the boundary behaviour in tests."

## Related

- [Embedding the Copilot SDK](./01-copilot-sdk-integration.md)
- [Streaming responses over SSE](./02-sse-streaming.md)
- [The Blazor front end](./04-blazor-ui.md)
- Source:
  [`RetailAnalyticsService.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Services/RetailAnalyticsService.cs),
  [`RetailDbContext.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Data/RetailDbContext.cs),
  [`Program.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Program.cs),
  [`Models`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/tree/main/src/AgentOrchestrator/AgentHQDemo.Api/Models),
  [`TransactionsController.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Controllers/TransactionsController.cs),
  [`SegmentsController.cs`](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/src/AgentOrchestrator/AgentHQDemo.Api/Controllers/SegmentsController.cs)
