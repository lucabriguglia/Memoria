using FluentValidation;
using Memoria;
using Memoria.Commands;
using Memoria.Messaging;
using Memoria.Notifications;
using Memoria.Queries;
using Memoria.Validation;
using Memoria.Validation.FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenCqrs.Validation.FluentValidation.Tests.Models.Commands;
using OpenCqrs.Validation.FluentValidation.Tests.Models.Commands.Handlers;
using OpenCqrs.Validation.FluentValidation.Tests.Models.Commands.Validators;

namespace OpenCqrs.Validation.FluentValidation.Tests;

public abstract class TestBase
{
    protected readonly IDispatcher Dispatcher;

    protected TestBase()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IValidationService, ValidationService>()
            .AddSingleton<IValidationProvider, FluentValidationProvider>()
            .AddSingleton<ICommandHandler<DoSomething>, DoSomethingHandler>()
            .AddSingleton<ICommandHandler<DoSomethingWithResponse, string>, DoSomethingWithResponseHandler>()
            .AddSingleton<ICommandHandler<DoSomethingWithCommandResponse, CommandResponse>, DoSomethingWithCommandResponseHandler>()
            .AddSingleton<ICommandSequenceHandler<FirstCommandInSequence, string>, FirstCommandInSequenceHandler>()
            .AddSingleton<ICommandSequenceHandler<SecondCommandInSequence, string>, SecondCommandInSequenceHandler>()
            .AddSingleton<IValidator<DoSomething>, DoSomethingValidator>()
            .AddSingleton<IValidator<DoSomethingWithResponse>, DoSomethingWithResponseValidator>()
            .AddSingleton<IValidator<DoSomethingWithCommandResponse>, DoSomethingWithCommandResponseValidator>()
            .AddSingleton<IValidator<FirstCommandInSequence>, FirstCommandInSequenceValidator>()
            .AddSingleton<IValidator<SecondCommandInSequence>, SecondCommandInSequenceValidator>()
            .BuildServiceProvider();

        var fluentValidationProvider = new FluentValidationProvider(serviceProvider);
        var validationService = new ValidationService(fluentValidationProvider);
        var commandSender = new CommandSender(serviceProvider, validationService, Substitute.For<INotificationPublisher>(), Substitute.For<IMessagePublisher>());

        Dispatcher = new Dispatcher(commandSender, Substitute.For<IQueryProcessor>(), Substitute.For<INotificationPublisher>());
    }
}
