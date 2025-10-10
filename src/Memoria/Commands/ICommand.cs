namespace OpenCqrs.Commands;

/// <summary>
/// Defines a command in the CQRS pattern that represents an intent to change system state
/// without expecting a specific return value. Commands are processed by command handlers
/// that execute the requested business operations.
/// </summary>
/// <example>
/// <code>
/// public record CreateUserCommand : ICommand
/// {
///     public string FirstName { get; init; } = string.Empty;
///     public string LastName { get; init; } = string.Empty;
///     public string Email { get; init; } = string.Empty;
/// }
/// </code>
/// </example>
public interface ICommand;

/// <summary>
/// Defines a command in the CQRS pattern that represents an intent to change system state
/// and expects a specific response value upon successful execution. The response contains
/// meaningful business information resulting from the command processing.
/// </summary>
/// <typeparam name="TResponse">
/// The type of response expected from a successful command execution. This should contain
/// relevant information needed by the caller, such as generated identifiers or computed values.
/// </typeparam>
/// <example>
/// <code>
/// public record CreateOrderCommand : ICommand&lt;Guid&gt;
/// {
///     public Guid CustomerId { get; init; }
///     public List&lt;OrderItem&gt; Items { get; init; } = new();
///     public string Notes { get; init; } = string.Empty;
/// }
/// </code>
/// </example>
public interface ICommand<TResponse>;
