using Memoria.EventSourcing.Dcb.Tests.Models.Events;
using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb.Tests.Models.Aggregates;

/// <summary>
/// A DCB write model. It reuses the fold from <see cref="EventSourcedModel"/> unchanged; the only
/// difference from an <see cref="AggregateRoot"/> is that it belongs to no stream and its events
/// carry tags.
/// </summary>
[AggregateType("Seat")]
public class SeatAggregate : DcbAggregateRoot
{
    public string? SeatId { get; private set; }

    public string? ReservedBy { get; private set; }

    public override Type[]? EventTypeFilter { get; } = [typeof(SeatReservedEvent), typeof(SeatReleasedEvent)];

    public void Reserve(string seatId, string studentId) =>
        Add(new SeatReservedEvent(seatId, studentId), new Tag("seat", seatId), new Tag("student", studentId));

    /// <summary>
    /// Releases the seat without naming tags, so the event inherits the aggregate's own.
    /// </summary>
    public void Release(string seatId) => Add(new SeatReleasedEvent(seatId));

    protected override bool Apply<T>(T @event)
    {
        switch (@event)
        {
            case SeatReservedEvent reserved:
                SeatId = reserved.SeatId;
                ReservedBy = reserved.StudentId;
                return true;
            case SeatReleasedEvent released:
                SeatId = released.SeatId;
                ReservedBy = null;
                return true;
            default:
                return false;
        }
    }
}
