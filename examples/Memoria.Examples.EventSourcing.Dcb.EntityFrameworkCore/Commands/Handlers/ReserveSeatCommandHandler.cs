using Memoria.Commands;
using Memoria.EventSourcing.Dcb;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Events;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.State;
using Memoria.Results;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Commands.Handlers;

public sealed class ReserveSeatCommandHandler(IDcbStore store) : ICommandHandler<ReserveSeatCommand>
{
    public async Task<Result> Handle(ReserveSeatCommand command, CancellationToken cancellationToken = default)
    {
        var query = DcbQuery.Of(new QueryItem(
            [typeof(SeatReservedEvent)],
            TagSet.Of(
                new Tag("showId", command.ShowId),
                new Tag("seat", command.Seat))));

        var result = await store.Decide(
            query: query,
            initialState: SeatState.Empty,
            fold: (state, evt) => state.Apply(evt),
            decide: state => state.Taken
                ? Result<IReadOnlyList<EventToAppend>>.Fail(new Failure(
                    ErrorCode: ErrorCode.UnprocessableEntity,
                    Title: "Seat already reserved",
                    Description: $"Seat {command.Seat} for show {command.ShowId} is already taken."))
                : Result<IReadOnlyList<EventToAppend>>.Ok(
                [
                    EventToAppend.Of(
                        new SeatReservedEvent(command.ShowId, command.Seat, command.CustomerId),
                        new Tag("showId", command.ShowId),
                        new Tag("seat", command.Seat))
                ]),
            cancellationToken);

        return result.IsSuccess ? Result.Ok() : result.Failure!;
    }
}
