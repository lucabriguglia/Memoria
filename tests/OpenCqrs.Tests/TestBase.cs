using Memoria;
using Memoria.Commands;
using Memoria.Messaging;
using Memoria.Notifications;
using Memoria.Queries;
using Memoria.Validation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenCqrs.Tests.Models.Commands;
using OpenCqrs.Tests.Models.Commands.Handlers;
using OpenCqrs.Tests.Models.Notifications;
using OpenCqrs.Tests.Models.Notifications.Handlers;

namespace OpenCqrs.Tests;

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
