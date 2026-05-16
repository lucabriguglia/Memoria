using Memoria.Pipeline;
using Memoria.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Commands;

internal class CommandHandlerWrapper<TCommand, TResponse> : CommandHandlerWrapperBase<TResponse> where TCommand : ICommand<TResponse>
{
    public override async Task<Result<TResponse>> Handle(ICommand<TResponse> command, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var handler = GetHandler<ICommandHandler<TCommand, TResponse>>(serviceProvider);
        if (handler == null)
        {
            throw new InvalidOperationException($"Command handler for {typeof(ICommand<TResponse>).Name} not found.");
        }

        var typedCommand = (TCommand)command;
        RequestHandlerDelegate<TResponse> pipeline = () => handler.Handle(typedCommand, cancellationToken);

        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TCommand, TResponse>>().Reverse().ToArray();
        foreach (var behavior in behaviors)
        {
            var next = pipeline;
            var current = behavior;
            pipeline = () => current.Handle(typedCommand, next, cancellationToken);
        }

        return await pipeline();
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
