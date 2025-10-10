using Memoria.Results;

namespace Memoria.Commands;

/// <summary>
/// Defines a handler for processing commands that do not return a response value.
/// Command handlers contain the business logic for executing the requested operations.
/// </summary>
/// <typeparam name="TCommand">The type of command this handler processes.</typeparam>
/// <example>
/// <code>
/// public class CreateUserCommandHandler : ICommandHandler&lt;CreateUserCommand&gt;
/// {
///     public async Task&lt;Result&gt; Handle(CreateUserCommand command, CancellationToken cancellationToken)
///     {
///         // Process the command
///         return Result.Ok();
///     }
/// }
/// </code>
/// </example>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    /// <summary>
    /// Handles the specified command and executes the associated business logic.
    /// </summary>
    /// <param name="command">The command to process.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure of the command processing.</returns>
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a handler for processing commands that return a response value.
/// Command handlers contain the business logic for executing the requested operations
/// and producing the expected response.
/// </summary>
/// <typeparam name="TCommand">The type of command this handler processes.</typeparam>
/// <typeparam name="TResponse">The type of response this handler returns.</typeparam>
/// <example>
/// <code>
/// public class CreateOrderCommandHandler : ICommandHandler&lt;CreateOrderCommand, Guid&gt;
/// {
///     public async Task&lt;Result&lt;Guid&gt;&gt; Handle(CreateOrderCommand command, CancellationToken cancellationToken)
///     {
///         // Process the command and return the generated ID
///         var orderId = Guid.NewGuid();
///         return orderId;
///     }
/// }
/// </code>
/// </example>
public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles the specified command, executes the associated business logic, and returns a response.
    /// </summary>
    /// <param name="command">The command to process.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the response value on success or failure information.</returns>
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken = default);
}
