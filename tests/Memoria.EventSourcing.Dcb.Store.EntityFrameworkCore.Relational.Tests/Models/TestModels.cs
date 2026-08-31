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

public class SeatId(string id) : IDcbAggregateId<SeatAggregate>
{
    public string Id { get; } = id;
}
