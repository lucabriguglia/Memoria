using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.Commands;
using Memoria.Extensions;
using Memoria.Notifications;
using Memoria.Queries;
using Memoria.Tests.Models.Commands;
using Memoria.Tests.Models.Commands.Handlers;
using Memoria.Tests.Models.Notifications;
using Memoria.Tests.Models.Notifications.Handlers;
using Memoria.Tests.Models.Queries;
using Memoria.Tests.Models.Queries.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Memoria.Tests.Features;

public class ServiceCollectionExtensionsTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddMemoria(typeof(ServiceCollectionExtensionsTests));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddMemoria_Should_Register_CommandHandler_Without_Response()
    {
        using var scope = BuildProvider().CreateScope();

        var handler = scope.ServiceProvider.GetService<ICommandHandler<SimpleCommand>>();

        handler.Should().BeOfType<SimpleCommandHandler>();
    }

    [Fact]
    public void AddMemoria_Should_Register_CommandHandler_With_Response()
    {
        using var scope = BuildProvider().CreateScope();

        var handler = scope.ServiceProvider.GetService<ICommandHandler<DoSomething, CommandResponse>>();

        handler.Should().BeOfType<DoSomethingHandler>();
    }

    [Fact]
    public void AddMemoria_Should_Register_QueryHandler()
    {
        using var scope = BuildProvider().CreateScope();

        var handler = scope.ServiceProvider.GetService<IQueryHandler<GetGreeting, string>>();

        handler.Should().BeOfType<GetGreetingHandler>();
    }

    [Fact]
    public void AddMemoria_Should_Register_NotificationHandler()
    {
        using var scope = BuildProvider().CreateScope();

        var handler = scope.ServiceProvider.GetService<INotificationHandler<SomethingElseHappened>>();

        handler.Should().BeOfType<SomethingElseHappenedHandler>();
    }

    [Fact]
    public void AddMemoria_Should_Register_Multiple_NotificationHandlers_For_Same_Notification()
    {
        using var scope = BuildProvider().CreateScope();

        var handlers = scope.ServiceProvider.GetServices<INotificationHandler<SomethingHappened>>().ToList();

        using (new AssertionScope())
        {
            handlers.Should().HaveCount(2);
            handlers.Should().Contain(h => h is SomethingHappenedHandlerOne);
            handlers.Should().Contain(h => h is SomethingHappenedHandlerTwo);
        }
    }

    [Fact]
    public void AddMemoria_Should_Register_Handlers_Using_Scoped_Lifetime()
    {
        var provider = BuildProvider();

        using var scopeOne = provider.CreateScope();
        using var scopeTwo = provider.CreateScope();

        var handlerOne = scopeOne.ServiceProvider.GetService<ICommandHandler<SimpleCommand>>();
        var handlerOneAgain = scopeOne.ServiceProvider.GetService<ICommandHandler<SimpleCommand>>();
        var handlerTwo = scopeTwo.ServiceProvider.GetService<ICommandHandler<SimpleCommand>>();

        using (new AssertionScope())
        {
            handlerOne.Should().BeSameAs(handlerOneAgain);
            handlerOne.Should().NotBeSameAs(handlerTwo);
        }
    }
}
