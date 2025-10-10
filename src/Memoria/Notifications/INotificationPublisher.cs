using Memoria.Results;

namespace Memoria.Notifications;

/// <summary>
/// Defines the contract for a service that publishes notifications to their respective handlers.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes a notification to the associated handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification to publish.</typeparam>
    /// <param name="notification">The notification to be published.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, which contains a collection of results indicating the outcome of the notification handling.</returns>
    Task<IEnumerable<Result>> Publish<TNotification>(TNotification notification,
        CancellationToken cancellationToken = default) where TNotification : INotification;
}
