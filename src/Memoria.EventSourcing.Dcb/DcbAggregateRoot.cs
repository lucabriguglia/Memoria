using Memoria.EventSourcing.Domain;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Abstract base class for DCB write models. It reuses the fold from
/// <see cref="EventSourcedModel"/> unchanged — version tracking, the event type filter and
/// <c>Apply</c> — and adds the two things specific to a write model whose boundary is dynamic:
/// staging events, and the tags those events are appended under.
/// </summary>
/// <remarks>
/// It deliberately does not derive from <see cref="StreamedModel"/>. A DCB aggregate belongs to no
/// stream, so inheriting <see cref="StreamedModel.StreamId"/> would give it a public settable
/// property with no honest value to put in it.
/// </remarks>
/// <example>
/// <code>
/// [AggregateType("Seat")]
/// public class SeatAggregate : DcbAggregateRoot
/// {
///     public override Type[]? EventTypeFilter { get; } = [typeof(SeatReservedEvent)];
///
///     public void Reserve(string seatId, string studentId) =&gt;
///         Add(new SeatReservedEvent(seatId, studentId),
///             new Tag("seat", seatId), new Tag("student", studentId));
///
///     protected override bool Apply&lt;T&gt;(T @event) =&gt; /* ... */;
/// }
/// </code>
/// </example>
public abstract class DcbAggregateRoot : DcbModel, IDcbAggregateRoot
{
    private readonly List<TaggedEvent> _uncommittedEvents = [];

    /// <summary>
    /// Gets or sets the aggregate ID.
    /// </summary>
    [JsonIgnore]
    public string AggregateId { get; set; } = null!;

    /// <summary>
    /// Gets the events staged but not yet appended, with the tags they will be appended under.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<TaggedEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    /// <summary>
    /// Stages and applies an event.
    /// </summary>
    /// <param name="event">The event.</param>
    /// <param name="tags">
    /// The tags to append the event under. When none are given the event inherits
    /// <see cref="Tags"/>, which is the common case for an aggregate whose every event concerns the
    /// same things it does.
    /// </param>
    /// <exception cref="ArgumentException">
    /// No tags were given and <see cref="Tags"/> is empty, so nothing would ever match the event.
    /// </exception>
    protected void Add(IEvent @event, params Tag[] tags)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(tags);

        var eventTags = tags.Length > 0 ? [..tags] : Tags;

        if (eventTags.Count == 0)
        {
            throw new ArgumentException(
                $"'{@event.GetType().Name}' was staged with no tags and {GetType().Name}.Tags is empty, so no query could ever reach it. Pass tags to Add, or set Tags on the aggregate.",
                nameof(tags));
        }

        _uncommittedEvents.Add(new TaggedEvent(@event, eventTags));

        if (Apply(@event))
        {
            Version++;
        }
    }
}
