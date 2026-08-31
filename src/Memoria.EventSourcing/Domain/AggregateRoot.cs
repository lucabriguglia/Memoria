using Newtonsoft.Json;

namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Abstract base class for aggregates (write models) in event sourcing. In addition to the shared
/// <see cref="EventSourcedModel"/> behaviour, an aggregate stages new domain events as uncommitted
/// events via <see cref="Add"/> until they are persisted.
/// </summary>
public abstract class AggregateRoot : StreamedModel, IAggregateRoot
{
    /// <summary>
    /// Gets or sets the aggregate ID.
    /// </summary>
    [JsonIgnore]
    public string AggregateId { get; set; } = null!;

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
}
