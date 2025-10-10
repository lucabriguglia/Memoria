using Memoria.Results;

namespace Memoria.Commands;

/// <summary>
/// Defines a service for dispatching commands to their corresponding handlers in the CQRS pattern.
/// Provides methods for sending both commands without responses and commands that return values.
/// </summary>
/// <example>
/// <code>
/// var result = await commandSender.Send(new CreateUserCommand 
/// { 
///     FirstName = "John", 
///     LastName = "Doe" 
/// });
/// </code>
/// </example>
public interface ICommandSender
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
    /// <returns>A <see cref="Result{TResponse}"/> containing the response value on success or failure information.</returns>
    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, Func<Task<Result<TResponse>>> commandHandler, bool validateCommand = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command that expects a <see cref="CommandResponse"/> as a result to its handler for processing
    /// and subsequently publishes related notifications associated with the command.
    /// </summary>
    /// <param name="command">The command instance to be processed and published.</param>
    /// <param name="validateCommand"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if necessary.</param>
    /// <returns>A <see cref="SendAndPublishResponse"/> containing the command processing result
    /// and a collection of results from the published notifications.</returns>
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
    /// Sends a sequence of commands to their corresponding handlers for processing and retrieves their results.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response expected from processing the command sequence.</typeparam>
    /// <param name="commandSequence">The sequence of commands to process.</param>
    /// <param name="validateCommands">Indicates whether each command in the sequence should be validated before processing.</param>
    /// <param name="stopProcessingOnFirstFailure">Indicates whether processing should stop if a command in the sequence fails.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a collection of <see cref="Result{TResponse}"/> instances indicating the outcomes of processing each command in the sequence.</returns>
    Task<IEnumerable<Result<TResponse>>> Send<TResponse>(ICommandSequence<TResponse> commandSequence, bool validateCommands = false, bool stopProcessingOnFirstFailure = false, CancellationToken cancellationToken = default);
}
