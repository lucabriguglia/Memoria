using Newtonsoft.Json;

namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Abstract base class shared by every event-sourced model. It owns the concerns common to any
/// model rebuilt by applying domain events: version tracking, the event type filter, and the
/// fold itself. It deliberately owns no identity, because what identifies a model differs by
/// consistency model — stream identity lives on <see cref="StreamedModel"/>, and write-model-only
/// concerns such as staging uncommitted events live on <see cref="AggregateRoot"/>.
/// </summary>
public abstract class EventSourcedModel : IEventSourcedModel
{
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    [JsonIgnore]
    public int Version { get; set; }

    /// <summary>
    /// Applies a collection of domain events.
    /// </summary>
    /// <param name="events">The domain events.</param>
    public void Apply(IEnumerable<IEvent> events)
    {
        foreach (var @event in events)
        {
            if (Apply(@event))
            {
                Version++;
            }
        }
    }

    /// <summary>
    /// Gets the event type filter.
    /// </summary>
    [JsonIgnore]
    public abstract Type[]? EventTypeFilter { get; }

    /// <summary>
    /// Applies an event.
    /// </summary>
    /// <typeparam name="T">The event type.</typeparam>
    /// <param name="event"></param>
    /// <returns>True if applied.</returns>
    protected abstract bool Apply<T>(T @event) where T : IEvent;

    /// <summary>
    /// Checks if the event type is handled.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <returns>True if handled.</returns>
    public bool IsEventHandled(Type eventType)
    {
        if (EventTypeFilter == null || EventTypeFilter.Length == 0)
        {
            return true;
        }

        return EventTypeFilter.Contains(eventType);
    }
}
