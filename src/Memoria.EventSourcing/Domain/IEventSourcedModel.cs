namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Defines the members shared by every event-sourced model, whether it is a write model
/// (an <see cref="IAggregateRoot"/>) or a read model (an <see cref="IProjection"/>).
/// An event-sourced model carries stream and aggregate identity, tracks its version, and
/// rebuilds its state by applying domain events from an event stream.
/// </summary>
public interface IEventSourcedModel
{
    /// <summary>
    /// Gets or sets the unique identifier for the event stream associated with this model.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies the event stream containing this model's domain events.
    /// This is typically derived from the model's identifier and type information.
    /// </value>
    string StreamId { get; set; }

    /// <summary>
    /// Gets or sets the current version of the model based on the number of events applied.
    /// </summary>
    /// <value>
    /// An integer representing the model's version, which increments with each applied event.
    /// Used for optimistic concurrency control and tracking model evolution.
    /// </value>
    int Version { get; set; }

    /// <summary>
    /// Gets or sets the sequence number of the latest event applied to this model.
    /// </summary>
    /// <value>
    /// An integer representing the sequence position of the most recent event in the event stream.
    /// Used for event ordering and ensuring proper event application sequence.
    /// </value>
    int LatestEventSequence { get; set; }

    /// <summary>
    /// Applies a collection of domain events to rebuild the model's state.
    /// Used during reconstruction from the event store.
    /// </summary>
    /// <param name="events">
    /// The collection of domain events to apply to the model in chronological order.
    /// </param>
    void Apply(IEnumerable<IEvent> events);

    /// <summary>
    /// Gets an array of event types that this model can handle.
    /// Returns null or empty array if all event types are handled.
    /// </summary>
    /// <value>
    /// An array of <see cref="Type"/> objects representing the event types that this model
    /// can process, or null/empty if the model handles all event types.
    /// </value>
    Type[]? EventTypeFilter { get; }

    /// <summary>
    /// Determines whether this model can handle the specified event type.
    /// </summary>
    /// <param name="eventType">The type of event to check.</param>
    /// <returns>
    /// <c>true</c> if the model can handle the specified event type; otherwise, <c>false</c>.
    /// </returns>
    bool IsEventHandled(Type eventType);
}
