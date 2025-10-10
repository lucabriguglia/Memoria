using Microsoft.Extensions.DependencyInjection;
using OpenCqrs.Results;

namespace OpenCqrs.Notifications;

/// <summary>
/// Handles the publishing of notifications to all registered notification handlers.
/// </summary>
public class NotificationPublisher(IServiceProvider serviceProvider) : INotificationPublisher
{
    /// <summary>
    /// Publishes a notification to all the subscribed handlers of the specified notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification being published.</typeparam>
    /// <param name="notification">The notification instance to be published.</param>
    /// <param name="cancellationToken">A token to cancel the publish operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is a collection of results returned by the handlers.</returns>
    public async Task<IEnumerable<Result>> Publish<TNotification>(TNotification notification,
        CancellationToken cancellationToken = default) where TNotification : INotification
    {
        var handlers = serviceProvider.GetServices<INotificationHandler<TNotification>>();

        var notificationHandlers = handlers as INotificationHandler<TNotification>[] ?? handlers.ToArray();
        if (notificationHandlers.Length == 0)
        {
            return [];
        }

        var tasks = notificationHandlers.Select(handler => handler.Handle(notification, cancellationToken)).ToList();

        return await Task.WhenAll(tasks);
    }
}
