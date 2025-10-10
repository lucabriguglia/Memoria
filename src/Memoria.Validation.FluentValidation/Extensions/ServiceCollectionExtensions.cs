using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memoria.Validation.FluentValidation.Extensions;

/// <summary>
/// Provides extension methods for registering Memoria FluentValidation services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Memoria FluentValidation services to the specified service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the services with.</param>
    /// <param name="types">An optional array of <see cref="Type"/> objects representing classes whose assemblies should be scanned for validators.</param>
    public static void AddMemoriaFluentValidation(this IServiceCollection services, params Type[] types)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Scoped<IValidationProvider, FluentValidationProvider>());

        var typeList = types.ToList();

        services.Scan(s => s
            .FromAssembliesOf(typeList)
            .AddClasses(classes => classes.AssignableTo(typeof(IValidator<>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());
    }
}
