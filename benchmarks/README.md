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

Both run on **two engines**, as a BenchmarkDotNet parameter: in-memory SQLite, which needs nothing
installed, and SQL Server 2022 in a container via Testcontainers. The SQL Server container starts
once per benchmark process, lazily, on the first case that needs it; Docker must be running or those
cases fail with a message saying so.

### Round trips

A round trip inside this process costs almost nothing. Against a real engine it costs real
milliseconds, and the two stores do not issue the same number of them:

```bash
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --round-trips --verbose
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --round-trips --sqlserver --verbose
```

| Operation                    | Streams (SQLite) | DCB (SQLite) | Streams (SQL Server) | DCB (SQL Server) |
|------------------------------|------------------|--------------|----------------------|------------------|
| GetEvents(100)               |                1 |            1 |                    1 |                1 |
| GetAggregate (snapshot only) |                1 |            1 |                    1 |                1 |
| GetAggregate (folded)        |                1 |            1 |                    1 |                1 |
| SaveAggregate (guarded)      |                3 |            8 |                    2 |                7 |

This is not a BenchmarkDotNet benchmark and needs no statistics: the count is exact and identical on
every run. **Reads cost DCB nothing in round trips. Appends cost it five more**, and `--verbose`
prints the statements so you can see which five.

The absolute counts differ by engine because Entity Framework Core batches more aggressively on SQL
Server — it folds the tag head `UPDATE` into the command that inserts the events, and the streamed
snapshot `UPDATE` into the one that inserts the event. **The gap does not move: five extra commands
on both.**

### What the two engines actually showed

The reason for running both was a prediction: that SQLite would understate DCB's append cost, because
five extra round trips are nearly free in process and expensive over a wire. **The prediction was
wrong, and the measurement is the reason we know.**

| `SaveAggregate`, median | Streams  | DCB      | Ratio |
|-------------------------|----------|----------|-------|
| SQLite, in memory       | ~0.95 ms | ~2.1 ms  | ~2.1× |
| SQL Server, container   | ~7.4 ms  | ~14.5 ms | ~2.0× |

Absolute cost rose about sevenfold on both sides and the ratio stayed put, because a streamed append
pays transaction and commit costs that scale the same way. Reads showed no systematic difference on
either engine.

What the container cannot answer is distance: it runs on the same machine, so a round trip is
loopback, not a network hop. The further the database, the more the five extra commands cost, and the
ratio is the first thing that will move. Keep the round-trip table for that case.

### Reading the timings

Report medians rather than means for the write benchmarks. Commit spikes skew the mean badly enough
that BenchmarkDotNet's own `RatioSD` column exceeds 0.8 on SQLite; the medians are stable across runs.

The write benchmarks use `RunStrategy.Monitoring` with one invocation per iteration, because every
append has a side effect: it moves the sequence and the position the next append is guarded against.
The benchmark carries that state forward so each iteration is a real guarded write, and throws if one
is ever refused — a refused append takes a much cheaper path, and measuring it by accident would
flatter DCB for the worst possible reason.

Both sides are guarded. Comparing a guarded streamed append against an unguarded DCB append would be
meaningless, because the guard is most of what a DCB append does.

## Serializer

`SerializerBenchmarks` sizes System.Text.Json against the Newtonsoft configuration the stores use
today, on the payloads the event store actually writes.
