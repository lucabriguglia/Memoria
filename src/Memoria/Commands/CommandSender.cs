using System.Collections.Concurrent;
using System.Linq.Expressions;
using Memoria.Messaging;
using Memoria.Notifications;
using Memoria.Results;
using Memoria.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Commands;

/// <summary>
/// Default implementation of <see cref="ICommandSender"/> that dispatches commands to their
/// corresponding handlers using dependency injection. Provides caching for improved performance
/// when handling commands that return responses.
/// </summary>
/// <example>
/// <code>
/// // Use in controller
/// var result = await commandSender.Send(new CreateUserCommand 
/// { 
///     FirstName = "John", 
///     LastName = "Doe" 
/// });
/// </code>
/// </example>
public class CommandSender(IServiceProvider serviceProvider, IValidationService validationService, INotificationPublisher notificationPublisher, IMessagePublisher messagePublisher) : ICommandSender
{
    private static readonly ConcurrentDictionary<Type, object?> CommandHandlerWrappers = new();
    private static readonly ConcurrentDictionary<Type, Func<INotificationPublisher, INotification, CancellationToken, Task<IEnumerable<Result>>>> CompiledNotificationPublishers = new();
    private static readonly ConcurrentDictionary<Type, Func<IMessagePublisher, IMessage, CancellationToken, Task<Result>>> CompiledMessagePublishers = new();

    /// <summary>
    /// Sends a command that does not expect a response value to its corresponding handler for processing.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to send.</typeparam>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the command processing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when command is null.</exception>
    /// <exception cref="Exception">Thrown when no handler is found for the command type.</exception>
    public async Task<Result> Send<TCommand>(TCommand command, bool validateCommand = false, CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);

        if (validateCommand)
        {
            var validationResult = await validationService.Validate(command);
            if (validationResult.IsNotSuccess)
            {
                return validationResult;
            }
        }

        var commandHandler = serviceProvider.GetService<ICommandHandler<TCommand>>();
        if (commandHandler is null)
        {
            throw new InvalidOperationException($"Command handler for {typeof(TCommand).Name} not found.");
        }

        return await commandHandler.Handle(command, cancellationToken);
    }

    /// <summary>
    /// Sends a command that does not expect a response value to its corresponding handler for processing, using a custom command handler.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to send.</typeparam>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="commandHandler">A custom handler function to process the command.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the command processing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when command is null.</exception>
    public async Task<Result> Send<TCommand>(TCommand command, Func<Task<Result>> commandHandler, bool validateCommand = false, CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(command);

        if (validateCommand)
        {
            var validationResult = await validationService.Validate(command);
            if (validationResult.IsNotSuccess)
            {
                return validationResult;
            }
        }

        return await commandHandler();
    }

    /// <summary>
    /// Sends a command that expects a response value to its corresponding handler for processing.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from the command.</typeparam>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the response value on success or failure information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the command is null.</exception>
    public async Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, bool validateCommand = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (validateCommand)
        {
            var validationResult = await validationService.Validate(command);
            if (validationResult.IsNotSuccess)
            {
                return validationResult;
            }
        }

        var commandType = command.GetType();

        var commandHandler = (CommandHandlerWrapperBase<TResponse>)CommandHandlerWrappers.GetOrAdd(commandType, _ =>
            Activator.CreateInstance(typeof(CommandHandlerWrapper<,>).MakeGenericType(commandType, typeof(TResponse))))!;

        if (commandHandler is null)
        {
            throw new InvalidOperationException($"Command handler for {typeof(ICommand<TResponse>).Name} not found.");
        }

        var result = await commandHandler.Handle(command, serviceProvider, cancellationToken);

        return result;
    }

    /// <summary>
    /// Sends a command that expects a response value to its corresponding handler for processing, using a custom command handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from the command.</typeparam>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="commandHandler">A custom handler function to process the command.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result{TResponse}"/> containing the response value on success or failure information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the command is null.</exception>
    public async Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, Func<Task<Result<TResponse>>> commandHandler, bool validateCommand = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (validateCommand)
        {
            var validationResult = await validationService.Validate(command);
            if (validationResult.IsNotSuccess)
            {
                return validationResult;
            }
        }

        return await commandHandler();
    }

    /// <summary>
    /// Sends a command to its corresponding handler for processing and publishes any resulting notifications.
    /// </summary>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="SendAndPublishResponse"/> containing the result of the command processing and the results of all published notifications.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the command is null.</exception>
    /// <exception cref="Exception">Thrown when no appropriate handler is found for the command type.</exception>
    public async Task<SendAndPublishResponse> SendAndPublish(ICommand<CommandResponse> command, bool validateCommand = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (validateCommand)
        {
            var validationResult = await validationService.Validate(command);
            if (validationResult.IsNotSuccess)
            {
                return new SendAndPublishResponse(CommandResult: validationResult, NotificationResults: [], MessageResults: []);
            }
        }

        var commandType = command.GetType();

        var commandHandler = (CommandHandlerWrapperBase<CommandResponse>)CommandHandlerWrappers.GetOrAdd(commandType, _ =>
            Activator.CreateInstance(typeof(CommandHandlerWrapper<,>).MakeGenericType(commandType, typeof(CommandResponse))))!;

        if (commandHandler is null)
        {
            throw new InvalidOperationException($"Command handler for {typeof(ICommand<CommandResponse>).Name} not found.");
        }

        var commandResult = await commandHandler.Handle(command, serviceProvider, cancellationToken);

        return await ProcessCommandResult(commandResult, cancellationToken);
    }

    /// <summary>
    /// Sends a command for processing and publishes any corresponding notifications, using a custom command handler.
    /// </summary>
    /// <param name="command">The command instance to be sent for processing.</param>
    /// <param name="commandHandler">A custom handler function to process the command.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="SendAndPublishResponse"/> containing the result of the command processing and the results of all published notifications.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the command is null.</exception>
    public async Task<SendAndPublishResponse> SendAndPublish(ICommand<CommandResponse> command, Func<Task<Result<CommandResponse>>> commandHandler, bool validateCommand = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (validateCommand)
        {
            var validationResult = await validationService.Validate(command);
            if (validationResult.IsNotSuccess)
            {
                return new SendAndPublishResponse(CommandResult: validationResult, NotificationResults: [], MessageResults: []);
            }
        }

        var commandResult = await commandHandler();

        return await ProcessCommandResult(commandResult, cancellationToken);
    }

    private async Task<SendAndPublishResponse> ProcessCommandResult(Result<CommandResponse> commandResult, CancellationToken cancellationToken)
    {
        var numberOfNotificationsToPublish = commandResult.Value?.Notifications.Count() ?? 0;
        var numberOfMessagesToPublish = commandResult.Value?.Messages.Count() ?? 0;
        if (commandResult.IsNotSuccess || (numberOfNotificationsToPublish == 0 && numberOfMessagesToPublish == 0))
        {
            return new SendAndPublishResponse(commandResult, NotificationResults: [], MessageResults: []);
        }

        var notificationTasks = commandResult.Value!.Notifications
            .Select(notification =>
            {
                var notificationType = notification.GetType();
                var publishDelegate = GetOrCreateCompiledNotificationPublisher(notificationType);
                return publishDelegate(notificationPublisher, notification, cancellationToken);
            })
            .ToList();

        var messageTasks = commandResult.Value!.Messages
            .Select(message =>
            {
                var messageType = message.GetType();
                var publishDelegate = GetOrCreateCompiledMessagePublisher(messageType);
                return publishDelegate(messagePublisher, message, cancellationToken);
            })
            .ToList();

        var notificationsResults = await Task.WhenAll(notificationTasks);
        var messagesResults = await Task.WhenAll(messageTasks);

        return new SendAndPublishResponse(commandResult, notificationsResults.SelectMany(r => r).ToList(), messagesResults.Select(r => r).ToList());
    }

    /// <summary>
    /// Sends a sequence of commands for processing and returns their respective results.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from each command in the sequence.</typeparam>
    /// <param name="commandSequence">The sequence of commands to be processed.</param>
    /// <param name="validateCommands">Specifies whether the commands should be validated before processing.</param>
    /// <param name="stopProcessingOnFirstFailure">Specifies whether to stop processing commands if a failure occurs.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A collection of <see cref="Result{TResponse}"/> objects representing the outcome of each processed command.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the command sequence is null.</exception>
    public async Task<IEnumerable<Result<TResponse>>> Send<TResponse>(ICommandSequence<TResponse> commandSequence, bool validateCommands = false, bool stopProcessingOnFirstFailure = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commandSequence);

        var commandResults = new List<Result<TResponse>>();

        foreach (var command in commandSequence.Commands)
        {
            if (validateCommands)
            {
                var validationResult = await validationService.Validate(command);
                if (validationResult.IsNotSuccess)
                {
                    commandResults.Add(validationResult);

                    if (stopProcessingOnFirstFailure)
                    {
                        break;
                    }

                    continue;
                }
            }

            var commandType = command.GetType();

            var commandHandler = (CommandSequenceHandlerWrapperBase<TResponse>)CommandHandlerWrappers.GetOrAdd(commandType, _ =>
                Activator.CreateInstance(typeof(CommandSequenceHandlerWrapper<,>).MakeGenericType(commandType, typeof(TResponse))))!;

            if (commandHandler is null)
            {
                throw new InvalidOperationException($"Command sequence handler for {typeof(ICommand<TResponse>).Name} not found.");
            }

            var commandResult = await commandHandler.Handle(command, commandResults, serviceProvider, cancellationToken);
            commandResults.Add(commandResult);

            if (commandResult.IsNotSuccess && stopProcessingOnFirstFailure)
            {
                break;
            }
        }

        return commandResults;
    }

    private static Func<INotificationPublisher, INotification, CancellationToken, Task<IEnumerable<Result>>> GetOrCreateCompiledNotificationPublisher(Type notificationType)
    {
        return CompiledNotificationPublishers.GetOrAdd(notificationType, type =>
        {
            var publisherParam = Expression.Parameter(typeof(INotificationPublisher), "publisher");
            var notificationParam = Expression.Parameter(typeof(INotification), "notification");
            var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var publishMethod = typeof(INotificationPublisher).GetMethod(nameof(INotificationPublisher.Publish))!.MakeGenericMethod(type);
            var castNotification = Expression.Convert(notificationParam, type);

            var methodCall = Expression.Call(publisherParam, publishMethod, castNotification, cancellationTokenParam);

            var lambda = Expression.Lambda<Func<INotificationPublisher, INotification, CancellationToken, Task<IEnumerable<Result>>>>(methodCall, publisherParam, notificationParam, cancellationTokenParam);

            return lambda.Compile();
        });
    }

    private static Func<IMessagePublisher, IMessage, CancellationToken, Task<Result>> GetOrCreateCompiledMessagePublisher(Type messageType)
    {
        return CompiledMessagePublishers.GetOrAdd(messageType, type =>
        {
            var publisherParam = Expression.Parameter(typeof(IMessagePublisher), "publisher");
            var messageParam = Expression.Parameter(typeof(IMessage), "message");
            var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            var publishMethod = typeof(IMessagePublisher).GetMethod(nameof(IMessagePublisher.Publish))!.MakeGenericMethod(type);
            var castMessage = Expression.Convert(messageParam, type);

            var methodCall = Expression.Call(publisherParam, publishMethod, castMessage, cancellationTokenParam);

            var lambda = Expression.Lambda<Func<IMessagePublisher, IMessage, CancellationToken, Task<Result>>>(methodCall, publisherParam, messageParam, cancellationTokenParam);

            return lambda.Compile();
        });
    }
}
