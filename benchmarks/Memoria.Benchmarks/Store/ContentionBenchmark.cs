using System.Collections.Concurrent;
using System.Diagnostics;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Configurations;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Memoria.Benchmarks.Store;

/// <summary>
/// Concurrent writers appending through the DCB store's append routine and through its Entity
/// Framework Core path, on the same engine. Run with <c>--contention</c>, plus <c>--sqlserver</c>
/// or <c>--postgres</c>, <c>--rcsi</c> for READ_COMMITTED_SNAPSHOT on, and <c>--only=Hot</c> or
/// <c>--only=Disjoint</c>.
/// </summary>
/// <remarks>
/// <para>
/// Not a BenchmarkDotNet benchmark: the question is throughput across writers, which it does not
/// measure. Each case gets a fresh database, warms up, then runs every writer flat out for a fixed
/// window. A writer reads its boundary's latest position, saves an aggregate conditioned on it, and
/// on refusal reads again and retries — the loop a real DCB command handler runs.
/// </para>
/// <para>
/// Both paths go through the store's own code. The routine path is a <see cref="DcbDbContext"/>; the
/// Entity Framework Core path a context that implements <see cref="IDcbDbContext"/> without deriving
/// from it, which the store appends for through Entity Framework Core on every engine. The read is the
/// store's own on both.
/// </para>
/// <para>
/// Each database is seeded with <see cref="FillerTags"/> tags, each on one event and with its tag
/// head, and the writers' boundaries are tags drawn evenly from among them. Without that, every head
/// and tag the writers touch sits on one or two pages of a nearly empty table, and what is measured
/// is contention on those pages rather than on the rows.
/// </para>
/// <para>
/// Two scenarios: <b>hot</b>, every writer on one boundary, where at most one append per position can
/// win; and <b>disjoint</b>, every writer on its own boundary, where nothing conflicts logically and
/// what is measured is how well appends run side by side. Every accepted append is checked against
/// the others: two accepted on the same position in one boundary fail the run.
/// </para>
/// </remarks>
public static class ContentionBenchmark
{
    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

    /// <summary>How many tags, events and heads each database starts with.</summary>
    private const int FillerTags = 50_000;

    private enum Path { EfCore, Routine }

    private enum Scenario { Hot, Disjoint }

    private sealed record Outcome(Scenario Scenario, Path Path, int Writers, bool Backoff, int Accepted,
        int Refused, int Errors, double[] AppendMs, double[] CommandMs, string? FirstError);

    /// <summary>The first wait after a refusal; each further refusal in a row doubles the ceiling.</summary>
    private static readonly TimeSpan BackoffBase = TimeSpan.FromMilliseconds(1);

    private static readonly TimeSpan BackoffCap = TimeSpan.FromMilliseconds(20);

    [System.Runtime.InteropServices.DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint milliseconds);

    public static async Task Run(StoreEngine engine, string? only, bool readCommittedSnapshot)
    {
        // Windows ticks timers every 15.6ms by default, far coarser than the waits a backoff needs.
        if (OperatingSystem.IsWindows())
        {
            timeBeginPeriod(1);
        }

        StoreBenchmarkHarness.ConfigureTypeBindings();
        await DescribeServer(engine);

        int[] writerCounts = [1, 4, 16];
        var outcomes = new List<Outcome>();

        // Backoff only changes anything where appends are refused, so disjoint runs without it.
        var cases =
            from scenario in new[] { Scenario.Hot, Scenario.Disjoint }
            from backoff in scenario == Scenario.Hot ? new[] { false, true } : [false]
            from writers in writerCounts
            from path in new[] { Path.EfCore, Path.Routine }
            where only is null || scenario.ToString() == only
            select (scenario, backoff, writers, path);

        foreach (var (scenario, backoff, writers, path) in cases)
        {
            var outcome = await RunCase(engine, scenario, path, writers, backoff, readCommittedSnapshot);
            outcomes.Add(outcome);
            Console.WriteLine($"done {scenario,-8} {(backoff ? "backoff" : "-"),-8} {path,-8} {writers,2} writers: {outcome.Accepted / Window.TotalSeconds,7:F0}/s");
        }

        Console.WriteLine();
        Console.WriteLine($"{engine}, READ_COMMITTED_SNAPSHOT {(readCommittedSnapshot ? "ON" : "OFF")}, {Window.TotalSeconds:F0}s per case after {Warmup.TotalSeconds:F0}s warmup, {FillerTags:N0} filler tags, {Environment.ProcessorCount} logical cores shared with the server.");
        Console.WriteLine("Append: one SaveAggregate, excluding the read. Command: first read to accepted save, retries and waits included.");
        Console.WriteLine();
        Console.WriteLine("| Scenario | Backoff | Writers | Path | Accepted/s | Refused % | Errors | Append p50 | Append p99 | Command p50 | Command p99 |");
        Console.WriteLine("|---|---|---:|---|---:|---:|---:|---:|---:|---:|---:|");

        foreach (var o in outcomes)
        {
            var attempts = o.Accepted + o.Refused;
            Console.WriteLine(
                $"| {o.Scenario} | {(o.Backoff ? "yes" : "no")} | {o.Writers} | {(o.Path == Path.EfCore ? "EF Core" : "Routine")} | " +
                $"{o.Accepted / Window.TotalSeconds:F0} | {(attempts == 0 ? 0 : 100.0 * o.Refused / attempts):F0} | {o.Errors} | " +
                $"{Percentile(o.AppendMs, 0.50):F2} | {Percentile(o.AppendMs, 0.99):F2} | " +
                $"{Percentile(o.CommandMs, 0.50):F2} | {Percentile(o.CommandMs, 0.99):F2} |");
        }

        foreach (var o in outcomes.Where(o => o.FirstError is not null))
        {
            Console.WriteLine($"\n{o.Scenario} {o.Path} {o.Writers}: first error: {o.FirstError}");
        }
    }

    private static async Task<Outcome> RunCase(StoreEngine engine, Scenario scenario, Path path, int writers,
        bool backoff, bool readCommittedSnapshot)
    {
        var connectionString = engine == StoreEngine.SqlServer
            ? SqlServerContainer.ForFreshDatabase("contention")
            : PostgreSqlContainer.ForFreshDatabase("contention");

        await using (var setup = RoutineContext(engine, connectionString))
        {
            await setup.Database.EnsureCreatedAsync();
            await setup.Database.EnsureDcbAppendRoutineAsync();
            await Seed(engine, setup, readCommittedSnapshot);
        }

        try
        {
            return await Measure(engine, connectionString, scenario, path, writers, backoff);
        }
        finally
        {
            ClearPools(engine);
            await using var cleanup = RoutineContext(engine, connectionString);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<Outcome> Measure(StoreEngine engine, string connectionString, Scenario scenario,
        Path path, int writers, bool backoff)
    {
        // Every writer's tag is one of the filler tags, spread evenly over them.
        var ids = Enumerable.Range(0, writers)
            .Select(writer => new DcbSeatsId(FillerTag(scenario == Scenario.Hot ? FillerTags / 2 : writer * (FillerTags / 16))))
            .ToArray();

        var accepted = 0;
        var refused = 0;
        var errors = 0;
        string? firstError = null;
        var appendLatencies = new ConcurrentBag<double>();
        var commandLatencies = new ConcurrentBag<double>();
        var acceptedOn = new ConcurrentBag<(string Boundary, long After)>();

        var start = new TaskCompletionSource();
        var measuring = false;
        var stop = false;

        var tasks = Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
        {
            var id = ids[writer];

            await using DbContext context = path == Path.Routine
                ? RoutineContext(engine, connectionString)
                : EntityFrameworkCoreContext(engine, connectionString);
            var store = (IDcbDbContext)context;

            await start.Task;
            var n = 0;
            var refusalsInRow = 0;
            var command = Stopwatch.StartNew();

            while (!Volatile.Read(ref stop))
            {
                bool ok;
                string? error = null;
                long seen = 0;
                var timer = new Stopwatch();

                try
                {
                    seen = await store.GetLatestPosition(id.Boundary);
                    var aggregate = new DcbSeats { Tags = id.Boundary.Tags, LatestPosition = seen, Version = 1 };
                    aggregate.Reserve($"seat-{writer}-{n++}", "customer", 19.99m);

                    timer.Start();
                    var result = await store.SaveAggregate(id, aggregate, new AppendCondition(id.Boundary, seen));
                    timer.Stop();

                    ok = result.IsSuccess;
                    if (!ok && result.Failure!.Type != "memoria/concurrency-conflict")
                    {
                        error = $"{result.Failure.Type}: {result.Failure.Description}";
                    }
                }
                catch (Exception exception)
                {
                    ok = false;
                    error = $"{exception.GetType().Name}: {exception.Message}";
                }

                if (Volatile.Read(ref measuring))
                {
                    appendLatencies.Add(timer.Elapsed.TotalMilliseconds);

                    if (error is not null)
                    {
                        Interlocked.Increment(ref errors);
                        Interlocked.CompareExchange(ref firstError, error, null);
                    }
                    else if (ok)
                    {
                        Interlocked.Increment(ref accepted);
                        acceptedOn.Add((id.Boundary.ToString(), seen));
                        commandLatencies.Add(command.Elapsed.TotalMilliseconds);
                    }
                    else
                    {
                        Interlocked.Increment(ref refused);
                    }
                }

                if (ok || error is not null)
                {
                    refusalsInRow = 0;
                    command.Restart();
                    continue;
                }

                refusalsInRow++;

                if (backoff)
                {
                    var ceiling = Math.Min(BackoffCap.TotalMilliseconds,
                        BackoffBase.TotalMilliseconds * Math.Pow(2, refusalsInRow - 1));
                    await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * ceiling));
                }
            }
        })).ToArray();

        start.SetResult();
        await Task.Delay(Warmup);
        Volatile.Write(ref measuring, true);
        await Task.Delay(Window);
        Volatile.Write(ref measuring, false);
        Volatile.Write(ref stop, true);
        await Task.WhenAll(tasks);

        var duplicates = acceptedOn.Count - acceptedOn.Distinct().Count();
        if (duplicates > 0)
        {
            throw new InvalidOperationException(
                $"{scenario} {path} {writers}: {duplicates} appends were accepted on a position another append also used.");
        }

        return new Outcome(scenario, path, writers, backoff, accepted, refused, errors, appendLatencies.ToArray(),
            commandLatencies.ToArray(), firstError);
    }

    private static string FillerTag(int index) => $"f{index:D6}";

    /// <summary>
    /// Fills the store with <see cref="FillerTags"/> tags, each on one event of its own and with its
    /// tag head, in a few set-based statements; then sets READ_COMMITTED_SNAPSHOT as asked, either
    /// way, since Entity Framework Core creates SQL Server databases with it on.
    /// </summary>
    private static async Task Seed(StoreEngine engine, DbContext setup, bool readCommittedSnapshot)
    {
        var data = Memoria.EventSourcing.DomainSerializer.Current.Serialize(
            new SeatReservedEvent("seat", "customer", 19.99m));

        if (engine == StoreEngine.SqlServer)
        {
            setup.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
            await setup.Database.ExecuteSqlRawAsync(
                $$"""
                INSERT [DcbEvents] ([EventType], [Data], [CreatedDate])
                SELECT TOP ({{FillerTags}}) N'BenchmarkSeatReserved:1', {0}, SYSDATETIMEOFFSET()
                FROM sys.all_objects a CROSS JOIN sys.all_objects b;

                INSERT [DcbEventTags] ([Position], [Tag])
                SELECT [Position], N'show:f' + RIGHT('000000' + CAST([Position] - 1 AS nvarchar(10)), 6) FROM [DcbEvents];

                INSERT [DcbTagHeads] ([Tag], [Token]) SELECT [Tag], NEWID() FROM [DcbEventTags];

                UPDATE STATISTICS [DcbEvents];
                UPDATE STATISTICS [DcbEventTags];
                UPDATE STATISTICS [DcbTagHeads];
                """, data);
            await setup.Database.ExecuteSqlRawAsync(
                $"ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT {(readCommittedSnapshot ? "ON" : "OFF")} WITH ROLLBACK IMMEDIATE");
        }
        else
        {
            await setup.Database.ExecuteSqlRawAsync(
                $$"""
                INSERT INTO "DcbEvents" ("EventType", "Data", "CreatedDate")
                SELECT 'BenchmarkSeatReserved:1', {0}::text, now() FROM generate_series(1, {{FillerTags}});

                INSERT INTO "DcbEventTags" ("Position", "Tag")
                SELECT "Position", 'show:f' || lpad(("Position" - 1)::text, 6, '0') FROM "DcbEvents";

                INSERT INTO "DcbTagHeads" ("Tag", "Token") SELECT "Tag", gen_random_uuid() FROM "DcbEventTags";

                ANALYZE;
                """, data);
        }
    }

    /// <summary>
    /// Prints what the server is; on a SQL Server container this run started, first gives tempdb
    /// eight data files, as a production server would have, where the image's setup leaves one.
    /// </summary>
    /// <remarks>
    /// A server named with <c>MEMORIA_BENCH_SQLSERVER</c> is somebody's own, so it is only described.
    /// </remarks>
    private static async Task DescribeServer(StoreEngine engine)
    {
        if (engine == StoreEngine.SqlServer)
        {
            await using var connection = new SqlConnection(SqlServerContainer.ConnectionString);
            await connection.OpenAsync();

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_BENCH_SQLSERVER")))
            {
                await using var tempdb = new SqlCommand(
                    """
                    DECLARE @files int = (SELECT COUNT(*) FROM tempdb.sys.database_files WHERE [type] = 0);
                    DECLARE @sql nvarchar(max);
                    WHILE @files < 8
                    BEGIN
                        SET @files += 1;
                        SET @sql = N'ALTER DATABASE tempdb ADD FILE (NAME = N''temp' + CAST(@files AS nvarchar(2)) +
                            N''', FILENAME = N''/var/opt/mssql/data/temp' + CAST(@files AS nvarchar(2)) +
                            N'.ndf'', SIZE = 64MB, FILEGROWTH = 64MB)';
                        EXEC (@sql);
                    END;
                    """, connection);
                await tempdb.ExecuteNonQueryAsync();
            }

            await using var command = new SqlCommand(
                """
                SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) + ' ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(100)),
                       (SELECT COUNT(*) FROM tempdb.sys.database_files WHERE [type] = 0),
                       (SELECT cpu_count FROM sys.dm_os_sys_info)
                """, connection);
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            Console.WriteLine($"SQL Server {reader.GetString(0)}, {reader.GetInt32(1)} tempdb data files, {reader.GetInt32(2)} CPUs visible to the server.");
        }
        else
        {
            await using var connection = new NpgsqlConnection(PostgreSqlContainer.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT version()", connection);
            Console.WriteLine(await command.ExecuteScalarAsync());
        }
    }

    private static void ClearPools(StoreEngine engine)
    {
        if (engine == StoreEngine.SqlServer)
        {
            SqlConnection.ClearAllPools();
        }
        else
        {
            NpgsqlConnection.ClearAllPools();
        }
    }

    private static BenchmarkDcbDbContext RoutineContext(StoreEngine engine, string connectionString) =>
        new(Options<DcbDbContext>(engine, connectionString), TimeProvider.System, StoreBenchmarkHarness.HttpContextAccessor());

    private static EntityFrameworkCorePathDcbDbContext EntityFrameworkCoreContext(StoreEngine engine, string connectionString) =>
        new(Options<EntityFrameworkCorePathDcbDbContext>(engine, connectionString), TimeProvider.System,
            StoreBenchmarkHarness.HttpContextAccessor());

    private static DbContextOptions<T> Options<T>(StoreEngine engine, string connectionString) where T : DbContext =>
        engine == StoreEngine.SqlServer
            ? new DbContextOptionsBuilder<T>().UseSqlServer(connectionString).Options
            : new DbContextOptionsBuilder<T>().UseNpgsql(connectionString).Options;

    private static double Percentile(double[] values, double percentile)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Min(sorted.Length - 1, Math.Ceiling(percentile * sorted.Length) - 1)];
    }
}

/// <summary>
/// A DCB context that implements <see cref="IDcbDbContext"/> without deriving from
/// <see cref="DcbDbContext"/>, so the store appends for it through Entity Framework Core on every
/// engine. Maps the tables exactly as <see cref="DcbDbContext"/> does, tag collation and audit
/// stamps included.
/// </summary>
public sealed class EntityFrameworkCorePathDcbDbContext(
    DbContextOptions<EntityFrameworkCorePathDcbDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DbContext(options), IDcbDbContext
{
    public DbSet<DcbEventEntity> DcbEvents { get; set; } = null!;

    public DbSet<DcbEventTagEntity> DcbEventTags { get; set; } = null!;

    public DbSet<DcbTagHeadEntity> DcbTagHeads { get; set; } = null!;

    public DbSet<DcbSnapshotEntity> DcbSnapshots { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.AddInterceptors(new AuditInterceptor(timeProvider, httpContextAccessor));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var tagCollation = Database.ProviderName switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => "SQL_Latin1_General_CP1_CS_AS",
            "Npgsql.EntityFrameworkCore.PostgreSQL" => "C",
            _ => null
        };

        modelBuilder.ApplyConfiguration(new DcbEventEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DcbEventTagEntityConfiguration(tagCollation));
        modelBuilder.ApplyConfiguration(new DcbTagHeadEntityConfiguration(tagCollation));
        modelBuilder.ApplyConfiguration(new DcbSnapshotEntityConfiguration());
    }
}
