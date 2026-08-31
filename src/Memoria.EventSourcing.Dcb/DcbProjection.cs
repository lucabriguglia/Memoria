using Memoria.EventSourcing.Domain;
using Newtonsoft.Json;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Abstract base class for DCB read models. It reuses the fold from
/// <see cref="EventSourcedModel"/> unchanged and adds only projection identity: a projection never
/// produces events, so it has no <c>Add</c> and no uncommitted events, and it belongs to no stream,
/// so it has no <see cref="StreamedModel.StreamId"/>.
/// </summary>
/// <example>
/// <code>
/// [ProjectionType("SeatSummary")]
/// public class SeatSummaryProjection : DcbProjection
/// {
///     public int Reservations { get; private set; }
///
///     public override Type[]? EventTypeFilter { get; } = [typeof(SeatReservedEvent)];
///
///     protected override bool Apply&lt;T&gt;(T @event) =&gt; /* ... */;
/// }
/// </code>
/// </example>
public abstract class DcbProjection : DcbModel, IDcbProjection
{
    /// <summary>
    /// Gets or sets the projection ID.
    /// </summary>
    [JsonIgnore]
    public string ProjectionId { get; set; } = null!;
}
