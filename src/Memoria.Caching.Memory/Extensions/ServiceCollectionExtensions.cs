using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Memoria.Caching.Memory.Extensions;

/// <summary>
/// Provides extension methods for adding and configuring Memoria memory caching services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds memory caching support for Memoria to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the memory cache to.</param>
    public static void AddMemoriaMemoryCache(this IServiceCollection services)
    {
        services.AddMemoriaMemoryCache(_ => { });
    }

    /// <summary>
    /// Adds memory caching support for Memoria to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the memory cache to.</param>
    /// <param name="options"></param>
    public static void AddMemoriaMemoryCache(this IServiceCollection services, Action<Configuration.MemoryCacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.Configure(options);

        services.AddMemoryCache();

        var serviceProvider = services.BuildServiceProvider();
        var memoryCacheOptions = serviceProvider.GetService<IOptions<Configuration.MemoryCacheOptions>>();

        services.Replace(ServiceDescriptor.Scoped<ICachingProvider>(sp => new MemoryCacheProvider(sp.GetRequiredService<IMemoryCache>(), memoryCacheOptions!)));
    }
}
