using System.Collections.Concurrent;
using System.Diagnostics;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;



namespace Memoria.Benchmarks.Store;

/// <summary>
/// SPIKE: concurrent writers appending through the store's EF Core path and through the single-call
/// function, on PostgreSQL. Run with <c>--contention</c>.
/// </summary>
/// <remarks>
/// <para>
/// Not a BenchmarkDotNet benchmark: the question is throughput across writers, which it does not
/// measure. Each case gets a fresh database, warms up, then runs every writer flat out for a fixed
/// window. A writer reads its boundary's latest position, appends one event conditioned on it, and on
/// refusal reads again and retries — the loop a real DCB command handler runs.
/// </para>
/// <para>
/// The read is the same raw query on both paths, so the difference measured is the append's. Two
/// scenarios: <b>hot</b>, every writer on one boundary, where at most one append per position can
/// win; and <b>disjoint</b>, every writer on its own boundary, where nothing conflicts logically and
/// what is measured is how well appends run side by side.
/// </para>
/// <para>
/// The database runs on the same machine as the writers, so they compete for the same cores.
/// </para>
/// </remarks>
public static class ContentionSpike
{
    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

    private enum Path { EfCore, Function }

    private enum Scenario { Hot, Disjoint }

    private sealed record Outcome(Scenario Scenario, Path Path, int Writers, bool Backoff, int Accepted,
        int Refused, int Errors, double[] AppendMs, double[] CommandMs, double[] ReadMs, string? FirstError);

    /// <summary>The first wait after a refusal; each further refusal in a row doubles the ceiling.</summary>
    private static readonly TimeSpan BackoffBase = TimeSpan.FromMilliseconds(1);

    private static readonly TimeSpan BackoffCap = TimeSpan.FromMilliseconds(20);

    [System.Runtime.InteropServices.DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint milliseconds);

    public static async Task Run(StoreEngine engine, string? only = null, bool readCommittedSnapshot = false)
    {
        // Windows ticks timers every 15.6ms by default, far coarser than the waits a backoff needs.
        if (OperatingSystem.IsWindows())
        {
            timeBeginPeriod(1);
        }

        await ReportTimerResolution();

        int[] writerCounts = [1, 4, 16];
        var outcomes = new List<Outcome>();

        // Backoff only changes anything where appends are refused, so disjoint runs without it.
        var cases =
            from scenario in new[] { Scenario.Hot, Scenario.Disjoint }
            from backoff in scenario == Scenario.Hot ? new[] { false, true } : [false]
            from writers in writerCounts
            from path in new[] { Path.EfCore, Path.Function }
            where only is null || scenario.ToString() == only
            select (scenario, backoff, writers, path);

        foreach (var (scenario, backoff, writers, path) in cases)
        {
            var outcome = await RunCase(engine, scenario, path, writers, backoff, readCommittedSnapshot);
            outcomes.Add(outcome);
            Console.WriteLine($"done {scenario,-8} {(backoff ? "backoff" : "-"),-8} {path,-8} {writers,2} writers: {outcome.Accepted / Window.TotalSeconds,7:F0}/s");
        }

        Console.WriteLine();
        Console.WriteLine($"{engine}{(readCommittedSnapshot ? " (READ_COMMITTED_SNAPSHOT ON)" : "")}, {Window.TotalSeconds:F0}s per case after {Warmup.TotalSeconds:F0}s warmup.");
        Console.WriteLine("Attempt: one append, excluding the read. Command: first read to accepted append, retries and waits included.");
        Console.WriteLine($"Backoff: full jitter, uniform in [0, min({BackoffCap.TotalMilliseconds}ms, {BackoffBase.TotalMilliseconds}ms x 2^refusals in a row)).");
        Console.WriteLine();
        Console.WriteLine("| Scenario | Backoff | Writers | Path | Accepted/s | Refused % | Errors | Attempt p50 | Attempt p99 | Command p50 | Command p95 | Command p99 | Read p50 | Read p99 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var o in outcomes)
        {
            var attempts = o.Accepted + o.Refused;
            Console.WriteLine(
                $"| {o.Scenario} | {(o.Backoff ? "yes" : "no")} | {o.Writers} | {(o.Path == Path.EfCore ? "EF Core" : "Function")} | " +
                $"{o.Accepted / Window.TotalSeconds:F0} | " +
                $"{(attempts == 0 ? 0 : 100.0 * o.Refused / attempts):F0}% | {o.Errors} | " +
                $"{Percentile(o.AppendMs, 0.50):F2} | {Percentile(o.AppendMs, 0.99):F2} | " +
                $"{Percentile(o.CommandMs, 0.50):F2} | {Percentile(o.CommandMs, 0.95):F2} | {Percentile(o.CommandMs, 0.99):F2} | {Percentile(o.ReadMs, 0.50):F2} | {Percentile(o.ReadMs, 0.99):F2} |");
        }

        foreach (var o in outcomes.Where(o => o.FirstError is not null))
        {
            Console.WriteLine($"\n{o.Scenario} {o.Path} {o.Writers}: first error: {o.FirstError}");
        }
    }

    private static async Task<Outcome> RunCase(StoreEngine engine, Scenario scenario, Path path, int writers, bool backoff,
        bool readCommittedSnapshot)
    {
        await using var harness = new StoreBenchmarkHarness(engine);
        await harness.Seed(100);
        await harness.WriteSnapshots();

        var connectionString = harness.DcbConnectionString;

        if (readCommittedSnapshot && engine == StoreEngine.SqlServer)
        {
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var alter = new Microsoft.Data.SqlClient.SqlCommand(
                "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE", connection);
            await alter.ExecuteNonQueryAsync();
        }

        await using var spike = AppendSpike.Create(engine, connectionString);
        await spike.Install();

        var ids = Enumerable.Range(0, writers)
            .Select(writer => scenario == Scenario.Hot ? StoreBenchmarkHarness.DcbId : new DcbSeatsId($"w{writer}"))
            .ToArray();

        var accepted = 0;
        var refused = 0;
        var errors = 0;
        string? firstError = null;
        string? efLog = null;
        var latencies = new ConcurrentBag<double>();
        var commandLatencies = new ConcurrentBag<double>();
        var readLatencies = new ConcurrentBag<double>();
        // Every accepted append, by the position it was conditioned on. Two on the same position in
        // the same boundary would mean the guard let a lost update through.
        var acceptedOn = new ConcurrentBag<(string Boundary, long After)>();

        var start = new TaskCompletionSource();
        var measuring = false;
        var stop = false;

        var tasks = Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
        {
            var id = ids[writer];

            await using var context = path == Path.EfCore
                ? spike.CreateContext(message => Interlocked.CompareExchange(ref efLog, message, null))
                : null;
            var store = context is null ? null : new EntityFrameworkCoreDcbDomainService(context);

            await start.Task;
            var n = 0;
            var refusalsInRow = 0;
            var command = Stopwatch.StartNew();

            while (!Volatile.Read(ref stop))
            {
                var readTimer = Stopwatch.StartNew();
                var seen = await spike.ReadLatest(id.Boundary);
                var readMs = readTimer.Elapsed.TotalMilliseconds;

                var aggregate = new DcbSeats { Tags = id.Boundary.Tags, LatestPosition = seen, Version = 1 };
                aggregate.Reserve($"seat-{writer}-{n++}", "customer", 19.99m);
                var condition = new AppendCondition(id.Boundary, seen);

                var timer = Stopwatch.StartNew();
                bool ok;
                string? error = null;

                try
                {
                    if (store is not null)
                    {
                        var result = await store.SaveAggregate(id, aggregate, condition);
                        ok = result.IsSuccess;
                        if (!ok && result.Failure!.Type != "memoria/concurrency-conflict")
                        {
                            error = $"{result.Failure.Type}: {result.Failure.Description}";
                        }
                    }
                    else
                    {
                        (ok, _) = await spike.SaveAggregate(id, aggregate, condition, "benchmark");
                    }
                }
                catch (Exception exception)
                {
                    ok = false;
                    error = $"{exception.GetType().Name}: {exception.Message}";
                }

                timer.Stop();

                if (Volatile.Read(ref measuring))
                {
                    latencies.Add(timer.Elapsed.TotalMilliseconds);
                    readLatencies.Add(readMs);

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
                    // The next command starts here, whether this one was accepted or gave up on an error.
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

        if (efLog is not null)
        {
            Console.WriteLine($"{scenario} {path} {writers}: EF logged: {efLog}");
        }

        var duplicates = acceptedOn.Count - acceptedOn.Distinct().Count();
        if (duplicates > 0)
        {
            throw new InvalidOperationException(
                $"{scenario} {path} {writers}: {duplicates} appends were accepted on a position another append also used.");
        }

        return new Outcome(scenario, path, writers, backoff, accepted, refused, errors, latencies.ToArray(),
            commandLatencies.ToArray(), readLatencies.ToArray(), firstError);
    }

    /// <summary>Prints what a short delay actually costs, so the backoff's waits can be trusted.</summary>
    private static async Task ReportTimerResolution()
    {
        var timer = Stopwatch.StartNew();
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(1);
        }

        Console.WriteLine($"Task.Delay(1) takes {timer.Elapsed.TotalMilliseconds / 50:F2}ms on average here.");
    }

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
