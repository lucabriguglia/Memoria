using Memoria.Notifications;
using Memoria.Results;

namespace Memoria.Tests.Models.Notifications.Handlers;

public class SomethingHappenedHandlerTwo : INotificationHandler<SomethingHappened>
{
    public Task<Result> Handle(SomethingHappened notification, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Handler Two processed: {notification.Name}");

        return Task.FromResult(Result.Ok());
    }
}
