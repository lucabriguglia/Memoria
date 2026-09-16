using System.Reflection;

namespace Memoria.Web.Extensibility;

/// <summary>
/// Registration for the domain types uploaded through the settings page.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the store the uploads land in and the registry that reads them.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="store">Where the uploaded assemblies live.</param>
    /// <param name="host">The application's own assembly, scanned alongside the uploads.</param>
    /// <remarks>
    /// Nothing is loaded here. The registry is a singleton because the bindings it rebuilds are
    /// process-wide, and it is reloaded once the application is built and again on every upload or
    /// refresh. The service a request is inside is scoped, since a request is under one or none.
    /// </remarks>
    public static void AddDomainExtensions(
        this IServiceCollection services, ExtensionStore store, Assembly? host = null)
    {
        services.AddSingleton(store);
        services.AddSingleton(new DomainTypeRegistry(store, host));
        services.AddScoped<CurrentService>();
    }
}
