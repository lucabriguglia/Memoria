namespace Memoria.Caching;

public class DefaultCachingProvider : ICachingProvider
{
    private static string NotImplementedMessage => "No caching provider has been configured. Please configure a caching provider such as Memory or Redis.";

    public Task<T?> Get<T>(string key)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task Set(string key, object? data, int? cacheTimeInSeconds = null)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<bool> IsSet(string key)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task Remove(string key)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }
}
