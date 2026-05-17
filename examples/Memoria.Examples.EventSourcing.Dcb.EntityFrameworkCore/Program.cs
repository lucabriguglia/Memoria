using Memoria;
using Memoria.EventSourcing.Dcb.Extensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;
using Memoria.EventSourcing.Domain;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Commands;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Data;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Events;
using Memoria.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Box-office seat reservation. The consistency boundary is "(showId, seat)" —
// a tag set, not an aggregate stream. Two customers racing for the same seat
// would both see an empty log; the DCB store's conflict check, keyed on those
// tags, lets exactly one of them win.

RegisterEventTypes();

var services = new ServiceCollection();

var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BoxOffice.db");
File.Delete(dbPath); // Start each demo run from an empty log.

services.AddDbContext<BoxOfficeDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
services.AddScoped<IDcbDbContext>(sp => sp.GetRequiredService<BoxOfficeDbContext>());

services.AddMemoria(typeof(Program));
services.AddMemoriaDcb();
services.AddMemoriaDcbEntityFrameworkCore();

await using var provider = services.BuildServiceProvider();

await using (var scope = provider.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BoxOfficeDbContext>();
    await db.Database.EnsureCreatedAsync();
}

var dispatcher = provider.GetRequiredService<IDispatcher>();

var showId = "JFK-2026-05-17";
var alice = Guid.NewGuid();
var bob = Guid.NewGuid();

Console.WriteLine($"Show:  {showId}\n");

// Alice reserves seat A12 — succeeds.
var first = await dispatcher.Send(new ReserveSeatCommand(showId, Seat: "A12", CustomerId: alice));
Print("Alice reserves A12", first);

// Bob tries to reserve the same seat — DCB blocks it.
var second = await dispatcher.Send(new ReserveSeatCommand(showId, Seat: "A12", CustomerId: bob));
Print("Bob   reserves A12", second);

// Bob reserves a different seat — succeeds (disjoint tag set).
var third = await dispatcher.Send(new ReserveSeatCommand(showId, Seat: "A13", CustomerId: bob));
Print("Bob   reserves A13", third);

return;

static void RegisterEventTypes()
{
    TypeBindings.EventTypeBindings = new Dictionary<string, Type>
    {
        ["SeatReserved:1"] = typeof(SeatReservedEvent)
    };
}

static void Print(string label, Memoria.Results.Result result)
{
    if (result.IsSuccess)
    {
        Console.WriteLine($"  ok       {label}");
    }
    else
    {
        Console.WriteLine($"  rejected {label}: {result.Failure!.Title}");
    }
}
