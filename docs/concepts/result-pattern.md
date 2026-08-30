# Result Pattern

Every handler and every provider in Memoria returns a `Result` (or `Result<T>`) instead of throwing on failure. This makes outcomes explicit in the type system: callers must look at the result before they can use it, and the framework can compose results without unwinding the stack.

## The two types

- `Result` — success or failure, with no value on success.
- `Result<T>` — success carrying a `T`, or failure.

Both are discriminated unions of `Success` / `Failure`, implemented via the [OneOf](https://github.com/mcintyre321/OneOf) library.

## How handlers use it

```C#
public async Task<Result<Order>> Handle(GetOrder query)
{
    var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == query.Id);
    if (order is null)
    {
        return Result<Order>.Fail("Order not found");
    }

    return Result<Order>.Ok(order);
}
```

The caller sees `Result<Order>`, not `Order` — they can't accidentally use a value that wasn't produced.

## How chained operations compose

When you call `IDomainService.SaveAggregate`, `IDomainService.GetEvents`, or any other framework operation, you get a `Result`. Check `IsSuccess`, unwrap with `.Value`, or short-circuit on failure:

```C#
var aggregateResult = await domainService.GetAggregate(streamId, aggregateId);
if (!aggregateResult.IsSuccess)
{
    return aggregateResult.Error;
}
var aggregate = aggregateResult.Value;
```

## Failure classification

Event store providers classify their failures, so a caller can tell what to do next without knowing
which provider is behind `IDomainService`. Every provider reports the same shapes:

| `Type` | `ErrorCode` | Means | What to do |
|---|---|---|---|
| `memoria/concurrency-conflict` | `Conflict` | The stream moved on between reading its sequence and appending to it | Reload and retry — the tags carry `latestEventSequence` |
| `memoria/storage-failure` | `Error` | The store could not complete the operation | Not retryable on its own; the provider's exception is on the current `Activity` |
| `memoria/batch-limit-exceeded` | `BadRequest` | The write was larger than the provider commits in one atomic unit | Split it across several calls — retrying unchanged cannot succeed. Tags carry `requestedEventCount` and `maximumEventCount` |

The constants live on `StoreFailures`, so you can branch on them without matching strings:

```C#
var result = await domainService.SaveAggregate(streamId, aggregateId, order, expectedEventSequence);

if (!result.IsSuccess && result.Failure!.Type == StoreFailures.ConcurrencyConflictType)
{
    var latest = int.Parse(result.Failure.Tags!["latestEventSequence"]);
    // Reload at `latest`, reapply the decision, and save again.
}
```

### What tags carry, and what they do not

`Tags` carry your own context echoed back — the stream you addressed, the sequences you supplied, the
operation attempted — plus `traceId` when there is a current `Activity`.

They deliberately never carry provider exception detail. Those messages name tables, columns and
constraints, vary by engine and locale, and a `Failure` mapped onto an HTTP response would disclose
them without you deciding to. That detail is recorded on the current `Activity` for operators, and
`traceId` is the handle that leads there.

## Notifications return a list of results

When a notification fans out to multiple handlers, the dispatcher returns the list of every handler's result. This lets you decide what "partial success" means for your domain — proceed if any succeeded, fail if any failed, log and continue, etc.

## Related

- [Overview](overview.md)
- [Configuration: Memoria Core](../reference/configuration/memoria.md)
