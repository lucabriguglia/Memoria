using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Models;

[EventType("SeatReserved")]
public record SeatReservedEvent(string SeatId, string StudentId) : IEvent;

[EventType("SeatReleased")]
public record SeatReleasedEvent(string SeatId) : IEvent;

[EventType("CourseRenamed")]
public record CourseRenamedEvent(string CourseId, string Name) : IEvent;

/// <summary>
/// Folds reservations. Its <see cref="EventTypeFilter"/> deliberately excludes
/// <see cref="CourseRenamedEvent"/>, so a read narrowed only by tag still ignores it.
/// </summary>
[AggregateType("Seat")]
public class SeatAggregate : DcbAggregateRoot
{
    public string? ReservedBy { get; private set; }

    public int Reservations { get; private set; }

    public override Type[]? EventTypeFilter { get; } = [typeof(SeatReservedEvent), typeof(SeatReleasedEvent)];

    protected override bool Apply<T>(T @event)
    {
        switch (@event)
        {
            case SeatReservedEvent reserved:
                ReservedBy = reserved.StudentId;
                Reservations++;
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

/// <summary>
/// The same aggregate over an intersection boundary: one student's dealings with one seat, rather
/// than everything about the seat.
/// </summary>
public class SeatForStudentId(string seatId, string studentId) : IDcbAggregateId<SeatAggregate>
{
    public string Id { get; } = $"{seatId}-{studentId}";

    public TagQuery Boundary { get; } =
        TagQuery.AllOf(new Tag("seat", seatId), new Tag("student", studentId));
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
