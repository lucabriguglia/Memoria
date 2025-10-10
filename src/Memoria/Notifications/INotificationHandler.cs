using Memoria.Results;

namespace Memoria.Notifications;

/// <summary>
/// Defines a handler for processing notifications of the specified type.
/// </summary>
/// <typeparam name="TNotification">The type of notification to handle. Must implement <see cref="INotification"/>.</typeparam>
public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    /// <summary>
    /// Handles the specified notification and processes it accordingly.
    /// </summary>
    /// <param name="notification">The notification instance that contains the details to process.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing a <see cref="Result"/> that indicates the outcome of the handling process.</returns>
    Task<Result> Handle(TNotification notification, CancellationToken cancellationToken = default);
}
