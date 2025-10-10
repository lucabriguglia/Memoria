namespace OpenCqrs.Caching.Redis.Configuration;

/// <summary>
/// Provides configuration options for the Redis caching mechanism.
/// </summary>
/// <remarks>
/// This class encapsulates settings for connecting to a Redis database and managing cache behavior.
/// It is used to configure essential parameters like the connection string, database index,
/// and default caching durations.
/// </remarks>
public class RedisCacheOptions
{
    /// <summary>
    /// Gets or sets the default cache duration in seconds.
    /// </summary>
    /// <remarks>
    /// This property defines the default time, in seconds, for which cached data remains valid.
    /// If no specific caching duration is provided when setting a cache entry, this value is used.
    /// The default value is 60 seconds.
    /// </remarks>
    public int DefaultCacheTimeInSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the connection string for the Redis server.
    /// </summary>
    /// <remarks>
    /// This property specifies the endpoint and authentication details required to connect to the Redis server.
    /// It is a required property and must be set for the application to interact with the Redis cache.
    /// </remarks>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Redis database index used for caching operations.
    /// </summary>
    /// <remarks>
    /// This property specifies the database index to be used within the Redis instance.
    /// By default, its value is -1, which indicates the default database.
    /// The value of this property can be modified to target specific Redis databases
    /// when the connection multiplexer retrieves a database during caching operations.
    /// </remarks>
    public int Db { get; set; } = -1;

    /// <summary>
    /// Gets or sets an optional state object used by the Redis database connection.
    /// </summary>
    /// <remarks>
    /// This property is passed when getting a database instance from the Redis connection multiplexer.
    /// It can be used to carry contextual information that might affect database connection behavior.
    /// The default value is null.
    /// </remarks>
    public object? AsyncState { get; set; } = null;
}
