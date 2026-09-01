using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;

[EventType("SeatReserved")]
public record SeatReservedEvent(string SeatId, string StudentId) : IEvent;

[EventType("SeatReleased")]
public record SeatReleasedEvent(string SeatId) : IEvent;

[AggregateType("Seat")]
public class SeatAggregate : DcbAggregateRoot
{
    public string? ReservedBy { get; private set; }

    public override Type[]? EventTypeFilter { get; } = [typeof(SeatReservedEvent), typeof(SeatReleasedEvent)];

    public void Reserve(string seatId, string studentId) =>
        Add(new SeatReservedEvent(seatId, studentId), new Tag("seat", seatId), new Tag("student", studentId));

    protected override bool Apply<T>(T @event)
    {
        switch (@event)
        {
            case SeatReservedEvent reserved:
                ReservedBy = reserved.StudentId;
                return true;
            case SeatReleasedEvent:
                ReservedBy = null;
                return true;
            default:
                return false;
        }
    }
}

public class SeatId(string seatId) : IDcbAggregateId<SeatAggregate>
{
    public string Id { get; } = seatId;

    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("seat", seatId));
}

[ProjectionType("SeatSummary")]
public class SeatSummaryProjection : DcbProjection
{
    public int Reservations { get; private set; }

    public override Type[]? EventTypeFilter { get; } = [typeof(SeatReservedEvent)];

    protected override bool Apply<T>(T @event)
    {
        if (@event is not SeatReservedEvent)
        {
            return false;
        }

        Reservations++;
        return true;
    }
}

public class SeatSummaryId(string seatId) : IDcbProjectionId<SeatSummaryProjection>
{
    public string Id { get; } = seatId;

    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("seat", seatId));
}

/// <summary>
/// The same seat aggregate after its boundary was widened to include the student.
/// </summary>
/// <remarks>
/// Stands for a redeploy in which an identifier's boundary changed. Snapshots are keyed by the
/// boundary that produced them, so the ones written under the narrower boundary become unreachable
/// and are rebuilt rather than returned as if they were folds of the wider one.
/// </remarks>
public class WideSeatId(string seatId) : IDcbAggregateId<SeatAggregate>
{
    public string Id { get; } = seatId;

    public TagQuery Boundary { get; } =
        TagQuery.AnyOf(new Tag("seat", seatId), new Tag("student", "s7"));
}
