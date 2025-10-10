namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Defines the contract for aggregates in the event sourcing domain model.
/// Aggregates are consistency boundaries that encapsulate business logic and maintain invariants
/// through the application of domain events stored in event streams.
/// </summary>
public interface IAggregateRoot
{
    /// <summary>
    /// Gets or sets the unique identifier for the event stream associated with this aggregate.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies the event stream containing this aggregate's domain events.
    /// This is typically derived from the aggregate's identifier and type information.
    /// </value>
    string StreamId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for this aggregate instance.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies this specific aggregate instance within its type.
    /// This serves as the primary key for the aggregate and should remain constant throughout its lifetime.
    /// </value>
    string AggregateId { get; set; }

    /// <summary>
    /// Gets or sets the current version of the aggregate based on the number of events applied.
    /// </summary>
    /// <value>
    /// An integer representing the aggregate's version, which increments with each applied event.
    /// Used for optimistic concurrency control and tracking aggregate evolution.
    /// </value>
    int Version { get; set; }

    /// <summary>
    /// Gets or sets the sequence number of the latest event applied to this aggregate.
    /// </summary>
    /// <value>
    /// An integer representing the sequence position of the most recent event in the event stream.
    /// Used for event ordering and ensuring proper event application sequence.
    /// </value>
    int LatestEventSequence { get; set; }

    /// <summary>
    /// Gets the collection of domain events that have been generated but not yet persisted to the event store.
    /// </summary>
    /// <value>
    /// A read-only collection of <see cref="IEvent"/> instances representing state changes
    /// that occurred during the current operation but haven't been committed to storage.
    /// </value>
    IEnumerable<IEvent> UncommittedEvents { get; }

    /// <summary>
    /// Applies a collection of domain events to rebuild the aggregate's state.
    /// Used during aggregate reconstruction from the event store.
    /// </summary>
    /// <param name="events">
    /// The collection of domain events to apply to the aggregate in chronological order.
    /// </param>
    void Apply(IEnumerable<IEvent> events);

    /// <summary>
    /// Gets an array of event types that this aggregate can handle.
    /// Returns null or empty array if all event types are handled.
    /// </summary>
    /// <value>
    /// An array of <see cref="Type"/> objects representing the event types that this aggregate
    /// can process, or null/empty if the aggregate handles all event types.
    /// </value>
    Type[]? EventTypeFilter { get; }

    /// <summary>
    /// Determines whether this aggregate can handle the specified event type.
    /// </summary>
    /// <param name="eventType">The type of event to check.</param>
    /// <returns>
    /// <c>true</c> if the aggregate can handle the specified event type; otherwise, <c>false</c>.
    /// </returns>
    bool IsEventHandled(Type eventType);
}
