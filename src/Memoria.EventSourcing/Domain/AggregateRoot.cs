using Newtonsoft.Json;

namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Abstract base class for aggregates in event sourcing.
/// </summary>
public abstract class AggregateRoot : IAggregateRoot
{
    /// <summary>
    /// Gets or sets the stream ID.
    /// </summary>
    [JsonIgnore]
    public string StreamId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the aggregate ID.
    /// </summary>
    [JsonIgnore]
    public string AggregateId { get; set; } = null!;

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
    /// Private collection of uncommitted events.
    /// </summary>
    [JsonIgnore]
    private readonly List<IEvent> _uncommittedEvents = [];

    /// <summary>
    /// Gets the uncommitted events.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<IEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    /// <summary>
    /// Adds and applies a event.
    /// </summary>
    /// <param name="event">The event.</param>
    protected void Add(IEvent @event)
    {
        _uncommittedEvents.Add(@event);

        if (Apply(@event))
        {
            Version++;
        }
    }

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
