using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;

namespace Memoria.Benchmarks.Store;

/// <summary>
/// Counts the commands each store sends per operation, and prints them side by side.
/// </summary>
/// <remarks>
/// <para>
/// This is not a BenchmarkDotNet benchmark and needs no statistics: the count is exact, the same on
/// every run, and — unlike a timing on SQLite — the same on every engine. It is the number that
/// predicts how each store behaves where a round trip is a network hop rather than a function call,
/// which is the one thing the timings here cannot show.
/// </para>
/// <para>
/// Run it with <c>dotnet run -c Release -- --round-trips</c>.
/// </para>
/// </remarks>
public static class RoundTripReport
{
    public static async Task Run(int events = 100, bool verbose = false, StoreEngine engine = StoreEngine.Sqlite)
    {
        await using var harness = new StoreBenchmarkHarness(engine);

        await harness.Seed(events);
        await harness.WriteSnapshots();

        var rows = new List<Row>
        {
            await Measure(harness, $"GetEvents({events})",
                () => harness.Streamed.GetEvents(StoreBenchmarkHarness.StreamId),
                () => harness.Dcb.GetEvents(StoreBenchmarkHarness.DcbId.Boundary)),

            await Measure(harness, "GetAggregate (snapshot only)",
                () => harness.Streamed.GetAggregate(StoreBenchmarkHarness.StreamId,
                    StoreBenchmarkHarness.StreamedId, ReadMode.SnapshotOnly),
                () => harness.Dcb.GetAggregate(StoreBenchmarkHarness.DcbId, ReadMode.SnapshotOnly)),

            await Measure(harness, "GetAggregate (folded)",
                () => harness.Streamed.GetInMemoryAggregate(StoreBenchmarkHarness.StreamId,
                    StoreBenchmarkHarness.StreamedId),
                () => harness.Dcb.GetInMemoryAggregate(StoreBenchmarkHarness.DcbId)),

            await Measure(harness, "SaveAggregate (guarded)",
                () => SaveStreamed(harness, events),
                () => SaveDcb(harness))
        };

        if (verbose)
        {
            await PrintCommands(harness, events);
        }

        Print(rows, events, engine);
    }

    private static async Task<Row> Measure(StoreBenchmarkHarness harness, string operation,
        Func<Task> streamed, Func<Task> dcb)
    {
        harness.Commands.Clear();
        await streamed();
        var streamedCommands = harness.Commands.Commands.Count;

        harness.Commands.Clear();
        await dcb();
        var dcbCommands = harness.Commands.Commands.Count;

        return new Row(operation, streamedCommands, dcbCommands);
    }

    private static async Task SaveStreamed(StoreBenchmarkHarness harness, int events)
    {
        var aggregate = new StreamedSeats { LatestEventSequence = events, Version = events };
        aggregate.Reserve("seat-extra", "customer", 19.99m);

        await harness.Streamed.SaveAggregate(StoreBenchmarkHarness.StreamId,
            StoreBenchmarkHarness.StreamedId, aggregate, expectedEventSequence: events);
    }

    private static async Task SaveDcb(StoreBenchmarkHarness harness)
    {
        var boundary = StoreBenchmarkHarness.DcbId.Boundary;
        var position = (await harness.Dcb.GetLatestPosition(boundary)).Value;

        // Counted from here, so the position read the append is conditioned on is not charged to it.
        harness.Commands.Clear();

        var aggregate = new DcbSeats { Tags = boundary.Tags, LatestPosition = position, Version = 1 };
        aggregate.Reserve("seat-extra", "customer", 19.99m);

        await harness.Dcb.SaveAggregate(StoreBenchmarkHarness.DcbId, aggregate,
            new AppendCondition(boundary, position));
    }

    private static void Print(List<Row> rows, int events, StoreEngine engine)
    {
        Console.WriteLine();
        Console.WriteLine($"Database commands per operation, over {events} events, on {engine}.");

        Console.WriteLine();

        var width = rows.Max(row => row.Operation.Length);

        Console.WriteLine($"| {"Operation".PadRight(width)} | Streams | DCB |");
        Console.WriteLine($"|{new string('-', width + 2)}|---------|-----|");

        foreach (var row in rows)
        {
            Console.WriteLine($"| {row.Operation.PadRight(width)} | " +
                              $"{row.Streamed.ToString().PadLeft(7)} | {row.Dcb.ToString().PadLeft(3)} |");
        }

        Console.WriteLine();
    }


    /// <summary>
    /// Prints the statements behind the SaveAggregate row, because "three against eight" only means
    /// something once you can see which eight.
    /// </summary>
    private static async Task PrintCommands(StoreBenchmarkHarness harness, int events)
    {
        harness.Commands.Clear();
        await SaveStreamed(harness, events + 1);
        Console.WriteLine();
        Console.WriteLine("SaveAggregate (streams):");
        foreach (var command in harness.Commands.Commands)
        {
            Console.WriteLine($"  {Flatten(command)}");
        }

        harness.Commands.Clear();
        await SaveDcb(harness);
        Console.WriteLine();
        Console.WriteLine("SaveAggregate (DCB):");
        foreach (var command in harness.Commands.Commands)
        {
            Console.WriteLine($"  {Flatten(command)}");
        }
    }

    private static string Flatten(string command)
    {
        var single = string.Join(' ', command.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()));

        return single.Length <= 110 ? single : single[..110] + "...";
    }

    private record Row(string Operation, int Streamed, int Dcb);
}
