# Cache query results

Memoria can cache query results automatically. Inherit from `CacheableQuery<TKey>`, supply a cache key and TTL on the query instance, and the dispatcher returns the cached value when one exists.

This guide assumes a caching provider is registered — see [Configuration: Caching](../reference/configuration/caching.md).

## Make the query cacheable

```C#
public class GetSomething : CacheableQuery<string>;
```

`CacheableQuery<TKey>` already implements `IQuery<TKey>` and adds two properties:

- `CacheKey` — uniquely identifies the cached entry.
- `CacheTimeInSeconds` — how long the entry stays valid.

## Set the key and TTL at dispatch

```C#
var result = await dispatcher.Get(new GetSomething
{
    CacheKey = "product:123",
    CacheTimeInSeconds = 600
});
```

The first call runs the handler and stores the result; subsequent calls within the TTL return the cached value without invoking the handler.

## Picking the cache provider

- **In-memory** — fast, per-process. Use for single-instance services or where staleness across instances is acceptable.
- **Redis** — shared across instances, survives process restarts.

See [Configuration: Caching](../reference/configuration/caching.md) for both.

## Related

- [Quickstart: Mediator](../getting-started/quickstart-mediator.md)
- [Configuration: Caching](../reference/configuration/caching.md)
