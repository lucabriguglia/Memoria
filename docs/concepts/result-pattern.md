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

## Notifications return a list of results

When a notification fans out to multiple handlers, the dispatcher returns the list of every handler's result. This lets you decide what "partial success" means for your domain — proceed if any succeeded, fail if any failed, log and continue, etc.

## Related

- [Overview](overview.md)
- [Configuration: Memoria Core](../reference/configuration/memoria.md)
