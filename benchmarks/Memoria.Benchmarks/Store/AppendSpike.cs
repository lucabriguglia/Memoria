using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Memoria.Benchmarks.Store;

/// <summary>
/// SPIKE: one engine's single-call append, as the check, the benchmark and the contention runner
/// see it.
/// </summary>
public interface IAppendSpike : IAsyncDisposable
{
    StoreEngine Engine { get; }

    Task Install();

    /// <summary>The boundary's latest position, read the same way on both paths.</summary>
    Task<long> ReadLatest(TagQuery boundary);

    Task<(bool Appended, long LatestPosition)> SaveAggregate<T>(IDcbAggregateId<T> aggregateId, T aggregate,
        AppendCondition? condition, string? user) where T : IDcbAggregateRoot;

    /// <summary>A store context of its own on the same database, for one concurrent writer.</summary>
    BenchmarkDcbDbContext CreateContext(Action<string>? onError = null);
}

public static class AppendSpike
{
    public static IAppendSpike Create(StoreEngine engine, string connectionString) => engine switch
    {
        StoreEngine.PostgreSql => new PostgresEngine(connectionString),
        StoreEngine.SqlServer => new SqlServerEngine(connectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Spike: PostgreSQL and SQL Server only.")
    };

    private static DbContextOptionsBuilder<DcbDbContext> LogErrors(DbContextOptionsBuilder<DcbDbContext> builder,
        Action<string>? onError) =>
        onError is null ? builder : builder.LogTo(onError, Microsoft.Extensions.Logging.LogLevel.Error);

    private sealed class PostgresEngine(string connectionString) : IAppendSpike
    {
        private readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(connectionString);

        public StoreEngine Engine => StoreEngine.PostgreSql;

        public Task Install() => PostgresAppendSpike.Install(_dataSource);

        public async Task<long> ReadLatest(TagQuery boundary)
        {
            await using var read = _dataSource.CreateCommand(
                """SELECT coalesce(max("Position"), 0) FROM "DcbEventTags" WHERE "Tag" = ANY ($1)""");
            read.Parameters.Add(new NpgsqlParameter
            {
                Value = boundary.Tags.Select(tag => tag.ToString()).ToArray(),
                NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
            });
            return (long)(await read.ExecuteScalarAsync())!;
        }

        public Task<(bool Appended, long LatestPosition)> SaveAggregate<T>(IDcbAggregateId<T> aggregateId,
            T aggregate, AppendCondition? condition, string? user) where T : IDcbAggregateRoot =>
            PostgresAppendSpike.SaveAggregate(_dataSource, aggregateId, aggregate, condition, user);

        public BenchmarkDcbDbContext CreateContext(Action<string>? onError = null) => new(
            LogErrors(new DbContextOptionsBuilder<DcbDbContext>().UseNpgsql(connectionString), onError).Options,
            TimeProvider.System, StoreBenchmarkHarness.HttpContextAccessor());

        public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
    }

    private sealed class SqlServerEngine(string connectionString) : IAppendSpike
    {
        public StoreEngine Engine => StoreEngine.SqlServer;

        public Task Install() => SqlServerAppendSpike.Install(connectionString);

        public async Task<long> ReadLatest(TagQuery boundary)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var read = new SqlCommand(
                """
                SELECT ISNULL(MAX([Position]), 0) FROM [dbo].[DcbEventTags]
                WHERE [Tag] IN (SELECT [Tag] COLLATE SQL_Latin1_General_CP1_CS_AS
                                FROM OPENJSON(@tags) WITH ([Tag] nvarchar(255) '$'))
                """, connection);
            read.Parameters.Add("@tags", System.Data.SqlDbType.NVarChar, -1).Value =
                System.Text.Json.JsonSerializer.Serialize(boundary.Tags.Select(tag => tag.ToString()));
            // Synchronous on purpose: see the contention notes on SqlClient async latency.
            return (long)read.ExecuteScalar()!;
        }

        public Task<(bool Appended, long LatestPosition)> SaveAggregate<T>(IDcbAggregateId<T> aggregateId,
            T aggregate, AppendCondition? condition, string? user) where T : IDcbAggregateRoot =>
            SqlServerAppendSpike.SaveAggregate(connectionString, aggregateId, aggregate, condition, user);

        public BenchmarkDcbDbContext CreateContext(Action<string>? onError = null) => new(
            LogErrors(new DbContextOptionsBuilder<DcbDbContext>().UseSqlServer(connectionString), onError).Options,
            TimeProvider.System, StoreBenchmarkHarness.HttpContextAccessor());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Checks an engine's single-call append against the store before trusting a timing of it: what
    /// it writes reads back through the store, a stale condition is refused, the store's own append
    /// against an old position is refused, and concurrent appends on one boundary never both succeed
    /// against the same position.
    /// </summary>
    public static async Task Check(StoreEngine engine, bool readCommittedSnapshot = false, int events = 100)
    {
        Console.WriteLine($"Checking the single-call append on {engine}{(readCommittedSnapshot ? " with READ_COMMITTED_SNAPSHOT ON" : "")}.");

        await using var harness = new StoreBenchmarkHarness(engine);

        if (readCommittedSnapshot)
        {
            // The Azure SQL default. Statements then read versions instead of taking locks, which is
            // exactly where a boundary check could read stale data.
            await using var connection = new SqlConnection(harness.DcbConnectionString);
            await connection.OpenAsync();
            await using var alter = new SqlCommand(
                "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE; " +
                "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID();", connection);
            Assert((bool)(await alter.ExecuteScalarAsync())!, "READ_COMMITTED_SNAPSHOT did not switch on");
        }
        await harness.Seed(events);
        await harness.WriteSnapshots();

        await using var spike = Create(engine, harness.DcbConnectionString);
        await spike.Install();

        var boundary = StoreBenchmarkHarness.DcbId.Boundary;
        var position = (await harness.Dcb.GetLatestPosition(boundary)).Value;

        // 1. An append through the single call reads back through the store.
        var aggregate = new DcbSeats { Tags = boundary.Tags, LatestPosition = position, Version = events };
        aggregate.Reserve("seat-check", "customer", 19.99m);
        var (appended, written) = await spike.SaveAggregate(StoreBenchmarkHarness.DcbId, aggregate,
            new AppendCondition(boundary, position), "benchmark");
        Assert(appended, "the first append was refused");
        Assert(written > position, $"position did not move ({position} -> {written})");

        var stored = (await harness.Dcb.GetAggregate(StoreBenchmarkHarness.DcbId, ReadMode.SnapshotOnly)).Value!;
        // The aggregate was built without folding the seed, so it is compared with itself, not the log.
        Assert(stored.Reserved == aggregate.Reserved && stored.Version == aggregate.Version,
            $"snapshot holds {stored.Reserved} reservations at version {stored.Version}, " +
            $"expected {aggregate.Reserved} at {aggregate.Version}");
        Assert(stored.LatestPosition == written, $"snapshot at {stored.LatestPosition}, expected {written}");

        var read = (await harness.Dcb.GetEvents(boundary)).Value!;
        Assert(read.Count == events + 1, $"store reads {read.Count} events, expected {events + 1}");
        Console.WriteLine($"ok   append reads back through the store ({read.Count} events, snapshot at {written})");

        // 2. A stale condition is refused, writes nothing, and reports where the boundary is.
        var stale = new DcbSeats { Tags = boundary.Tags, LatestPosition = position, Version = events };
        stale.Reserve("seat-stale", "customer", 19.99m);
        var (staleAppended, reported) = await spike.SaveAggregate(StoreBenchmarkHarness.DcbId, stale,
            new AppendCondition(boundary, position), "benchmark");
        Assert(!staleAppended, "a stale append was accepted");
        Assert(reported == written, $"refusal reported {reported}, expected {written}");
        Assert((await harness.Dcb.GetEvents(boundary)).Value!.Count == events + 1, "a refused append wrote events");
        Console.WriteLine("ok   stale condition refused, nothing written");

        // 3. The store's own optimistic append against the old position is refused too.
        var storeAppend = new DcbSeats { Tags = boundary.Tags, LatestPosition = position, Version = events };
        storeAppend.Reserve("seat-store", "customer", 19.99m);
        var storeResult = await harness.Dcb.SaveAggregate(StoreBenchmarkHarness.DcbId, storeAppend,
            new AppendCondition(boundary, position));
        Assert(storeResult.IsNotSuccess, "the store accepted an append against a stale position");
        Console.WriteLine($"ok   store append against the old position refused ({storeResult.Failure!.Type})");

        // 4. Tags differing only in case are two tags, not one.
        var upper = new DcbSeatsId("Case-Check");
        var lower = new DcbSeatsId("case-check");
        var first = new DcbSeats { Tags = upper.Boundary.Tags };
        first.Reserve("seat-upper", "customer", 19.99m);
        Assert((await spike.SaveAggregate(upper, first, AppendCondition.NothingAppendedFor(upper.Boundary), "benchmark")).Appended,
            "append under the upper-case tag was refused");
        var second = new DcbSeats { Tags = lower.Boundary.Tags };
        second.Reserve("seat-lower", "customer", 19.99m);
        Assert((await spike.SaveAggregate(lower, second, AppendCondition.NothingAppendedFor(lower.Boundary), "benchmark")).Appended,
            "a lower-case tag was treated as the upper-case one");
        Console.WriteLine("ok   tags differing only in case are distinct");

        // 5. Several events in one append keep their order and each keeps its own tags.
        var multi = new DcbSeatsId("multi");
        var batch = new DcbSeats { Tags = multi.Boundary.Tags };
        for (var i = 0; i < 5; i++)
        {
            batch.Reserve($"seat-multi-{i}", "customer", i);
        }

        Assert((await spike.SaveAggregate(multi, batch, AppendCondition.NothingAppendedFor(multi.Boundary), "benchmark")).Appended,
            "multi-event append was refused");
        var multiRead = (await harness.Dcb.GetEvents(multi.Boundary)).Value!;
        var seats = multiRead.Select(e => ((SeatReservedEvent)e).SeatId).ToArray();
        Assert(seats.SequenceEqual(Enumerable.Range(0, 5).Select(i => $"seat-multi-{i}")),
            $"events read back as [{string.Join(", ", seats)}]");
        Console.WriteLine("ok   a five-event append reads back in order under its tag");

        // 6. Concurrency: writers race on one boundary, each re-reading and retrying on refusal.
        const int writers = 8;
        const int appendsEach = 25;
        var acceptedOn = new ConcurrentBag<long>();
        var refusals = 0;

        await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
        {
            for (var n = 0; n < appendsEach;)
            {
                var seen = await spike.ReadLatest(boundary);
                var racer = new DcbSeats { Tags = boundary.Tags, LatestPosition = seen, Version = 1 };
                racer.Reserve($"seat-{writer}-{n}", "customer", 19.99m);
                var (ok, _) = await spike.SaveAggregate(StoreBenchmarkHarness.DcbId, racer,
                    new AppendCondition(boundary, seen), "benchmark");

                if (ok)
                {
                    acceptedOn.Add(seen);
                    n++;
                }
                else
                {
                    Interlocked.Increment(ref refusals);
                }
            }
        })));

        var distinct = acceptedOn.Distinct().Count();
        var total = (await harness.Dcb.GetEvents(boundary)).Value!.Count;
        Assert(distinct == acceptedOn.Count, $"{acceptedOn.Count - distinct} appends succeeded against a position another append also used");
        Assert(total == events + 1 + writers * appendsEach, $"log holds {total} events, expected {events + 1 + writers * appendsEach}");
        Console.WriteLine($"ok   {writers} writers x {appendsEach} appends: {acceptedOn.Count} accepted on distinct positions, {refusals} refused and retried, log consistent");
    }

    private static void Assert(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Spike check failed: " + failure);
        }
    }
}

/// <summary>
/// SPIKE: the store's DCB append against the single-call append, on PostgreSQL and SQL Server, in the
/// same shape as <see cref="StoreWriteBenchmarks"/> so the numbers are comparable.
/// </summary>
[MemoryDiagnoser]
[MedianColumn]
[SimpleJob(RunStrategy.Monitoring, invocationCount: 1, warmupCount: 10, iterationCount: 200)]
public class AppendSpikeBenchmarks
{
    private StoreBenchmarkHarness _harness = null!;
    private IAppendSpike _spike = null!;
    private long _expectedPosition;
    private int _seat;

    [Params(StoreEngine.PostgreSql, StoreEngine.SqlServer)]
    public StoreEngine Engine { get; set; }

    [Params(10, 100, 1000)]
    public int Events { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _harness = new StoreBenchmarkHarness(Engine);
        _harness.Seed(Events).GetAwaiter().GetResult();
        _harness.WriteSnapshots().GetAwaiter().GetResult();

        _spike = AppendSpike.Create(Engine, _harness.DcbConnectionString);
        _spike.Install().GetAwaiter().GetResult();

        _expectedPosition = _harness.Dcb.GetLatestPosition(StoreBenchmarkHarness.DcbId.Boundary)
            .GetAwaiter().GetResult().Value;
        _seat = Events;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _spike.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _harness.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, Description = "SaveAggregate (DCB, EF Core)")]
    public async Task SaveAggregateStore()
    {
        var aggregate = NextAggregate();

        var result = await _harness.Dcb.SaveAggregate(StoreBenchmarkHarness.DcbId, aggregate,
            new AppendCondition(StoreBenchmarkHarness.DcbId.Boundary, _expectedPosition));

        Guard(result.IsSuccess);
        _expectedPosition = aggregate.LatestPosition;
    }

    [Benchmark(Description = "SaveAggregate (DCB, single call)")]
    public async Task SaveAggregateSingleCall()
    {
        var aggregate = NextAggregate();

        var (appended, _) = await _spike.SaveAggregate(StoreBenchmarkHarness.DcbId, aggregate,
            new AppendCondition(StoreBenchmarkHarness.DcbId.Boundary, _expectedPosition), "benchmark");

        Guard(appended);
        _expectedPosition = aggregate.LatestPosition;
    }

    private DcbSeats NextAggregate()
    {
        var aggregate = new DcbSeats
        {
            Tags = StoreBenchmarkHarness.DcbId.Boundary.Tags,
            LatestPosition = _expectedPosition,
            Version = Events
        };

        aggregate.Reserve($"seat-{_seat++}", "customer", 19.99m);
        return aggregate;
    }

    private static void Guard(bool succeeded)
    {
        if (!succeeded)
        {
            throw new InvalidOperationException("The append was refused, so this iteration measured the failure path.");
        }
    }
}
