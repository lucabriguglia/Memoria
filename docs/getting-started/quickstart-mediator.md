# Quickstart: Mediator

A five-minute walk-through of the three message kinds Memoria dispatches: **commands**, **queries**, and **notifications**. Every handler returns a [`Result`](../concepts/result-pattern.md) so failures travel through types, not exceptions.

If you haven't already: [install](install.md) the `Memoria` package.

## 1. Register Memoria

In `Program.cs` (or wherever you build your `IServiceCollection`):

```C#
services.AddMemoria(typeof(Program));
```

That single call scans the supplied assembly for command, query, and notification handlers and registers them. Pass one type per assembly that contains handlers.

For the full configuration surface, see [Configuration: Memoria Core](../reference/configuration/memoria.md).

## 2. Dispatch a command

A command is a write operation handled by exactly one handler.

```C#
public class CreateProduct : ICommand
{
    public string Name { get; init; } = string.Empty;
}

public class CreateProductHandler : ICommandHandler<CreateProduct>
{
    private readonly IProductService _products;

    public CreateProductHandler(IProductService products) => _products = products;

    public async Task<Result> Handle(CreateProduct command)
    {
        await _products.Create(command.Name);
        return Result.Ok();
    }
}

// Anywhere you have IDispatcher injected:
var result = await dispatcher.Send(new CreateProduct { Name = "Espresso" });
```

Commands can return a value by implementing `ICommand<TResponse>` and `ICommandHandler<TCommand, TResponse>`. Beyond the happy path, see [Commands](../Commands.md) for validation, custom handlers, command sequences, and publishing notifications/messages on success.

## 3. Run a query

A query is a read operation handled by exactly one handler.

```C#
public class GetProduct : IQuery<Product>
{
    public int Id { get; init; }
}

public class GetProductHandler : IQueryHandler<GetProduct, Product>
{
    private readonly MyDbContext _db;

    public GetProductHandler(MyDbContext db) => _db = db;

    public async Task<Result<Product>> Handle(GetProduct query)
    {
        var product = await _db.Products.FindAsync(query.Id);
        return product is null
            ? Result<Product>.Fail("Not found")
            : Result<Product>.Ok(product);
    }
}

var result = await dispatcher.Get(new GetProduct { Id = 123 });
```

Need automatic caching? See [Queries](../Queries.md) for the `CacheableQuery` base class.

## 4. Publish a notification

A notification is a fan-out message. Any number of handlers can subscribe to the same notification type.

```C#
public class ProductCreated : INotification
{
    public int Id { get; init; }
}

public class SendWelcomeEmail : INotificationHandler<ProductCreated>
{
    public Task<Result> Handle(ProductCreated notification)
    {
        // …send an email
        return Task.FromResult(Result.Ok());
    }
}

public class IndexInSearch : INotificationHandler<ProductCreated>
{
    public Task<Result> Handle(ProductCreated notification)
    {
        // …push to search index
        return Task.FromResult(Result.Ok());
    }
}

await dispatcher.Publish(new ProductCreated { Id = 123 });
```

The dispatcher invokes every registered handler and returns the list of their results. Decide what "partial success" means for your domain.

## Where to go next

- [Quickstart: Event Sourcing](quickstart-event-sourcing.md) — same dispatcher, plus aggregates and streams.
- [Concepts: Overview](../concepts/overview.md) — when to add event sourcing.
- [Reference: Domain Service](../reference/domain-service.md) — full API surface once you do.
