using Microsoft.Extensions.DependencyInjection;
using OpenCqrs.Results;

namespace OpenCqrs.Queries;

internal class QueryHandlerWrapper<TQuery, TResult> : QueryHandlerWrapperBase<TResult> where TQuery : IQuery<TResult>
{
    public override async Task<Result<TResult>> Handle(IQuery<TResult> query, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = GetHandler<IQueryHandler<TQuery, TResult>>(serviceProvider);

        if (handler == null)
        {
            throw new InvalidOperationException($"Query handler for {typeof(IQuery<TResult>).Name} not found.");
        }

        return await handler.Handle((TQuery)query, cancellationToken);
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
