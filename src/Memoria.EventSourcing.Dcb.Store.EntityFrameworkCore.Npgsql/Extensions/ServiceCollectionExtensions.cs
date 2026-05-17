using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql.Extensions;

/// <summary>
/// Registration extensions for the Postgres-flavoured DCB store.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces any prior <see cref="IDcbStore"/> registration with the Npgsql implementation
    /// (advisory-lock based concurrency control). The caller is responsible for registering
    /// an <see cref="IDcbDbContext"/> backed by an Npgsql provider.
    /// </summary>
    public static IServiceCollection AddMemoriaDcbEntityFrameworkCoreNpgsql(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.RemoveAll<IDcbStore>();
        services.AddScoped<IDcbStore, NpgsqlDcbStore>();
        return services;
    }
}
