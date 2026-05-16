# Configuration: Caching

To use Memoria's caching features, install and register a caching package.

## In-memory

Install **Memoria.Caching.Memory**:

```C#
services.AddMemoriaMemoryCache();
```

## Redis

Install **Memoria.Caching.Redis**:

```C#
services.AddMemoriaRedisCache(options =>
{
    options.ConnectionString = "localhost:6379";
});
```

## Related

- [Memoria Core](memoria.md)
