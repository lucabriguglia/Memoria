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
/// Three engines. In-memory SQLite needs nothing installed, so its numbers are reproducible by
/// anyone reading them; SQL Server and PostgreSQL in containers are real engines paying real
/// per-command costs. All three run, because the question was whether the SQLite answer survives the
/// move to a real one.
/// </para>
/// <para>
/// It does: appends cost 1.9x to 2.6x on all three, though the absolute cost is several times higher
/// on a real engine. What no local container can answer is distance — they run on this machine, so a
/// round trip is loopback rather than a network hop. <see cref="RoundTripReport"/> is for that case:
/// a DCB append issues five more commands than a streamed one on every engine, and that count is
/// what a remote database charges for.
/// </para>
/// </remarks>
public sealed class StoreBenchmarkHarness : IAsyncDisposable
{
    private readonly StoreEngine _engine;
    private readonly IDisposable? _streamedConnection;
    private readonly IDisposable? _dcbConnection;
    private readonly BenchmarkDbContext? _streamedContext;
    private readonly BenchmarkDcbDbContext? _dcbContext;
    private readonly IDcbDomainService? _dcb;

    public StoreBenchmarkHarness(StoreEngine engine = StoreEngine.Sqlite)
    {
        _engine = engine;
        ConfigureTypeBindings();

        Commands = new CommandCountingInterceptor();

        if (engine is StoreEngine.Cosmos)
        {
            // Not an Entity Framework Core provider, so there is no context, no interceptor and no
            // DCB side. Cosmos hosts the streamed store only.
            Streamed = CosmosEmulator.CreateDomainService(HttpContextAccessor());
            return;
        }

        // A database each. One EnsureCreated on a database that already has tables does nothing, so
        // putting both schemas in one database silently leaves the second store without its tables.
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
        _dcb = new EntityFrameworkCoreDcbDomainService(_dcbContext);
    }

    /// <summary>Counts the commands each store sends, for <see cref="RoundTripReport"/>.</summary>
    public CommandCountingInterceptor Commands { get; }

    public IDomainService Streamed { get; }

    /// <summary>The DCB store, on the engines that have one.</summary>
    /// <exception cref="InvalidOperationException">The engine hosts no DCB store.</exception>
    public IDcbDomainService Dcb => _dcb ?? throw new InvalidOperationException(
        $"{_engine} hosts no DCB store, so there is nothing to compare against here. " +
        "See docs/concepts/providers.md.");

    /// <summary>The DCB context.s underlying connection, for ad hoc diagnostics.</summary>
    public System.Data.Common.DbConnection DcbConnection => _dcbContext!.Database.GetDbConnection();

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
        Ensure(await Streamed.SaveEvents(StreamId, streamed, expectedEventSequence: 0));

        if (_dcbContext is not null)
        {
            Ensure(await _dcbContext.SaveEvents(tagged, condition: null, maxEventsPerAppend: int.MaxValue));
        }

        await UpdateStatistics(_engine);
    }

    /// <summary>
    /// Builds and stores both snapshots, so a <see cref="ReadMode.SnapshotOnly"/> read has something
    /// to find and the two stores are compared from the same starting state.
    /// </summary>
    public async Task WriteSnapshots()
    {
        Ensure(await Streamed.GetAggregate(StreamId, StreamedId, ReadMode.SnapshotOrCreate));
        if (_dcb is not null)
        {
            Ensure(await _dcb.GetAggregate(DcbId, ReadMode.SnapshotOrCreate));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_streamedContext is not null) await _streamedContext.DisposeAsync();
        if (_dcbContext is not null) await _dcbContext.DisposeAsync();
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


    /// <summary>
    /// Brings the engine's planner statistics up to date after the seed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PostgreSQL only. Without it the DCB read is measured against a table the planner knows nothing
    /// about, and it is unusually sensitive to that: both its predicates are <c>= ANY(@array)</c>,
    /// whose selectivity PostgreSQL cannot estimate from a parameter, so it defaults to one row on
    /// each side, picks a nested loop semi join and applies the position match as a filter rather than
    /// an index condition. At a thousand events that removed 499,500 rows by filter and took 80ms
    /// against 3ms for the same query after ANALYZE.
    /// </para>
    /// <para>
    /// A real database has statistics, so measuring without them measures autovacuum lag rather than
    /// the store. SQL Server needs no equivalent: it creates the missing statistics itself the first
    /// time a query needs them.
    /// </para>
    /// </remarks>
    private async Task UpdateStatistics(StoreEngine engine)
    {
        if (engine is not StoreEngine.PostgreSql)
        {
            return;
        }

        await _streamedContext!.Database.ExecuteSqlRawAsync("ANALYZE;");
        await _dcbContext!.Database.ExecuteSqlRawAsync("ANALYZE;");
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
