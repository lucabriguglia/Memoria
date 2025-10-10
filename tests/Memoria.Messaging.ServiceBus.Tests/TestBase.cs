using Memoria.Commands;
using Memoria.Messaging.ServiceBus.Tests.Models.Commands;
using Memoria.Messaging.ServiceBus.Tests.Models.Commands.Handlers;
using Memoria.Notifications;
using Memoria.Queries;
using Memoria.Validation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Memoria.Messaging.ServiceBus.Tests;

public abstract class TestBase
{
    protected readonly MockServiceBusTestHelper MockServiceBusTestHelper;
    protected readonly IDispatcher Dispatcher;

    protected TestBase()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICommandHandler<DoSomething, CommandResponse>, DoSomethingHandler>()
            .BuildServiceProvider();

        MockServiceBusTestHelper = new MockServiceBusTestHelper();
        var serviceBusMessagingProvider = new ServiceBusMessagingProvider(MockServiceBusTestHelper.MockServiceBusClient);
        var messagePublisher = new MessagePublisher(serviceBusMessagingProvider);
        var commandSender = new CommandSender(serviceProvider, Substitute.For<IValidationService>(), Substitute.For<INotificationPublisher>(), messagePublisher);

        Dispatcher = new Dispatcher(commandSender, Substitute.For<IQueryProcessor>(), Substitute.For<INotificationPublisher>());
    }
}
