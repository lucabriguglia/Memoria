namespace Memoria.Caching.Memory.Configuration;

/// <summary>
/// Represents configuration settings for the in-memory caching mechanism used in the application.
/// </summary>
public class MemoryCacheOptions
{
    /// <summary>
    /// Gets or sets the default cache expiration time in seconds used for caching operations
    /// when no specific expiration duration is provided.
    /// </summary>
    public int DefaultCacheTimeInSeconds { get; set; } = 60;
}
