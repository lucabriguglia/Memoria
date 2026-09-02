using BenchmarkDotNet.Attributes;
using Memoria.EventSourcing;

namespace Memoria.Benchmarks.Store;

/// <summary>
/// What reading costs in the DCB store against the streamed store, over the same events.
/// </summary>
/// <remarks>
/// <para>
/// The models, the events and the payloads are identical on both sides — only the identity and the
/// boundary differ — so what is left is the store.
/// </para>
/// <para>
/// The two shapes being compared: a stream is a contiguous range on one indexed column, so the
/// streamed store filters <c>WHERE StreamId = @id</c>. A boundary is a set of tags on a separate
/// table, so the DCB store filters with an <c>EXISTS</c> against <c>DcbEventTags</c>. An index seek
/// against an index seek plus a semi-join, over the same rows. <see cref="Events"/> is varied to show
/// whether the gap is a constant or grows with the set.
/// </para>
/// <para>
/// Relative costs on one engine, not production latency. Read them next to
/// <see cref="RoundTripReport"/>.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 5, iterationCount: 15)]
public class StoreReadBenchmarks
{
    private StoreBenchmarkHarness _harness = null!;

    /// <summary>How many events sit inside the stream and inside the boundary.</summary>
    /// <summary>The database to run against. SQLite hides the cost of a round trip; SQL Server does not.</summary>
    [Params(StoreEngine.Sqlite, StoreEngine.SqlServer, StoreEngine.PostgreSql)]
    public StoreEngine Engine { get; set; }

    [Params(10, 100, 1000)]
    public int Events { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _harness = new StoreBenchmarkHarness(Engine);
        _harness.Seed(Events).GetAwaiter().GetResult();
        _harness.WriteSnapshots().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _harness.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Benchmark(Baseline = true, Description = "GetEvents (streams)")]
    public async Task<int> GetEventsStreamed() =>
        (await _harness.Streamed.GetEvents(StoreBenchmarkHarness.StreamId)).Value!.Count;

    [Benchmark(Description = "GetEvents (DCB)")]
    public async Task<int> GetEventsDcb() =>
        (await _harness.Dcb.GetEvents(StoreBenchmarkHarness.DcbId.Boundary)).Value!.Count;

    /// <summary>
    /// One snapshot row, deserialized, no events folded. The same shape in both stores, so it should
    /// show almost no difference — a control on the folded pair below.
    /// </summary>
    [Benchmark(Description = "GetAggregate, snapshot only (streams)")]
    public async Task<int> GetAggregateSnapshotStreamed() =>
        (await _harness.Streamed.GetAggregate(StoreBenchmarkHarness.StreamId,
            StoreBenchmarkHarness.StreamedId, ReadMode.SnapshotOnly)).Value!.Reserved;

    [Benchmark(Description = "GetAggregate, snapshot only (DCB)")]
    public async Task<int> GetAggregateSnapshotDcb() =>
        (await _harness.Dcb.GetAggregate(StoreBenchmarkHarness.DcbId, ReadMode.SnapshotOnly)).Value!.Reserved;

    /// <summary>
    /// The cold path, through the store's own fold: every event in the stream or the boundary is read
    /// and applied. <c>GetInMemoryAggregate</c> rather than <c>GetAggregate</c> so nothing is written,
    /// which keeps the benchmark repeatable and keeps a snapshot write out of a read measurement.
    /// </summary>
    [Benchmark(Description = "GetAggregate, folded (streams)")]
    public async Task<int> GetAggregateFoldedStreamed() =>
        (await _harness.Streamed.GetInMemoryAggregate(StoreBenchmarkHarness.StreamId,
            StoreBenchmarkHarness.StreamedId)).Value!.Reserved;

    [Benchmark(Description = "GetAggregate, folded (DCB)")]
    public async Task<int> GetAggregateFoldedDcb() =>
        (await _harness.Dcb.GetInMemoryAggregate(StoreBenchmarkHarness.DcbId)).Value!.Reserved;
}
