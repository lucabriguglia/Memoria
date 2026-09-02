using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;

namespace Memoria.Benchmarks.Store;

/// <summary>
/// The event both stores write. One type, so the payload the serializer handles is identical on
/// both sides and the comparison is about the store rather than about the model.
/// </summary>
[EventType("BenchmarkSeatReserved")]
public record SeatReservedEvent(string SeatId, string CustomerId, decimal Price) : IEvent;

/// <summary>
/// The streamed write model.
/// </summary>
[AggregateType("BenchmarkStreamedSeats")]
public class StreamedSeats : AggregateRoot
{
    public override Type[] EventTypeFilter { get; } = [typeof(SeatReservedEvent)];

    public int Reserved { get; private set; }

    public decimal Takings { get; private set; }

    public void Reserve(string seatId, string customerId, decimal price) =>
        Add(new SeatReservedEvent(seatId, customerId, price));

    protected override bool Apply<T>(T @event)
    {
        if (@event is not SeatReservedEvent reserved)
        {
            return false;
        }

        Reserved++;
        Takings += reserved.Price;
        return true;
    }
}

/// <summary>
/// The DCB write model, deliberately the same state and the same fold as
/// <see cref="StreamedSeats"/>. Only the identity and the boundary differ, which is the whole point:
/// any difference the benchmarks show is the store's, not the model's.
/// </summary>
[AggregateType("BenchmarkDcbSeats")]
public class DcbSeats : DcbAggregateRoot
{
    public override Type[] EventTypeFilter { get; } = [typeof(SeatReservedEvent)];

    public int Reserved { get; private set; }

    public decimal Takings { get; private set; }

    public void Reserve(string seatId, string customerId, decimal price) =>
        Add(new SeatReservedEvent(seatId, customerId, price));

    protected override bool Apply<T>(T @event)
    {
        if (@event is not SeatReservedEvent reserved)
        {
            return false;
        }

        Reserved++;
        Takings += reserved.Price;
        return true;
    }
}

public class ShowStreamId(string showId) : IStreamId
{
    public string Id { get; } = $"show:{showId}";
}

/// <summary>
/// Deliberately not the same string as <see cref="ShowStreamId"/>.
/// </summary>
/// <remarks>
/// On Cosmos DB an event document is keyed <c>{streamId}:{sequence}</c> and an aggregate document
/// <c>{aggregateId}:{typeVersion}</c>, in one container. Give the stream and the aggregate the same
/// string and a version 1 aggregate collides with the event at sequence 1.
/// </remarks>
public class StreamedSeatsId(string showId) : IAggregateId<StreamedSeats>
{
    public string Id { get; } = $"seats:{showId}";

    public IDictionary<string, string>? EventPropertyFilter => null;
}

/// <summary>
/// The DCB counterpart. One tag, so the boundary selects exactly the events the stream would.
/// </summary>
public class DcbSeatsId(string showId) : IDcbAggregateId<DcbSeats>
{
    public string Id { get; } = showId;

    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("show", showId));
}
