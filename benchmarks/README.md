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

Both run on in-memory SQLite. That is a deliberate trade: it needs nothing installed, so anyone can
reproduce the numbers, and the relative query-shape costs are real.

### What SQLite hides, and what to read instead

A round trip inside this process costs almost nothing. Against a networked engine it is a real
millisecond, and the two stores do not issue the same number of them:

```bash
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --round-trips --verbose
```

```
| Operation                    | Streams | DCB |
|------------------------------|---------|-----|
| GetEvents(100)               |       1 |   1 |
| GetAggregate (snapshot only) |       1 |   1 |
| GetAggregate (folded)        |       1 |   1 |
| SaveAggregate (guarded)      |       3 |   8 |
```

This is not a BenchmarkDotNet benchmark and needs no statistics: the count is exact, identical on
every run, and — unlike a timing — identical on every engine. **Reads cost DCB nothing in round
trips. Appends cost it five more**, and `--verbose` prints the statements so you can see which five.
On a networked engine that gap, not the timings below, is what you will feel.

### Reading the timings

Report medians rather than means for the write benchmarks. SQLite commit spikes skew the mean badly
enough that BenchmarkDotNet's own `RatioSD` column exceeds 0.8; the medians are stable across runs.

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
