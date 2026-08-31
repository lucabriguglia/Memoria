using Memoria.EventSourcing.Dcb.Tests.Models.Events;
using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb.Tests.Models.Aggregates;

/// <summary>
/// A streamed write model deliberately sharing its <see cref="AggregateType"/> name and version
/// with <see cref="SeatAggregate"/>. Two consistency models may legitimately each have a "Seat"
/// aggregate, which is why the aggregate binding maps are separate.
/// </summary>
[AggregateType("Seat")]
public class StreamedSeatAggregate : AggregateRoot
{
    public override Type[]? EventTypeFilter { get; } = [typeof(SeatReservedEvent)];

    protected override bool Apply<T>(T @event) => @event is SeatReservedEvent;
}
