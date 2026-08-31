using Memoria.EventSourcing.Dcb.Tests.Models.Events;
using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb.Tests.Models.Projections;

/// <summary>
/// A DCB read model. Like a <see cref="Projection"/> it never produces events, and like a
/// <see cref="Dcb.DcbAggregateRoot"/> it belongs to no stream.
/// </summary>
[ProjectionType("SeatSummary")]
public class SeatProjection : DcbProjection
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
