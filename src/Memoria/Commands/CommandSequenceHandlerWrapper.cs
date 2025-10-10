using Memoria.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Commands;

internal class CommandSequenceHandlerWrapper<TCommand, TResponse> : CommandSequenceHandlerWrapperBase<TResponse> where TCommand : ICommand<TResponse>
{
    public override async Task<Result<TResponse>> Handle(ICommand<TResponse> command, IEnumerable<Result<TResponse>> previousResults, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = GetHandler<ICommandSequenceHandler<TCommand, TResponse>>(serviceProvider);
        if (handler == null)
        {
            throw new InvalidOperationException($"Command sequence handler for {typeof(ICommand<TResponse>).Name} not found.");
        }

        return await handler.Handle((TCommand)command, previousResults, cancellationToken);
    }
}

internal abstract class CommandSequenceHandlerWrapperBase<TResponse>
{
    protected static THandler? GetHandler<THandler>(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<THandler>();
    }

    public abstract Task<Result<TResponse>> Handle(ICommand<TResponse> command, IEnumerable<Result<TResponse>> previousResults, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
