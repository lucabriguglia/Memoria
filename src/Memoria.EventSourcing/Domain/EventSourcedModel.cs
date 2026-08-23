using Newtonsoft.Json;

namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Abstract base class shared by every event-sourced model. It owns the concerns common to both
/// write models (<see cref="AggregateRoot"/>) and read models (<see cref="Projection"/>):
/// stream and aggregate identity, version tracking, and rebuilding state by applying domain events.
/// Write-model-only concerns such as staging uncommitted events live on <see cref="AggregateRoot"/>.
/// </summary>
public abstract class EventSourcedModel : IEventSourcedModel
{
    /// <summary>
    /// Gets or sets the stream ID.
    /// </summary>
    [JsonIgnore]
    public string StreamId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    [JsonIgnore]
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the latest event sequence.
    /// </summary>
    [JsonIgnore]
    public int LatestEventSequence { get; set; }

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
