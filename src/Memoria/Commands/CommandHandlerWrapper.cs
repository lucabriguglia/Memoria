using Microsoft.Extensions.DependencyInjection;
using OpenCqrs.Results;

namespace OpenCqrs.Commands;

internal class CommandHandlerWrapper<TCommand, TResponse> : CommandHandlerWrapperBase<TResponse> where TCommand : ICommand<TResponse>
{
    public override async Task<Result<TResponse>> Handle(ICommand<TResponse> command, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = GetHandler<ICommandHandler<TCommand, TResponse>>(serviceProvider);
        if (handler == null)
        {
            throw new InvalidOperationException($"Command handler for {typeof(ICommand<TResponse>).Name} not found.");
        }

        return await handler.Handle((TCommand)command, cancellationToken);
    }
}

internal abstract class CommandHandlerWrapperBase<TResponse>
{
    protected static THandler? GetHandler<THandler>(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<THandler>();
    }

    public abstract Task<Result<TResponse>> Handle(ICommand<TResponse> command, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
