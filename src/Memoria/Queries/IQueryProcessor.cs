using OpenCqrs.Results;

namespace OpenCqrs.Queries;

/// <summary>
/// Defines a service for processing queries by dispatching them to their corresponding handlers.
/// Provides a centralized entry point for executing query operations in the CQRS pattern.
/// </summary>
/// <example>
/// <code>
/// var result = await queryProcessor.Get(new GetUserQuery { UserId = userId });
/// </code>
/// </example>
public interface IQueryProcessor
{
    /// <summary>
    /// Executes a query and returns the requested data.
    /// </summary>
    /// <typeparam name="TResult">The type of data expected from the query.</typeparam>
    /// <param name="query">The query instance to be executed.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result{T}"/> containing the query result on success or failure information.</returns>
    Task<Result<TResult>> Get<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
