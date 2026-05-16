using Memoria.Pipeline;
using Memoria.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Queries;

internal class QueryHandlerWrapper<TQuery, TResult> : QueryHandlerWrapperBase<TResult> where TQuery : IQuery<TResult>
{
    public override async Task<Result<TResult>> Handle(IQuery<TResult> query, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = GetHandler<IQueryHandler<TQuery, TResult>>(serviceProvider);

        if (handler == null)
        {
            throw new InvalidOperationException($"Query handler for {typeof(IQuery<TResult>).Name} not found.");
        }

        var typedQuery = (TQuery)query;
        RequestHandlerDelegate<TResult> pipeline = () => handler.Handle(typedQuery, cancellationToken);

        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TQuery, TResult>>().Reverse().ToArray();
        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            var current = behavior;
            pipeline = () => current.Handle(typedQuery, next, cancellationToken);
        }

        return await pipeline();
    }
}

internal abstract class QueryHandlerWrapperBase<TResult>
{
    protected static THandler? GetHandler<THandler>(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<THandler>();
    }

    public abstract Task<Result<TResult>> Handle(IQuery<TResult> query, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
