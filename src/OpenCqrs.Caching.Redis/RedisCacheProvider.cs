using Memoria.Caching;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OpenCqrs.Caching.Redis.Configuration;
using StackExchange.Redis;

namespace OpenCqrs.Caching.Redis;

/// <summary>
/// Provides an implementation of <see cref="ICachingProvider"/> using Redis as the underlying cache.
/// </summary>
/// <remarks>
/// This class uses the StackExchange.Redis library to interact with a Redis data store.
/// It supports basic caching operations such as retrieval, insertion, existence verification, and removal of cache entries.
/// </remarks>
public class RedisCacheProvider(IConnectionMultiplexer connectionMultiplexer, IOptions<RedisCacheOptions> options) : ICachingProvider
{
    private IDatabase Database => connectionMultiplexer.GetDatabase(options.Value.Db, options.Value.AsyncState);

    /// <summary>
    /// Retrieves a cached value for the specified key from the Redis cache.
    /// If the key does not exist or the value cannot be deserialized, returns the default value for the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the value to retrieve and deserialize from the cache.</typeparam>
    /// <param name="key">The key of the cached item.</param>
    /// <returns>The cached value deserialized to the specified type, or the default value for the type if the item does not exist or deserialization fails.</returns>
    public async Task<T?> Get<T>(string key)
    {
        var value = await Database.StringGetAsync(key);
        return value.IsNullOrEmpty ? default : JsonConvert.DeserializeObject<T>(value!);
    }

    /// <summary>
    /// Caches the specified data in the Redis store with an optional expiration time.
    /// </summary>
    /// <param name="key">The key under which the data will be stored in the cache.</param>
    /// <param name="data">The data object to be serialized and cached. If null, no action is taken.</param>
    /// <param name="cacheTimeInSeconds">
    /// The duration, in seconds, for which the data should remain in the cache.
    /// If not specified, the default cache time defined in the options will be used.
    /// </param>
    /// <returns>A task that represents the asynchronous operation of storing data in the cache.</returns>
    public async Task Set(string key, object? data, int? cacheTimeInSeconds = null)
    {
        var json = JsonConvert.SerializeObject(data);
        cacheTimeInSeconds ??= options.Value.DefaultCacheTimeInSeconds;
        await Database.StringSetAsync(key, json, TimeSpan.FromSeconds(cacheTimeInSeconds.Value));
    }

    /// <summary>
    /// Checks if a cached item with the specified key exists in the Redis cache.
    /// </summary>
    /// <param name="key">The key of the cached item to check.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean value indicating whether the cached item exists.</returns>
    public async Task<bool> IsSet(string key)
    {
        return await Database.KeyExistsAsync(key);
    }

    /// <summary>
    /// Removes the specified key and its associated value from the Redis cache.
    /// If the key does not exist, no action is taken.
    /// </summary>
    /// <param name="key">The key of the item to remove from the cache.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Remove(string key)
    {
        await Database.KeyDeleteAsync(key);
    }
}
