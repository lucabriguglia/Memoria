using Memoria.Commands;
using Memoria.Results;

namespace Memoria.Pipeline;

/// <summary>
/// Represents the continuation in a pipeline behavior chain for requests that produce no
/// response value. Invoking it runs the next behavior, or the terminal handler if this is
/// the innermost behavior.
/// </summary>
/// <returns>The <see cref="Result"/> produced by the inner step.</returns>
public delegate Task<Result> RequestHandlerDelegate();

/// <summary>
/// Represents the continuation in a pipeline behavior chain. Invoking it runs the next
/// behavior, or the terminal handler if this is the innermost behavior.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by the inner step.</typeparam>
/// <returns>The <see cref="Result{TResponse}"/> produced by the inner step.</returns>
public delegate Task<Result<TResponse>> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Defines a behavior that wraps the handling of a command that produces no response,
/// enabling cross-cutting concerns such as logging, timing, authorization, or
/// transactional boundaries.
/// </summary>
/// <typeparam name="TRequest">The command type the behavior applies to.</typeparam>
public interface IPipelineBehavior<in TRequest>
    where TRequest : ICommand
{
    /// <summary>
    /// Handles the request, optionally invoking the next step in the pipeline. Returning
    /// without calling <paramref name="next"/> short-circuits the chain.
    /// </summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="next">The continuation that invokes the next behavior or terminal handler.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result"/> produced either by the chain or by this behavior directly.</returns>
    Task<Result> Handle(
        TRequest request,
        RequestHandlerDelegate next,
        CancellationToken cancellationToken);
}

/// <summary>
/// Defines a behavior that wraps the handling of a request, enabling cross-cutting
/// concerns such as logging, timing, authorization, or transactional boundaries.
/// </summary>
/// <typeparam name="TRequest">The request type the behavior applies to.</typeparam>
/// <typeparam name="TResponse">The response type produced by the request.</typeparam>
/// <example>
/// <code>
/// public class LoggingBehavior&lt;TRequest, TResponse&gt; : IPipelineBehavior&lt;TRequest, TResponse&gt;
///     where TRequest : notnull
/// {
///     public async Task&lt;Result&lt;TResponse&gt;&gt; Handle(
///         TRequest request,
///         RequestHandlerDelegate&lt;TResponse&gt; next,
///         CancellationToken cancellationToken)
///     {
///         // pre-processing
///         var result = await next();
///         // post-processing
///         return result;
///     }
/// }
/// </code>
/// </example>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Handles the request, optionally invoking the next step in the pipeline. Returning
    /// without calling <paramref name="next"/> short-circuits the chain.
    /// </summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="next">The continuation that invokes the next behavior or terminal handler.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Result{TResponse}"/> produced either by the chain or by this behavior directly.</returns>
    Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
