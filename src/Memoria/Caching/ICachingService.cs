namespace OpenCqrs.Caching;

/// <summary>
/// Provides caching operations for storing and retrieving data.
/// </summary>
public interface ICachingService
{
    /// <summary>
    /// Gets the cached value for the specified key, or sets it using the provided acquire function if not found.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="acquire">The function to execute if the value is not found in cache.</param>
    /// <param name="cacheTimeInSeconds">The cache expiration time in seconds. If null, uses default expiration.</param>
    /// <returns>The cached or newly acquired value.</returns>
    Task<T?> GetOrSet<T>(string key, Func<Task<T>> acquire, int? cacheTimeInSeconds = null);

    /// <summary>
    /// Removes the cached value for the specified key.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    Task Remove(string key);
}
