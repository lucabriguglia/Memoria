using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Memoria.EventSourcing.Dcb;

namespace Memoria.Benchmarks.Store;

/// <summary>
/// What appending costs in the DCB store against the streamed store, with both guarded.
/// </summary>
/// <remarks>
/// <para>
/// Both sides append one event and rewrite the snapshot, and both refuse if their boundary moved:
/// the streamed store on the expected sequence, the DCB store on the boundary's position and the tag
/// heads. Comparing a guarded append against an unguarded one would flatter DCB, because the guard is
/// most of what it does.
/// </para>
/// <para>
/// <strong>This is the measurement SQLite is least able to make.</strong> The streamed append is three
/// round trips; the DCB append is eight. In this process each one costs almost nothing, so the gap here
/// is a floor. Against a networked engine every one of those five extra commands is a real
/// millisecond, and <see cref="RoundTripReport"/> is the number to reason with.
/// </para>
/// <para>
/// <see cref="RunStrategy.Monitoring"/> with one invocation per iteration, because every append has a
/// side effect: it moves the sequence and the position that the next one is guarded against. The
/// benchmark carries that state forward so each append is a real, valid, guarded write rather than a
/// concurrency conflict measured over and over.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, invocationCount: 1, warmupCount: 10, iterationCount: 200)]
public class StoreWriteBenchmarks
{
    private StoreBenchmarkHarness _harness = null!;
    private int _expectedSequence;
    private long _expectedPosition;
    private int _seat;

    /// <summary>How many events the stream and the boundary already hold before the append.</summary>
    [Params(10, 100, 1000)]
    public int Events { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _harness = new StoreBenchmarkHarness();
        _harness.Seed(Events).GetAwaiter().GetResult();
        _harness.WriteSnapshots().GetAwaiter().GetResult();

        _expectedSequence = Events;
        _expectedPosition = _harness.Dcb.GetLatestPosition(StoreBenchmarkHarness.DcbId.Boundary)
            .GetAwaiter().GetResult().Value;
        _seat = Events;
    }

    [GlobalCleanup]
    public void Cleanup() => _harness.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Benchmark(Baseline = true, Description = "SaveAggregate (streams)")]
    public async Task SaveAggregateStreamed()
    {
        var aggregate = new StreamedSeats { LatestEventSequence = _expectedSequence, Version = _expectedSequence };
        aggregate.Reserve($"seat-{_seat++}", "customer", 19.99m);

        var result = await _harness.Streamed.SaveAggregate(StoreBenchmarkHarness.StreamId,
            StoreBenchmarkHarness.StreamedId, aggregate, _expectedSequence);

        Guard(result.IsSuccess, result.Failure?.Type);

        // The next append is guarded against where this one left the stream.
        _expectedSequence = aggregate.LatestEventSequence;
    }

    [Benchmark(Description = "SaveAggregate (DCB)")]
    public async Task SaveAggregateDcb()
    {
        var aggregate = new DcbSeats
        {
            Tags = StoreBenchmarkHarness.DcbId.Boundary.Tags,
            LatestPosition = _expectedPosition,
            Version = Events
        };

        aggregate.Reserve($"seat-{_seat++}", "customer", 19.99m);

        var result = await _harness.Dcb.SaveAggregate(StoreBenchmarkHarness.DcbId, aggregate,
            new AppendCondition(StoreBenchmarkHarness.DcbId.Boundary, _expectedPosition));

        Guard(result.IsSuccess, result.Failure?.Type);

        // SaveAggregate stamps the aggregate with the position it wrote, so the next append is
        // guarded against it without another read.
        _expectedPosition = aggregate.LatestPosition;
    }

    /// <summary>
    /// A refused append is a different, much cheaper code path. Measuring it by accident would make
    /// the DCB column look good for the worst possible reason, so the benchmark fails loudly instead.
    /// </summary>
    private static void Guard(bool succeeded, string? failureType)
    {
        if (!succeeded)
        {
            throw new InvalidOperationException(
                $"The append was refused ({failureType}), so this iteration measured the failure path.");
        }
    }
}
