using Memoria.EventSourcing.Domain;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Events;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.State;

/// <summary>
/// The folded decision state for a single (show, seat) pair: was it ever reserved?
/// </summary>
public sealed record SeatState(bool Taken)
{
    public static SeatState Empty { get; } = new(Taken: false);

    public SeatState Apply(IEvent evt) => evt switch
    {
        SeatReservedEvent => this with { Taken = true },
        _ => this
    };
}
