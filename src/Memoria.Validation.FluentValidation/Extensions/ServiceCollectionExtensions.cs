using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memoria.Validation.FluentValidation.Extensions;

/// <summary>
/// Provides extension methods for registering Memoria FluentValidation services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly Type ValidatorOpenGeneric = typeof(IValidator<>);

    /// <summary>
    /// Adds Memoria FluentValidation services to the specified service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register the services with.</param>
    /// <param name="types">The types whose assemblies are scanned for validators.</param>
    public static void AddMemoriaFluentValidation(this IServiceCollection services, params Type[] types)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Scoped<IValidationProvider, FluentValidationProvider>());

        RegisterValidators(services, types);
    }

    private static void RegisterValidators(IServiceCollection services, Type[] types)
    {
        var assemblies = types.Select(t => t.Assembly).Distinct();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                foreach (var interfaceType in type.GetInterfaces())
                {
                    if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == ValidatorOpenGeneric)
                    {
                        services.AddScoped(interfaceType, type);
                    }
                }
            }
        }
    }
}
