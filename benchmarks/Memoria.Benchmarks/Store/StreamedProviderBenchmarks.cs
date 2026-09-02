using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Memoria.EventSourcing;

namespace Memoria.Benchmarks.Store;

/// <summary>
/// What the streamed store costs on each provider it ships with, over the same events and the same
/// model.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="StoreReadBenchmarks"/> and <see cref="StoreWriteBenchmarks"/> because it
/// answers a different question. Those compare two consistency models on one engine; this compares
/// one consistency model across four providers, and Cosmos DB can only appear here — there is no DCB
/// store for it, and the reason is structural rather than unfinished work. An append has to condition
/// on a tag query and write atomically, and a transactional batch is scoped to one logical partition
/// while a boundary is not. See <c>docs/concepts/providers.md</c>.
/// </para>
/// <para>
/// There are no ratios in this report. Each provider is its own row and the comparison is read down
/// the Mean column, because none of them is the baseline the others are variations on — a
/// containerised SQL Server, a local Cosmos emulator and an in-process SQLite are not four ways of
/// doing one thing, they are four different deployments.
/// </para>
/// <para>
/// The Cosmos cases need the emulator on <see cref="CosmosEmulator.Endpoint"/>, matching the Cosmos
/// test project, which is a local-only gate for the same reason: no CI job provides one. Filter them
/// out if it is not running.
/// </para>
/// <para>
/// <see cref="RunStrategy.Monitoring"/> with one invocation per iteration, because the append moves
/// the sequence the next one is guarded against; the benchmark carries that forward so every
/// iteration is a real write rather than a refusal.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, invocationCount: 1, warmupCount: 10, iterationCount: 100)]
public class StreamedProviderBenchmarks
{
    private StoreBenchmarkHarness _harness = null!;
    private int _expectedSequence;
    private int _seat;

    /// <summary>The provider under test. Cosmos DB is here and not in the DCB comparison.</summary>
    [Params(StoreEngine.Sqlite, StoreEngine.SqlServer, StoreEngine.PostgreSql, StoreEngine.Cosmos)]
    public StoreEngine Provider { get; set; }

    /// <summary>How many events sit in the stream.</summary>
    [Params(100)]
    public int Events { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _harness = new StoreBenchmarkHarness(Provider);
        _harness.Seed(Events).GetAwaiter().GetResult();
        _harness.WriteSnapshots().GetAwaiter().GetResult();

        _expectedSequence = Events;
        _seat = Events;
    }

    [GlobalCleanup]
    public void Cleanup() => _harness.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Benchmark(Description = "GetEvents")]
    public async Task<int> GetEvents() =>
        (await _harness.Streamed.GetEvents(StoreBenchmarkHarness.StreamId)).Value!.Count;

    [Benchmark(Description = "GetAggregate, snapshot only")]
    public async Task<int> GetAggregateSnapshot() =>
        (await _harness.Streamed.GetAggregate(StoreBenchmarkHarness.StreamId,
            StoreBenchmarkHarness.StreamedId, ReadMode.SnapshotOnly)).Value!.Reserved;

    [Benchmark(Description = "GetAggregate, folded")]
    public async Task<int> GetAggregateFolded() =>
        (await _harness.Streamed.GetInMemoryAggregate(StoreBenchmarkHarness.StreamId,
            StoreBenchmarkHarness.StreamedId)).Value!.Reserved;

    [Benchmark(Description = "SaveAggregate")]
    public async Task SaveAggregate()
    {
        var aggregate = new StreamedSeats { LatestEventSequence = _expectedSequence, Version = _expectedSequence };
        aggregate.Reserve($"seat-{_seat++}", "customer", 19.99m);

        var result = await _harness.Streamed.SaveAggregate(StoreBenchmarkHarness.StreamId,
            StoreBenchmarkHarness.StreamedId, aggregate, _expectedSequence);

        if (result.IsNotSuccess)
        {
            throw new InvalidOperationException(
                $"The append was refused ({result.Failure!.Type}), so this iteration measured the failure path.");
        }

        _expectedSequence = aggregate.LatestEventSequence;
    }
}
