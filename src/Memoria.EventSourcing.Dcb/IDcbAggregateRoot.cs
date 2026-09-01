using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Defines the contract for a DCB write model. Like an <see cref="IAggregateRoot"/> it stages new
/// domain events until they are persisted, but its consistency boundary is a
/// <see cref="TagQuery"/> evaluated at append time rather than a stream fixed at design time — so
/// it carries no stream identity and its staged events carry tags.
/// </summary>
public interface IDcbAggregateRoot : IDcbModel
{
    /// <summary>
    /// Gets or sets the unique identifier for this aggregate instance.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies this specific aggregate within its type. Used to key its
    /// snapshot; it does not select the events the aggregate is built from — the tag query does.
    /// </value>
    string AggregateId { get; set; }

    /// <summary>
    /// Gets the events that have been staged but not yet appended, with the tags they will be
    /// appended under.
    /// </summary>
    IReadOnlyCollection<TaggedEvent> UncommittedEvents { get; }
}
