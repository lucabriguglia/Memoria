using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;

/// <summary>
/// Registration extensions for the Entity Framework Core DCB store.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces any prior <see cref="IDcbStore"/> registration (such as the in-memory default
    /// from <c>AddMemoriaDcb</c>) with the EF Core implementation. The caller is responsible for
    /// registering an <see cref="IDcbDbContext"/> beforehand.
    /// </summary>
    public static IServiceCollection AddMemoriaDcbEntityFrameworkCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.RemoveAll<IDcbStore>();
        services.AddScoped<IDcbStore, EntityFrameworkCoreDcbStore>();
        return services;
    }
}
