namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Defines the members shared by every event-sourced model, whether it is a write model
/// (an <see cref="IAggregateRoot"/>) or a read model (an <see cref="IProjection"/>).
/// An event-sourced model tracks its version and rebuilds its state by applying domain events.
/// It carries no identity of its own: what identifies a model depends on its consistency model,
/// so stream identity lives on <see cref="IStreamedModel"/>.
/// </summary>
public interface IEventSourcedModel
{
    /// <summary>
    /// Gets or sets the current version of the model based on the number of events applied.
    /// </summary>
    /// <value>
    /// An integer representing the model's version, which increments with each applied event.
    /// Used for optimistic concurrency control and tracking model evolution.
    /// </value>
    int Version { get; set; }

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
