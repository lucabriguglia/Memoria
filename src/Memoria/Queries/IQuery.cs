namespace OpenCqrs.Queries;

/// <summary>
/// Defines a query in the CQRS pattern that represents a request for data without modifying system state.
/// Queries are processed by query handlers that retrieve and return the requested information.
/// </summary>
/// <typeparam name="TResult">The type of data expected to be returned from the query.</typeparam>
/// <example>
/// <code>
/// public record GetUserQuery : IQuery&lt;UserDto&gt;
/// {
///     public Guid UserId { get; init; }
/// }
/// </code>
/// </example>
public interface IQuery<TResult>;
