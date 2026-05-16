using FluentAssertions;
using FluentAssertions.Execution;
using FluentValidation;
using Memoria.Validation.FluentValidation.Extensions;
using Memoria.Validation.FluentValidation.Tests.Models.Commands;
using Memoria.Validation.FluentValidation.Tests.Models.Commands.Validators;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Validation.FluentValidation.Tests.Features;

public class ServiceCollectionExtensionsTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddMemoriaFluentValidation(typeof(ServiceCollectionExtensionsTests));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddMemoriaFluentValidation_Should_Register_Concrete_Validator()
    {
        using var scope = BuildProvider().CreateScope();

        var validator = scope.ServiceProvider.GetService<IValidator<DoSomething>>();

        validator.Should().BeOfType<DoSomethingValidator>();
    }

    [Fact]
    public void AddMemoriaFluentValidation_Should_Replace_Default_ValidationProvider()
    {
        using var scope = BuildProvider().CreateScope();

        var provider = scope.ServiceProvider.GetService<IValidationProvider>();

        provider.Should().BeOfType<FluentValidationProvider>();
    }

    [Fact]
    public void AddMemoriaFluentValidation_Should_Register_Validators_Using_Scoped_Lifetime()
    {
        var provider = BuildProvider();

        using var scopeOne = provider.CreateScope();
        using var scopeTwo = provider.CreateScope();

        var validatorOne = scopeOne.ServiceProvider.GetService<IValidator<DoSomething>>();
        var validatorOneAgain = scopeOne.ServiceProvider.GetService<IValidator<DoSomething>>();
        var validatorTwo = scopeTwo.ServiceProvider.GetService<IValidator<DoSomething>>();

        using (new AssertionScope())
        {
            validatorOne.Should().BeSameAs(validatorOneAgain);
            validatorOne.Should().NotBeSameAs(validatorTwo);
        }
    }
}
