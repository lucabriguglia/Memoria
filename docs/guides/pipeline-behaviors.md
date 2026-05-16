# Pipeline behaviors

Pipeline behaviors wrap command and query handling so you can add cross-cutting concerns — logging, timing, authorization, transactions — without touching individual handlers.

A behavior implements `IPipelineBehavior<TRequest, TResponse>` (for `ICommand<TResponse>` and `IQuery<TResult>`) or `IPipelineBehavior<TRequest>` (for `ICommand` with no response). It receives the request and a `next` continuation. Calling `next()` runs the next behavior or — if this is the innermost one — the terminal handler.

## Define a behavior

```C#
using Memoria.Pipeline;
using Memoria.Results;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => this.logger = logger;

    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var result = await next();
        logger.LogInformation("Handled {Request}: success={Success}", typeof(TRequest).Name, result.IsSuccess);
        return result;
    }
}
```

## Register it

```C#
services.AddMemoria(typeof(Program));
services.AddMemoriaBehavior(typeof(LoggingBehavior<,>));   // open generic
services.AddMemoriaBehavior<MyClosedBehavior>();           // closed generic
```

Behaviors are **opt-in**. `AddMemoria(...)` does not auto-discover them — you register each one explicitly.

## Ordering

Behaviors run in **registration order**, with the first registered behavior wrapping the outermost layer:

```C#
services.AddMemoriaBehavior(typeof(LoggingBehavior<,>));
services.AddMemoriaBehavior(typeof(TimingBehavior<,>));
```

Produces this call order around the handler:

```
Logging:enter → Timing:enter → handler → Timing:exit → Logging:exit
```

## Short-circuit by returning a Failure

Skip the rest of the pipeline by returning a `Result` without calling `next()`. The handler never runs.

```C#
public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!CurrentUserIsAuthorized(request))
        {
            return Task.FromResult(Result<TResponse>.Fail(title: "Forbidden"));
        }
        return next();
    }
}
```

This is the idiomatic precondition pattern in Memoria — no exceptions, just a `Failure` propagated through the `Result` type. See [Result pattern](../concepts/result-pattern.md).

## Sample: timing

```C#
public class TimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TimingBehavior<TRequest, TResponse>> logger;

    public TimingBehavior(ILogger<TimingBehavior<TRequest, TResponse>> logger) => this.logger = logger;

    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await next();
        }
        finally
        {
            logger.LogInformation("{Request} took {Ms}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        }
    }
}
```

## Sample: unit of work

Commit `DbContext` changes only after the handler succeeds. The `Result` makes the rollback path explicit — no exception-driven control flow.

```C#
public class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly MyDbContext db;

    public UnitOfWorkBehavior(MyDbContext db) => this.db = db;

    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var result = await next();
        if (result.IsSuccess)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        return result;
    }
}
```

## What pipeline behaviors don't cover (yet)

- **Notifications** — `INotification` fan-out is not wrapped. Wrapping a single behavior around N handlers has ambiguous semantics; this may change in a future release.
- **Command sequences** — each step in an `ICommandSequence` runs without per-step behaviors. Dispatch each command individually through the dispatcher if you need behaviors per command.
- **Validation and caching** — Memoria has dedicated, longer-standing mechanisms for these: pass `validateCommand: true` to opt in, or inherit from `CacheableQuery<T>`. They are not implemented as pipeline behaviors today. See [Validate commands](validate-commands.md) and [Cache query results](cache-queries.md).

## Related

- [Result pattern](../concepts/result-pattern.md)
- [Quickstart: Mediator](../getting-started/quickstart-mediator.md)
- [Validate commands](validate-commands.md)
- [Cache query results](cache-queries.md)
