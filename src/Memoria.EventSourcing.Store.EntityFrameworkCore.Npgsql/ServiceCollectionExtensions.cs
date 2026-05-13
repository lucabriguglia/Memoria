using Memoria.EventSourcing.Store.EntityFrameworkCore.Filtering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql;

/// <summary>
/// DI extensions that install the Npgsql JSON event-data filter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default substring-based <see cref="IEventDataFilter"/> with one that uses Postgres
    /// JSON operators. Call this after <c>AddMemoriaEntityFrameworkCore</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection so calls can be chained.</returns>
    public static IServiceCollection AddMemoriaEntityFrameworkCoreNpgsql(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IEventDataFilter, NpgsqlJsonEventDataFilter>());
        return services;
    }
}
