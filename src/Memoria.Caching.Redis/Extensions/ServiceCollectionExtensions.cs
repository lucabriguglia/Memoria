using Memoria.Caching.Redis.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Memoria.Caching.Redis.Extensions;

/// <summary>
/// Provides extension methods for integrating Redis caching with the Memoria framework.
/// </summary>
/// <remarks>
/// This static class offers functionality to configure and register Redis caching services within
/// the dependency injection container. It utilizes the Microsoft.Extensions.DependencyInjection namespace
/// to allow seamless integration of Redis caching into applications using the Memoria framework.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures and registers Redis caching services for use with the Memoria framework.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the Redis caching services to.</param>
    /// <param name="options">
    /// An <see cref="Action{RedisCacheOptions}"/> to configure the Redis cache settings,
    /// including the connection string, default cache time, and other optional parameters.
    /// </param>
    /// <remarks>
    /// This method integrates Redis as the caching provider for Memoria, including configuration of
    /// connection multiplexer and Redis cache options. It replaces the default caching provider with Redis.
    /// </remarks>
    public static void AddOpenCqrsRedisCache(this IServiceCollection services, Action<RedisCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.Configure(options);

        var serviceProvider = services.BuildServiceProvider();
        var redisCacheOptions = serviceProvider.GetService<IOptions<RedisCacheOptions>>();

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisCacheOptions!.Value.ConnectionString));
        services.Replace(ServiceDescriptor.Scoped<ICachingProvider>(sp => new RedisCacheProvider(sp.GetRequiredService<IConnectionMultiplexer>(), redisCacheOptions!)));
    }
}
