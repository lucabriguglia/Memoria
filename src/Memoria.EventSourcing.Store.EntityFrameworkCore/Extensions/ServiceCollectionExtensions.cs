using Memoria.EventSourcing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides extension methods for IServiceCollection to add OpenCQRS Entity Framework Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OpenCQRS Entity Framework Core services to the service collection.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the DbContext that implements IDomainDbContext.</typeparam>
    /// <param name="services">The IServiceCollection to add services to.</param>
    public static void AddOpenCqrsEntityFrameworkCore<TDbContext>(this IServiceCollection services) where TDbContext : IDomainDbContext
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IDomainDbContext>(serviceProvider => serviceProvider.GetRequiredService<TDbContext>());
        services.Replace(ServiceDescriptor.Scoped<IDomainService>(provider =>
            new EntityFrameworkCoreDomainService(provider.GetRequiredService<IDomainDbContext>())));
    }
}
