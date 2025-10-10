namespace OpenCqrs.Caching;

/// <summary>
/// Provides caching functionality with get-or-set and removal operations.
/// </summary>
public class CachingService(ICachingProvider cachingProvider) : ICachingService
{
    /// <summary>
    /// Gets a cached value by key, or sets it using the provided acquisition function if not found.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="acquire">The function to execute if the value is not found in cache.</param>
    /// <param name="cacheTimeInSeconds">The cache expiration time in seconds.</param>
    /// <returns>The cached or newly acquired value.</returns>
    public async Task<T?> GetOrSet<T>(string key, Func<Task<T>> acquire, int? cacheTimeInSeconds = null)
    {
        var data = await cachingProvider.Get<T>(key);
        if (data != null)
        {
            return data;
        }

        var result = await acquire();
        if (result == null)
        {
            return default;
        }

        await cachingProvider.Set(key, result, cacheTimeInSeconds);

        return result;
    }

    /// <summary>
    /// Removes a cached value by key.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    public async Task Remove(string key)
    {
        await cachingProvider.Remove(key);
    }
}
