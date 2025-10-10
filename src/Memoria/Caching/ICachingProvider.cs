namespace Memoria.Caching;

/// <summary>
/// Provides caching functionality for storing and retrieving data.
/// </summary>
public interface ICachingProvider
{
    /// <summary>
    /// Retrieves a cached value of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value or null if not found.</returns>
    Task<T?> Get<T>(string key);

    /// <summary>
    /// Stores data in the cache with an optional expiration time.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="data">The data to cache.</param>
    /// <param name="cacheTimeInSeconds">Optional cache expiration time in seconds.</param>
    Task Set(string key, object? data, int? cacheTimeInSeconds = null);

    /// <summary>
    /// Checks if a value exists in the cache for the specified key.
    /// </summary>
    /// <param name="key">The cache key to check.</param>
    /// <returns>True if the key exists in the cache, otherwise false.</returns>
    Task<bool> IsSet(string key);

    /// <summary>
    /// Removes a cached value for the specified key.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    Task Remove(string key);
}
