using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql.Tests;

[EventType("SeatReserved")]
internal sealed record SeatReserved(string Seat) : IEvent;

[EventType("SeatReleased")]
internal sealed record SeatReleased(string Seat) : IEvent;

internal static class TestTypeBindings
{
    public static void Register()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            ["SeatReserved:1"] = typeof(SeatReserved),
            ["SeatReleased:1"] = typeof(SeatReleased)
        };
    }
}
