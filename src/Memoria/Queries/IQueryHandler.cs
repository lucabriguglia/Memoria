using Memoria.Results;

namespace Memoria.Queries;

/// <summary>
/// Defines a handler for processing queries and returning the requested data.
/// Query handlers contain the logic for retrieving information without modifying system state.
/// </summary>
/// <typeparam name="TQuery">The type of query this handler processes.</typeparam>
/// <typeparam name="TResult">The type of data this handler returns.</typeparam>
/// <example>
/// <code>
/// public class GetUserQueryHandler : IQueryHandler&lt;GetUserQuery, UserDto&gt;
/// {
///     public async Task&lt;Result&lt;UserDto&gt;&gt; Handle(GetUserQuery query, CancellationToken cancellationToken)
///     {
///         // Retrieve and return user data
///         return new UserDto { Id = query.UserId };
///     }
/// }
/// </code>
/// </example>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Handles the specified query and returns the requested data.
    /// </summary>
    /// <param name="query">The query to process.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the requested data on success or failure information.</returns>
    Task<Result<TResult>> Handle(TQuery query, CancellationToken cancellationToken = default);
}
