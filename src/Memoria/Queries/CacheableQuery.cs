namespace OpenCqrs.Queries;

/// <summary>
/// Represents an abstract query with optional caching capabilities, used in the context of the CQRS pattern.
/// This query can be cached with a specified key and duration, enabling more efficient query handling.
/// </summary>
/// <typeparam name="TResult">The type of data expected to be returned by the query.</typeparam>
public abstract class CacheableQuery<TResult> : IQuery<TResult>
{
    /// <summary>
    /// The value indicating the cache key to use if retrieving from the cache.
    /// </summary>
    public required string CacheKey { get; set; }

    /// <summary>
    /// The value indicating the cache time (in seconds). If not set, the value will be taken from configured options.
    /// </summary>
    public int? CacheTimeInSeconds { get; set; }
}
