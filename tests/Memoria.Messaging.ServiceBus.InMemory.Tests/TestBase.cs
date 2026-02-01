using Memoria.Commands;
using Memoria.Messaging.ServiceBus.InMemory;
using Memoria.Messaging.ServiceBus.InMemory.Tests.Models.Commands;
using Memoria.Messaging.ServiceBus.InMemory.Tests.Models.Commands.Handlers;
using Memoria.Notifications;
using Memoria.Queries;
using Memoria.Validation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Memoria.Messaging.ServiceBus.InMemory.Tests;

public abstract class TestBase
{
    protected readonly InMemoryServiceBusStorage Storage;
    protected readonly IDispatcher Dispatcher;

    protected TestBase()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICommandHandler<DoSomething, CommandResponse>, DoSomethingHandler>()
            .BuildServiceProvider();

        Storage = new InMemoryServiceBusStorage();
        var messagingProvider = new InMemoryServiceBusMessagingProvider(Storage);
        var messagePublisher = new MessagePublisher(messagingProvider);
        var commandSender = new CommandSender(serviceProvider, Substitute.For<IValidationService>(), Substitute.For<INotificationPublisher>(), messagePublisher);

        Dispatcher = new Dispatcher(commandSender, Substitute.For<IQueryProcessor>(), Substitute.For<INotificationPublisher>());
    }
}
