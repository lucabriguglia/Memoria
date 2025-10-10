using Memoria.Commands;
using Memoria.Messaging;
using Memoria.Notifications;
using Memoria.Queries;
using Memoria.Tests.Models.Commands;
using Memoria.Tests.Models.Commands.Handlers;
using Memoria.Tests.Models.Notifications;
using Memoria.Tests.Models.Notifications.Handlers;
using Memoria.Validation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Memoria.Tests;

public abstract class TestBase
{
    protected readonly IDispatcher Dispatcher;

    protected TestBase()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICommandHandler<DoSomething, CommandResponse>, DoSomethingHandler>()
            .AddSingleton<ICommandHandler<DoMore, CommandResponse>, DoMoreHandler>()
            .AddSingleton<ICommandSequenceHandler<FirstCommandInSequence, string>, FirstCommandInSequenceHandler>()
            .AddSingleton<ICommandSequenceHandler<SecondCommandInSequence, string>, SecondCommandInSequenceHandler>()
            .AddSingleton<ICommandSequenceHandler<ThirdCommandInSequence, string>, ThirdCommandInSequenceHandler>()
            .AddSingleton<INotificationHandler<SomethingHappened>, SomethingHappenedHandlerOne>()
            .AddSingleton<INotificationHandler<SomethingHappened>, SomethingHappenedHandlerTwo>()
            .AddSingleton<INotificationHandler<SomethingElseHappened>, SomethingElseHappenedHandler>()
            .BuildServiceProvider();

        var publisher = new NotificationPublisher(serviceProvider);
        var commandSender = new CommandSender(serviceProvider, Substitute.For<IValidationService>(), publisher, Substitute.For<IMessagePublisher>());

        Dispatcher = new Dispatcher(commandSender, Substitute.For<IQueryProcessor>(), publisher);
    }
}
