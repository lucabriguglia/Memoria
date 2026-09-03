# Benchmarks

BenchmarkDotNet projects backing decisions about Memoria's internals. Run everything, or filter:

```bash
dotnet run -c Release --project benchmarks/Memoria.Benchmarks                              # pick from a menu
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --filter "*StoreRead*"
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --round-trips             # not a benchmark, see below
```

## Streams against DCB

`Store/StoreReadBenchmarks` and `Store/StoreWriteBenchmarks` put the two consistency models side by
side over `GetEvents`, `GetAggregate` and `SaveAggregate`. The event type, the model state, the fold
and the payloads are identical on both sides — only the identity and the boundary differ — so what is
left is the store.

The streams-against-DCB benchmarks run on **the three engines the DCB store targets**, as a BenchmarkDotNet parameter:
in-memory SQLite, which needs nothing installed, and SQL Server 2022 and PostgreSQL 15 in containers
via Testcontainers. Each container starts once per benchmark process, lazily, on the first case that
needs it; Docker must be running or those cases fail with a message saying so. Cosmos DB is compared
separately, further down, because it hosts no DCB store.

### Round trips

Streams vs DCB, per operation:

| Operation                    | SQLite | SQL Server | PostgreSQL |
|------------------------------|--------|------------|------------|
| GetEvents(100)               | 1 vs 1 | 1 vs 1     | 1 vs 1     |
| GetAggregate (snapshot only) | 1 vs 1 | 1 vs 1     | 1 vs 1     |
| GetAggregate (folded)        | 1 vs 1 | 1 vs 1     | 1 vs 1     |
| SaveAggregate (guarded)      | 3 vs 8 | 2 vs 7     | 2 vs 7     |

```bash
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --round-trips --verbose
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --round-trips --sqlserver --verbose
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --round-trips --postgres --verbose
```

This is not a BenchmarkDotNet benchmark and needs no statistics: the count is exact and identical on
every run. **Reads cost DCB nothing in round trips. Appends cost it five more**, and `--verbose`
prints the statements so you can see which five.

The absolute counts drop by one on the real engines because Entity Framework Core batches more
aggressively there, folding the tag head `UPDATE` into the event `INSERT` and the streamed snapshot
`UPDATE` into its `INSERT`. **The gap is five on all three.**

### What the engines showed

Reads are a wash. On SQLite, which runs in process and is much the quietest of the three, DCB comes
in between 0.95x and 1.29x of streams, widest at ten events where a boundary's fixed cost has nothing
to amortise against and the entire read is tens of microseconds.

**Use `GetAggregate, snapshot only` to calibrate before believing any of the others.** It is the same
operation in both stores — one row fetched by primary key, no boundary anywhere near it — so its
ratio measures the harness rather than the store, and it ought to be exactly 1.00. It came out at
0.83–1.16 on PostgreSQL and 0.93–0.99 on SQL Server in the same run that produced everything else
here. A container difference inside that band is not a finding.

One result does survive that test, because it repeats on all three engines and in the right
direction: **`GetEvents` over a thousand events is faster under DCB** — 0.95x on SQLite, 0.78x on SQL
Server, 0.84x on PostgreSQL — while allocating about 3% less. Both stores fetch the same rows, and
DCB reaches them through two key seeks rather than one seek over a wider row.

| `SaveAggregate`, median | Streams | DCB      | Ratio |
|-------------------------|---------|----------|-------|
| SQLite, in memory       | ~1.3 ms | ~2.6 ms  | ~2.1x |
| SQL Server, container   | ~8.2 ms | ~16.5 ms | ~2.0x |
| PostgreSQL, container   | ~4.6 ms | ~13.0 ms | ~2.8x |

Compare the ratios, never the milliseconds, when re-running. Two runs of the *same commit* on this
machine ten minutes apart put the SQL Server streamed append at 13.8 ms and then 18.9 ms — a 37%
spread with nothing changed. The ratios were stable across the same pair. If you need to know whether
a change moved the store, run the old and new commits alternately in one sitting and compare within
each pass; a table like this one from two different sittings cannot tell you.

The original reason for running more than one engine was a prediction: that SQLite would understate
the DCB append cost, because five extra round trips are nearly free in process. **It was wrong.** The
absolute cost rises several-fold on a real engine and the ratio stays in the same band, because a
streamed append pays transaction and commit costs that scale the same way.

The ratio does not track engine speed either. PostgreSQL has the fastest streamed append and the
largest DCB ratio, because its baseline is cheaper while five extra commands cost much the same.

What no local container can answer is distance: they run on this machine, so a round trip is loopback
rather than a network hop. Keep the round-trip table for that case.

### Why a boundary is an EXISTS and not an IN

Both translate a boundary correctly and both are semi-joins, so neither can return an event twice.
They do not cost the same, and which one wins depends on how big the boundary is.

Selecting the events with `WHERE Position IN (SELECT Position FROM DcbEventTags WHERE …)` was
measured against the `EXISTS` the store uses now, alternating commits in one sitting on SQLite:

| `GetAggregate, folded`, DCB ÷ streams | 10 events | 100 events | 1000 events |
|---------------------------------------|-----------|------------|-------------|
| `EXISTS`                              | 1.05      | 1.02       | 1.00        |
| `IN (subquery)`                       | **1.32**  | 0.95       | **0.89**    |

The subquery has a setup cost that a ten-event boundary cannot absorb and a plan that pays off once
there are a thousand rows to find. `EXISTS` is the default because a boundary is the consistency
boundary of one decision and is normally small; a thousand-event boundary read in full is unusual,
and is what snapshots exist for. If your boundaries really are that large, this is the knob.

The two baseline passes bracketing that measurement agreed to 1–2%, which is what makes a 1.05
against 1.32 readable at all. Do not try to reproduce it on a container.

### Why the harness runs ANALYZE on PostgreSQL

Because without it the PostgreSQL numbers were fiction. A fresh database, a thousand inserted rows and
an immediate query gives the planner no statistics, and the DCB read is unusually sensitive to that:
both its predicates are `= ANY(@array)`, whose selectivity PostgreSQL cannot estimate from a
parameter, so it assumes one row on each side, picks a nested loop semi join and applies the position
match as a filter rather than an index condition.

That measured 77 ms against 3.3 ms for streams — a 21x "regression" that was entirely the missing
statistics. One `ANALYZE` takes it to 3 ms. The harness now runs one after seeding, so the benchmarks
measure the store rather than autovacuum lag. SQL Server needs no equivalent: it creates missing
statistics itself on first use, which is why it never showed this.

### Reading the timings

Report medians rather than means for the write benchmarks. Commit spikes skew the mean badly enough
that the `RatioSD` column exceeds 0.8 on SQLite; the medians are stable across runs.

The write benchmarks use `RunStrategy.Monitoring` with one invocation per iteration, because every
append has a side effect: it moves the sequence and the position the next append is guarded against.
The benchmark carries that state forward so each iteration is a real guarded write, and throws if one
is ever refused — a refused append takes a much cheaper path, and measuring it by accident would
flatter DCB for the worst possible reason.

Both sides are guarded. Comparing a guarded streamed append against an unguarded DCB append would be
meaningless, because the guard is most of what a DCB append does.

## The streamed store across providers

`Store/StreamedProviderBenchmarks` answers a different question from the two above: not which
consistency model costs more on one engine, but what one consistency model costs on each provider it
ships with.

**Cosmos DB appears only here.** There is no DCB store for it, and the reason is structural rather
than unfinished work: an append has to condition on a tag query and write atomically, and a
transactional batch is scoped to one logical partition while a boundary is not. See
[Providers](../docs/concepts/providers.md).

Medians over 100 events:

| Operation                   | SQLite  | SQL Server | PostgreSQL | Cosmos  |
|-----------------------------|---------|------------|------------|---------|
| GetEvents                   | 1.14 ms | 3.65 ms    | 2.85 ms    | 5.73 ms |
| GetAggregate, snapshot only | 0.36 ms | 2.35 ms    | 2.00 ms    | 0.99 ms |
| GetAggregate, folded        | 1.33 ms | 3.81 ms    | 3.06 ms    | 5.78 ms |
| SaveAggregate               | 1.08 ms | 7.17 ms    | 4.30 ms    | 9.46 ms |

Cosmos wins the point read and loses the query, which is what it is built to do. Reading a snapshot
is a `ReadItemAsync` by id and partition key, and at 0.99 ms it beats both relational engines; every
operation that has to *query* — `GetEvents`, and the fold built on it — is the slowest of the four. It
also allocates roughly twice as much (730 KB against ~390 KB), which is the SDK's serialization
rather than the store's.

There are no ratios in this report. Each provider is its own row, because none of them is a baseline
the others vary from: an in-process SQLite, two containers and a local emulator are four different
deployments, not four ways of doing one thing.

The Cosmos cases need the emulator running on `https://localhost:8081`, the same local-only gate the
Cosmos test project has — no CI job provides one. Filter them out if it is not running. The
round-trip report stays Entity Framework Core only: Cosmos is not an EF provider, so there is no
command interceptor to count with.

### Why the benchmark's stream and aggregate ids differ

Not cosmetic. On Cosmos DB an event document is keyed `{streamId}:{sequence}` and an aggregate
document `{aggregateId}:{typeVersion}`, both in one container. The first version of this benchmark
used `show-1` for the stream and the aggregate, and the version 1 aggregate's document id collided
with the event at sequence 1 — `ReadItemAsync<AggregateDocument>` returned that event, and
`ToAggregate` threw `ArgumentNullException` on a null `AggregateType`.

The ids here are prefixed to avoid it. The repository's Cosmos tests avoid it too, but by naming
convention rather than by design.

## Serializer

`SerializerBenchmarks` sizes System.Text.Json against the Newtonsoft configuration the stores use
today, on the payloads the event store actually writes.
