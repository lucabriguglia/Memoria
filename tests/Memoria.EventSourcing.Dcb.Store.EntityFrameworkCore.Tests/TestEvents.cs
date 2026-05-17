using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests;

[EventType("SeatReserved")]
internal sealed record SeatReserved(string Seat) : IEvent;

[EventType("SeatReleased")]
internal sealed record SeatReleased(string Seat) : IEvent;

[EventType("PaymentTaken")]
internal sealed record PaymentTaken(string OrderId, decimal Amount) : IEvent;

internal static class TestTypeBindings
{
    public static void Register()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            ["SeatReserved:1"] = typeof(SeatReserved),
            ["SeatReleased:1"] = typeof(SeatReleased),
            ["PaymentTaken:1"] = typeof(PaymentTaken)
        };
    }
}
