namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Defines the contract for aggregates in the event sourcing domain model.
/// Aggregates are consistency boundaries that encapsulate business logic and maintain invariants
/// through the application of domain events stored in event streams. In addition to the shared
/// event-sourced model members, an aggregate is a write model that stages new domain events as
/// uncommitted events until they are persisted.
/// </summary>
public interface IAggregateRoot : IStreamedModel
{
    /// <summary>
    /// Gets or sets the unique identifier for this aggregate instance.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies this specific aggregate within its type.
    /// This serves as the primary key for the aggregate and should remain constant throughout its lifetime.
    /// </value>
    string AggregateId { get; set; }

    /// <summary>
    /// Gets the collection of domain events that have been generated but not yet persisted to the event store.
    /// </summary>
    /// <value>
    /// A read-only collection of <see cref="IEvent"/> instances representing state changes
    /// that occurred during the current operation but haven't been committed to storage.
    /// </value>
    IEnumerable<IEvent> UncommittedEvents { get; }
}
