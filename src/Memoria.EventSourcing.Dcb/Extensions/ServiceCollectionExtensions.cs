using Microsoft.Extensions.DependencyInjection;

namespace Memoria.EventSourcing.Dcb.Extensions;

/// <summary>
/// Registration extensions for the Dynamic Consistency Boundary event store.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DCB in-memory default. Storage adapters (EF Core, etc.) replace
    /// the <see cref="IDcbStore"/> registration with their own scoped implementation.
    /// </summary>
    public static IServiceCollection AddMemoriaDcb(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IDcbStore, InMemoryDcbStore>();
        return services;
    }
}
