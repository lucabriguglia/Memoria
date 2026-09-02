using System.Data.Common;
using System.Security.Claims;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore;
using Memoria.Results;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.Benchmarks.Store;

/// <summary>
/// A database per store on a chosen engine, seeded with the same events, so a benchmark can put the
/// streamed store and the DCB store side by side.
/// </summary>
/// <remarks>
/// <para>
/// Two engines. In-memory SQLite needs nothing installed, so its numbers are reproducible by anyone
/// reading them; SQL Server in a container is a real engine paying real per-command costs. Both are
/// run, because the whole question was whether the SQLite answer survives the move.
/// </para>
/// <para>
/// It did: appends cost about 2x on both, though the absolute cost is roughly sevenfold higher on
/// SQL Server. What neither engine can answer is distance — the container is on this machine, so a
/// round trip is loopback rather than a network hop. <see cref="RoundTripReport"/> exists for that
/// case: a DCB append issues five more commands than a streamed one, on both engines, and that count
/// is what a remote database charges for.
/// </para>
/// </remarks>
public sealed class StoreBenchmarkHarness : IAsyncDisposable
{
    private readonly IDisposable? _streamedConnection;
    private readonly IDisposable? _dcbConnection;
    private readonly BenchmarkDbContext _streamedContext;
    private readonly BenchmarkDcbDbContext _dcbContext;

    public StoreBenchmarkHarness(StoreEngine engine = StoreEngine.Sqlite)
    {
        ConfigureTypeBindings();

        // A database each. One EnsureCreated on a database that already has tables does
        // nothing, so putting both schemas in one database silently leaves the second store
        // without its tables.

        Commands = new CommandCountingInterceptor();

        _streamedContext = new BenchmarkDbContext(
            new DbContextOptionsBuilder<DomainDbContext>()
                .UseEngine(engine, "streamed", out _streamedConnection)
                .AddInterceptors(Commands)
                .Options,
            TimeProvider.System, HttpContextAccessor());

        _dcbContext = new BenchmarkDcbDbContext(
            new DbContextOptionsBuilder<DcbDbContext>()
                .UseEngine(engine, "dcb", out _dcbConnection)
                .AddInterceptors(Commands)
                .Options,
            TimeProvider.System, HttpContextAccessor());

        _streamedContext.Database.EnsureCreated();
        _dcbContext.Database.EnsureCreated();

        Streamed = new EntityFrameworkCoreDomainService(_streamedContext);
        Dcb = new EntityFrameworkCoreDcbDomainService(_dcbContext);
    }

    /// <summary>Counts the commands each store sends, for <see cref="RoundTripReport"/>.</summary>
    public CommandCountingInterceptor Commands { get; }

    public IDomainService Streamed { get; }

    public IDcbDomainService Dcb { get; }

    public static ShowStreamId StreamId { get; } = new("show-1");

    public static StreamedSeatsId StreamedId { get; } = new("show-1");

    public static DcbSeatsId DcbId { get; } = new("show-1");

    /// <summary>
    /// Writes the same events to both stores: to the stream as one append, and to the log tagged
    /// <c>show:show-1</c> so the DCB boundary selects exactly the same set.
    /// </summary>
    public async Task Seed(int events)
    {
        var streamed = new IEvent[events];
        var tagged = new TaggedEvent[events];
        var tag = new Tag("show", "show-1");

        for (var seat = 0; seat < events; seat++)
        {
            var @event = new SeatReservedEvent($"seat-{seat}", $"customer-{seat}", 19.99m);
            streamed[seat] = @event;
            tagged[seat] = new TaggedEvent(@event, [tag]);
        }

        // Checked, not ignored. A silently failed seed would leave every benchmark measuring
        // a query against an empty table, which looks like a very fast store.
        Ensure(await _streamedContext.SaveEvents(StreamId, streamed, expectedEventSequence: 0));
        Ensure(await _dcbContext.SaveEvents(tagged, condition: null, maxEventsPerAppend: int.MaxValue));
    }

    /// <summary>
    /// Builds and stores both snapshots, so a <see cref="ReadMode.SnapshotOnly"/> read has something
    /// to find and the two stores are compared from the same starting state.
    /// </summary>
    public async Task WriteSnapshots()
    {
        Ensure(await Streamed.GetAggregate(StreamId, StreamedId, ReadMode.SnapshotOrCreate));
        Ensure(await Dcb.GetAggregate(DcbId, ReadMode.SnapshotOrCreate));
    }

    public async ValueTask DisposeAsync()
    {
        await _streamedContext.DisposeAsync();
        await _dcbContext.DisposeAsync();
        _streamedConnection?.Dispose();
        _dcbConnection?.Dispose();
    }



    private static void Ensure(Result result)
    {
        if (result.IsNotSuccess)
        {
            throw new InvalidOperationException($"Benchmark setup failed: {result.Failure!.Description}");
        }
    }

    private static void Ensure<T>(Result<T> result)
    {
        if (result.IsNotSuccess)
        {
            throw new InvalidOperationException($"Benchmark setup failed: {result.Failure!.Description}");
        }
    }

    private static void ConfigureTypeBindings()
    {
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "BenchmarkSeatReserved:1", typeof(SeatReservedEvent) }
        };

        TypeBindings.AggregateTypeBindings = new Dictionary<string, Type>
        {
            { "BenchmarkStreamedSeats:1", typeof(StreamedSeats) }
        };

        DcbTypeBindings.AggregateTypeBindings = new Dictionary<string, Type>
        {
            { "BenchmarkDcbSeats:1", typeof(DcbSeats) }
        };
    }

    private static IHttpContextAccessor HttpContextAccessor() =>
        new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "benchmark")], "Benchmark"))
            }
        };
}

public class BenchmarkDbContext(DbContextOptions<DomainDbContext> options, TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor) : DomainDbContext(options, timeProvider, httpContextAccessor);

public class BenchmarkDcbDbContext(DbContextOptions<DcbDbContext> options, TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor) : DcbDbContext(options, timeProvider, httpContextAccessor);

/// <summary>
/// Records every command a store sends, so round trips can be counted rather than inferred from
/// reading the code.
/// </summary>
public sealed class CommandCountingInterceptor : DbCommandInterceptor
{
    private readonly List<string> _commands = [];

    public IReadOnlyList<string> Commands => _commands;

    public void Clear() => _commands.Clear();

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default) => Record(command, result);

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default) => Record(command, result);

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default) => Record(command, result);

    private ValueTask<InterceptionResult<T>> Record<T>(DbCommand command, InterceptionResult<T> result)
    {
        _commands.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }
}
