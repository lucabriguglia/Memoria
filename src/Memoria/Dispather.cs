using OpenCqrs.Commands;
using OpenCqrs.Notifications;
using OpenCqrs.Queries;
using OpenCqrs.Results;

namespace OpenCqrs;

/// <summary>
/// Provides a unified implementation for dispatching commands, queries, and events in the CQRS pattern.
/// Delegates operations to specialized services for command sending, query processing, and event publishing.
/// </summary>
/// <example>
/// <code>
/// var dispatcher = new Dispatcher(commandSender, queryProcessor, publisher);
/// var result = await dispatcher.Send(new CreateUserCommand { Email = "user@example.com" });
/// </code>
/// </example>
public class Dispatcher(ICommandSender commandSender, IQueryProcessor queryProcessor, INotificationPublisher notificationPublisher) : IDispatcher
{
    /// <summary>
    /// Sends a command that does not expect a response value to its corresponding handler for processing.
    /// </summary>
    /// <typeparam name="TRequest">The type of command to send.</typeparam>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the command processing.</returns>
    public async Task<Result> Send<TRequest>(TRequest command, bool validateCommand = false, CancellationToken cancellationToken = default) where TRequest : ICommand
    {
        return await commandSender.Send(command, validateCommand, cancellationToken);
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
    public async Task<Result> Send<TCommand>(TCommand command, Func<Task<Result>> commandHandler, bool validateCommand = false,
        CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        return await commandSender.Send(command, commandHandler, validateCommand, cancellationToken);
    }

    /// <summary>
    /// Sends a command that expects a response value to its corresponding handler for processing.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from the command.</typeparam>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the response value on success or failure information.</returns>
    public async Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, bool validateCommand = false, CancellationToken cancellationToken = default)
    {
        return await commandSender.Send(command, validateCommand, cancellationToken);
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
    public async Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, Func<Task<Result<TResponse>>> commandHandler, bool validateCommand = false,
        CancellationToken cancellationToken = default)
    {
        return await commandSender.Send(command, commandHandler, validateCommand, cancellationToken);
    }

    /// <summary>
    /// Sends a command for processing, publishes any associated notifications,
    /// and returns the combined results.
    /// </summary>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="SendAndPublishResponse"/> containing the result of the command processing
    /// and the results of the published notifications.</returns>
    public async Task<SendAndPublishResponse> SendAndPublish(ICommand<CommandResponse> command, bool validateCommand = false, CancellationToken cancellationToken = default)
    {
        return await commandSender.SendAndPublish(command, validateCommand, cancellationToken);
    }

    /// <summary>
    /// Sends a command for processing and publishes any corresponding notifications, using a custom command handler.
    /// </summary>
    /// <param name="command">The command instance to be sent for processing.</param>
    /// <param name="commandHandler">A custom handler function to process the command.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="SendAndPublishResponse"/> containing the result of the command processing and the notification publishing results.</returns>
    public async Task<SendAndPublishResponse> SendAndPublish(ICommand<CommandResponse> command, Func<Task<Result<CommandResponse>>> commandHandler, bool validateCommand = false, CancellationToken cancellationToken = default)
    {
        return await commandSender.SendAndPublish(command, commandHandler, validateCommand, cancellationToken);
    }

    /// <summary>
    /// Sends a sequence of commands to their corresponding handlers for processing, capturing results for each command.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response returned by each command in the sequence.</typeparam>
    /// <param name="commandSequence">The sequence of commands to be processed.</param>
    /// <param name="validateCommands">Indicates whether the commands should be validated before processing.</param>
    /// <param name="stopProcessingOnFirstFailure">Indicates whether to halt further command processing if a failure occurs.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing a collection of results for each command in the sequence.</returns>
    public async Task<IEnumerable<Result<TResponse>>> Send<TResponse>(ICommandSequence<TResponse> commandSequence, bool validateCommands = false, bool stopProcessingOnFirstFailure = false, CancellationToken cancellationToken = default)
    {
        return await commandSender.Send(commandSequence, validateCommands, stopProcessingOnFirstFailure, cancellationToken);
    }

    /// <summary>
    /// Executes a query and returns the commanded data.
    /// </summary>
    /// <typeparam name="TResult">The type of data expected from the query.</typeparam>
    /// <param name="query">The query instance to be executed.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the query result on success or failure information.</returns>
    public async Task<Result<TResult>> Get<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        return await queryProcessor.Get(query, cancellationToken);
    }

    /// <summary>
    /// Publishes an notification to all registered handlers that can process the specified notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to publish.</typeparam>
    /// <param name="notification">The notification instance to be published.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A collection of <see cref="Result"/> objects indicating the success or failure of each notification handler.</returns>
    public async Task<IEnumerable<Result>> Publish<TNotification>(INotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        return await notificationPublisher.Publish(notification, cancellationToken);
    }
}
