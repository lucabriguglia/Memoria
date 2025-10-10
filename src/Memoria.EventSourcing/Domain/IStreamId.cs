namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Defines a contract for stream identifiers in the event sourcing infrastructure.
/// Stream identifiers uniquely identify event streams that contain the history of changes
/// for specific aggregates or entities in the system.
/// </summary>
/// <example>
/// <code>
/// // Simple GUID-based stream ID
/// public class OrderStreamId : IStreamId
/// {
///     public string Id { get; }
///     
///     public OrderStreamId(Guid orderId)
///     {
///         Id = $"order-{orderId.ToString()}";
///     }
/// }
///
/// // Usage in event sourcing
/// var streamId = new OrderStreamId(order.Id);
/// var events = await eventStore.GetEventsAsync(streamId.Id);
/// </code>
/// </example>
public interface IStreamId
{
    /// <summary>
    /// Gets the unique string identifier for the event stream.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies an event stream within the event store.
    /// This value must be unique across all streams and suitable for persistence.
    /// </value>
    string Id { get; }
}
