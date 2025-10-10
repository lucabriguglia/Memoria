using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Memoria.Caching.Memory;

/// <summary>
/// Provides an implementation of the <see cref="ICachingProvider"/> interface using the in-memory caching mechanism
/// provided by <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/>.
/// </summary>
public class MemoryCacheProvider(IMemoryCache memoryCache, IOptions<Configuration.MemoryCacheOptions> options) : ICachingProvider
{
    /// <summary>
    /// Retrieves a cached value of the specified type associated with the given key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The key of the cached value to retrieve.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the cached value of type T, or null if the key does not exist in the cache.</returns>
    public Task<T?> Get<T>(string key)
    {
        var data = memoryCache.Get<T>(key);
        return Task.FromResult(data);
    }

    /// <summary>
    /// Stores a value in the memory cache with the specified key and optional expiration time.
    /// If the key already exists in the cache, the method will not overwrite the existing value.
    /// </summary>
    /// <param name="key">The key with which the specified data will be associated.</param>
    /// <param name="data">The data to be cached. If null, the value will not be cached.</param>
    /// <param name="cacheTimeInSeconds">Optional cache expiration time in seconds. If not provided, a default value will be used.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task Set(string key, object? data, int? cacheTimeInSeconds = null)
    {
        if (data is null)
        {
            return;
        }

        var isSet = await IsSet(key);
        if (isSet)
        {
            return;
        }

        cacheTimeInSeconds ??= options.Value.DefaultCacheTimeInSeconds;
        var memoryCacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(cacheTimeInSeconds.Value));

        memoryCache.Set(key, data, memoryCacheEntryOptions);
    }

    /// <summary>
    /// Checks whether a given key exists in the memory cache.
    /// </summary>
    /// <param name="key">The key to check for existence in the cache.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the key exists in the cache; otherwise, false.</returns>
    public Task<bool> IsSet(string key)
    {
        var ieSet = memoryCache.Get(key) is not null;
        return Task.FromResult(ieSet);
    }

    /// <summary>
    /// Removes the cached value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the cached value to remove.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation was successful.</returns>
    public Task Remove(string key)
    {
        memoryCache.Remove(key);
        return Task.FromResult(true);
    }
}
