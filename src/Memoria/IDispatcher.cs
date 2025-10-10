using Memoria.Commands;
using Memoria.Notifications;
using Memoria.Queries;
using Memoria.Results;

namespace Memoria;

/// <summary>
/// Provides a unified interface for dispatching commands, queries, and events in the CQRS pattern.
/// Acts as a centralized entry point for all CQRS operations within the application.
/// </summary>
/// <example>
/// <code>
/// var result = await dispatcher.Send(new CreateUserCommand { Email = "user@example.com" });
/// </code>
/// </example>
public interface IDispatcher
{
    /// <summary>
    /// Sends a command that does not expect a response value to its corresponding handler for processing.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to send.</typeparam>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the command processing.</returns>
    Task<Result> Send<TCommand>(TCommand command, bool validateCommand = false, CancellationToken cancellationToken = default) where TCommand : ICommand;

    /// <summary>
    /// Sends a command that does not expect a response value to its corresponding handler for processing, using a custom command handler.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to send.</typeparam>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="commandHandler">A custom handler function to process the command.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the command processing.</returns>
    Task<Result> Send<TCommand>(TCommand command, Func<Task<Result>> commandHandler, bool validateCommand = false, CancellationToken cancellationToken = default) where TCommand : ICommand;

    /// <summary>
    /// Sends a command that expects a response value to its corresponding handler for processing.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from the command.</typeparam>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the response value on success or failure information.</returns>
    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, bool validateCommand = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command that expects a response value to its corresponding handler for processing, using a custom command handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response expected from the command.</typeparam>
    /// <param name="command">The command instance to be processed.</param>
    /// <param name="commandHandler">A custom handler function to process the command.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the response value on success or failure information.</returns>
    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, Func<Task<Result<TResponse>>> commandHandler, bool validateCommand = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command for processing and publishes any corresponding notifications.
    /// </summary>
    /// <param name="command">The command instance to be sent for processing.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="SendAndPublishResponse"/> containing the result of the command processing and the notification publishing results.</returns>
    Task<SendAndPublishResponse> SendAndPublish(ICommand<CommandResponse> command, bool validateCommand = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command for processing and publishes any corresponding notifications, using a custom command handler.
    /// </summary>
    /// <param name="command">The command instance to be sent for processing.</param>
    /// <param name="commandHandler">A custom handler function to process the command.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="SendAndPublishResponse"/> containing the result of the command processing and the notification publishing results.</returns>
    Task<SendAndPublishResponse> SendAndPublish(ICommand<CommandResponse> command, Func<Task<Result<CommandResponse>>> commandHandler, bool validateCommand = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a sequence of commands to their respective handlers for processing, allowing sequential execution with optional validation and error handling.
    /// </summary>
    /// <typeparam name="TResponse">The type of response that is expected from the command sequence execution.</typeparam>
    /// <param name="commandSequence">The sequence of commands to be processed.</param>
    /// <param name="validateCommands">Indicates whether each command in the sequence should be validated before processing.</param>
    /// <param name="stopProcessingOnFirstFailure">Specifies whether to halt execution if a command in the sequence fails.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests during processing.</param>
    /// <returns>A task that represents the asynchronous operation. The result contains a collection of <see cref="Result{TResponse}"/> instances representing the outcomes of processing each command in the sequence.</returns>
    Task<IEnumerable<Result<TResponse>>> Send<TResponse>(ICommandSequence<TResponse> commandSequence, bool validateCommands = false, bool stopProcessingOnFirstFailure = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query and returns the requested data.
    /// </summary>
    /// <typeparam name="TResult">The type of data expected from the query.</typeparam>
    /// <param name="query">The query instance to be executed.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the query result on success or failure information.</returns>
    Task<Result<TResult>> Get<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a notification to all registered handlers that can process the specified notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to publish.</typeparam>
    /// <param name="notification">The notification instance to be published.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A collection of <see cref="Result"/> objects indicating the success or failure of each notification handler.</returns>
    Task<IEnumerable<Result>> Publish<TNotification>(INotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
}
