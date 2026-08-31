using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;

/// <summary>
/// Registers the Entity Framework Core DCB store.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default no-op <see cref="IDcbDomainService"/> with the Entity Framework Core one.
    /// </summary>
    /// <typeparam name="TDbContext">A context implementing <see cref="IDcbDbContext"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="maxEventsPerAppend">
    /// The batch limit for a single append. Defaults to
    /// <see cref="DbContextExtensions.DcbDbContextExtensions.DefaultMaxEventsPerAppend"/>.
    /// </param>
    /// <example>
    /// <code>
    /// services.AddDbContext&lt;BoxOfficeDbContext&gt;(options =&gt; options.UseSqlServer(connectionString));
    /// services.AddMemoriaDcb(typeof(Program));
    /// services.AddMemoriaDcbEntityFrameworkCore&lt;BoxOfficeDbContext&gt;();
    /// </code>
    /// </example>
    /// <remarks>
    /// Call after <c>AddMemoriaDcb</c>, which registers the service this replaces. Independent of
    /// <c>AddMemoriaEntityFrameworkCore</c>: an application may register both stores, and neither
    /// reads the other's tables.
    /// </remarks>
    public static void AddMemoriaDcbEntityFrameworkCore<TDbContext>(this IServiceCollection services,
        int maxEventsPerAppend = DbContextExtensions.DcbDbContextExtensions.DefaultMaxEventsPerAppend)
        where TDbContext : IDcbDbContext
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IDcbDbContext>(serviceProvider => serviceProvider.GetRequiredService<TDbContext>()!);

        services.Replace(ServiceDescriptor.Scoped<IDcbDomainService>(serviceProvider =>
            new EntityFrameworkCoreDcbDomainService(
                serviceProvider.GetRequiredService<IDcbDbContext>(), maxEventsPerAppend)));
    }
}
