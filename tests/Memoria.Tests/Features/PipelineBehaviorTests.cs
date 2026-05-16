using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.Commands;
using Memoria.Messaging;
using Memoria.Notifications;
using Memoria.Pipeline;
using Memoria.Results;
using Memoria.Queries;
using Memoria.Caching;
using Memoria.Tests.Models.Commands;
using Memoria.Tests.Models.Commands.Handlers;
using Memoria.Tests.Models.Pipeline;
using Memoria.Tests.Models.Queries;
using Memoria.Tests.Models.Queries.Handlers;
using Memoria.Validation;
using Memoria.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Memoria.Tests.Features;

public class PipelineBehaviorTests
{
    private static CommandSender BuildSender(IServiceProvider serviceProvider) => new(
        serviceProvider,
        Substitute.For<IValidationService>(),
        Substitute.For<INotificationPublisher>(),
        Substitute.For<IMessagePublisher>());

    [Fact]
    public async Task PipelineBehavior_Should_Wrap_CommandHandler_With_Response()
    {
        var calls = new List<string>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICommandHandler<CommandWithResult, string>, CommandWithResultHandler>()
            .AddSingleton<IPipelineBehavior<CommandWithResult, string>>(_ => new RecordingBehavior<CommandWithResult, string>("A", calls))
            .BuildServiceProvider();

        var result = await BuildSender(serviceProvider).Send(new CommandWithResult("test"));

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("test");
            calls.Should().ContainInOrder("A:enter", "A:exit");
        }
    }

    [Fact]
    public async Task PipelineBehaviors_Should_Execute_In_Registration_Order_With_First_As_Outermost()
    {
        var calls = new List<string>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICommandHandler<CommandWithResult, string>, CommandWithResultHandler>()
            .AddSingleton<IPipelineBehavior<CommandWithResult, string>>(_ => new RecordingBehavior<CommandWithResult, string>("A", calls))
            .AddSingleton<IPipelineBehavior<CommandWithResult, string>>(_ => new RecordingBehavior<CommandWithResult, string>("B", calls))
            .BuildServiceProvider();

        await BuildSender(serviceProvider).Send(new CommandWithResult("test"));

        calls.Should().Equal("A:enter", "B:enter", "B:exit", "A:exit");
    }

    [Fact]
    public async Task PipelineBehavior_Can_ShortCircuit_By_Returning_Failure_Without_Calling_Next()
    {
        var handlerCalls = new List<string>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICommandHandler<CommandWithResult, string>>(_ => new RecordingHandler(handlerCalls))
            .AddSingleton<IPipelineBehavior<CommandWithResult, string>>(_ => new ShortCircuitBehavior("denied"))
            .BuildServiceProvider();

        var result = await BuildSender(serviceProvider).Send(new CommandWithResult("test"));

        using (new AssertionScope())
        {
            result.IsFailure.Should().BeTrue();
            result.Failure!.Title.Should().Be("denied");
            handlerCalls.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task PipelineBehavior_Can_Transform_The_Response_After_Calling_Next()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICommandHandler<CommandWithResult, string>, CommandWithResultHandler>()
            .AddSingleton<IPipelineBehavior<CommandWithResult, string>>(_ => new TransformBehavior(v => v.ToUpperInvariant()))
            .BuildServiceProvider();

        var result = await BuildSender(serviceProvider).Send(new CommandWithResult("hello"));

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("HELLO");
        }
    }

    [Fact]
    public async Task PipelineBehavior_Should_Wrap_CommandHandler_Without_Response()
    {
        var calls = new List<string>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICommandHandler<SimpleCommand>, SimpleCommandHandler>()
            .AddSingleton<IPipelineBehavior<SimpleCommand>>(_ => new RecordingBehavior<SimpleCommand>("A", calls))
            .BuildServiceProvider();

        var result = await BuildSender(serviceProvider).Send(new SimpleCommand("test"));

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            calls.Should().ContainInOrder("A:enter", "A:exit");
        }
    }

    [Fact]
    public async Task PipelineBehaviors_Should_Wrap_The_Custom_Func_CommandHandler_Overload()
    {
        var calls = new List<string>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IPipelineBehavior<SimpleCommand>>(_ => new RecordingBehavior<SimpleCommand>("A", calls))
            .BuildServiceProvider();

        var result = await BuildSender(serviceProvider).Send(new SimpleCommand("test"), () =>
        {
            calls.Add("custom-handler");
            return Task.FromResult(Result.Ok());
        });

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            calls.Should().Equal("A:enter", "custom-handler", "A:exit");
        }
    }

    [Fact]
    public async Task PipelineBehavior_Should_Wrap_QueryHandler()
    {
        var calls = new List<string>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IQueryHandler<GetGreeting, string>, GetGreetingHandler>()
            .AddSingleton<IPipelineBehavior<GetGreeting, string>>(_ => new RecordingBehavior<GetGreeting, string>("A", calls))
            .BuildServiceProvider();

        var processor = new QueryProcessor(serviceProvider, Substitute.For<ICachingService>());

        var result = await processor.Get(new GetGreeting("World"));

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("Hello, World");
            calls.Should().ContainInOrder("A:enter", "A:exit");
        }
    }

    [Fact]
    public void AddMemoriaBehavior_Should_Throw_When_Type_Does_Not_Implement_IPipelineBehavior()
    {
        var services = new ServiceCollection();

        Action act = () => services.AddMemoriaBehavior(typeof(string));

        act.Should().Throw<ArgumentException>().WithMessage("*IPipelineBehavior*");
    }

    [Fact]
    public void AddMemoriaBehavior_Should_Throw_When_Services_Is_Null()
    {
        IServiceCollection? services = null;

        Action act = () => services!.AddMemoriaBehavior(typeof(RecordingBehavior<CommandWithResult, string>));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddMemoriaBehavior_Should_Throw_When_BehaviorType_Is_Null()
    {
        var services = new ServiceCollection();

        Action act = () => services.AddMemoriaBehavior((Type)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AddMemoriaBehavior_Typed_Overload_Should_Register_Closed_Behavior()
    {
        var calls = new List<string>();
        var services = new ServiceCollection()
            .AddSingleton<ICommandHandler<CommandWithResult, string>, CommandWithResultHandler>()
            .AddSingleton(calls);
        services.AddMemoriaBehavior<ClosedRecordingBehavior>();

        var result = await BuildSender(services.BuildServiceProvider()).Send(new CommandWithResult("test"));

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            calls.Should().ContainInOrder("closed:enter", "closed:exit");
        }
    }

    [Fact]
    public async Task AddMemoriaBehavior_Should_Register_Open_Generic_Behavior()
    {
        var calls = new List<string>();
        var services = new ServiceCollection()
            .AddSingleton<ICommandHandler<CommandWithResult, string>, CommandWithResultHandler>()
            .AddSingleton<ICommandHandler<SimpleCommand>, SimpleCommandHandler>()
            .AddSingleton(calls);
        services.AddMemoriaBehavior(typeof(OpenGenericRecordingBehavior<,>));

        var sender = BuildSender(services.BuildServiceProvider());
        await sender.Send(new CommandWithResult("test"));

        calls.Should().Contain("open:enter").And.Contain("open:exit");
    }

    private sealed class ClosedRecordingBehavior(List<string> calls) : IPipelineBehavior<CommandWithResult, string>
    {
        public async Task<Result<string>> Handle(CommandWithResult request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            calls.Add("closed:enter");
            var result = await next();
            calls.Add("closed:exit");
            return result;
        }
    }

    private sealed class OpenGenericRecordingBehavior<TRequest, TResponse>(List<string> calls) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            calls.Add("open:enter");
            var result = await next();
            calls.Add("open:exit");
            return result;
        }
    }

    [Fact]
    public async Task PipelineBehavior_With_No_Behaviors_Registered_Should_Invoke_Handler_Exactly_Once()
    {
        var handlerCalls = new List<string>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICommandHandler<CommandWithResult, string>>(_ => new RecordingHandler(handlerCalls))
            .BuildServiceProvider();

        var result = await BuildSender(serviceProvider).Send(new CommandWithResult("test"));

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            handlerCalls.Should().Equal("handler");
        }
    }

    [Fact]
    public async Task PipelineBehavior_Should_Receive_The_Same_CancellationToken_The_Dispatcher_Was_Called_With()
    {
        using var cts = new CancellationTokenSource();
        var observed = new List<CancellationToken>();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<ICommandHandler<CommandWithResult, string>, CommandWithResultHandler>()
            .AddSingleton<IPipelineBehavior<CommandWithResult, string>>(_ => new CancellationCapturingBehavior(observed))
            .BuildServiceProvider();

        await BuildSender(serviceProvider).Send(new CommandWithResult("test"), cancellationToken: cts.Token);

        observed.Should().Equal(cts.Token);
    }

    private sealed class CancellationCapturingBehavior(List<CancellationToken> observed) : IPipelineBehavior<CommandWithResult, string>
    {
        public Task<Result<string>> Handle(CommandWithResult request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            observed.Add(cancellationToken);
            return next();
        }
    }

    private sealed class RecordingHandler(List<string> calls) : ICommandHandler<CommandWithResult, string>
    {
        public Task<Result<string>> Handle(CommandWithResult command, CancellationToken cancellationToken = default)
        {
            calls.Add("handler");
            return Task.FromResult<Result<string>>(command.Name);
        }
    }

    private sealed class ShortCircuitBehavior(string failureTitle) : IPipelineBehavior<CommandWithResult, string>
    {
        public Task<Result<string>> Handle(CommandWithResult request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result<string>.Fail(title: failureTitle));
        }
    }

    private sealed class TransformBehavior(Func<string, string> transform) : IPipelineBehavior<CommandWithResult, string>
    {
        public async Task<Result<string>> Handle(CommandWithResult request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            var inner = await next();
            return inner.IsSuccess ? transform(inner.Value!) : inner;
        }
    }
}
