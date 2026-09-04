# Benchmarks

BenchmarkDotNet projects backing decisions about Memoria's internals. Run everything, or filter:

```bash
dotnet run -c Release --project benchmarks/Memoria.Benchmarks                              # pick from a menu
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --filter "*StoreRead*"
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --round-trips             # not a benchmark, see below
```

**The numbers below come from two machines, and which one matters.** Everything about streams
against DCB was re-measured on 2026-09-04 after the DCB append lost three round trips: BenchmarkDotNet
0.15.8 on .NET 10.0.11, Windows 11, an Intel Core i7-14700KF with 20 physical cores, against SQL
Server 2022 and PostgreSQL 15 in Testcontainers. [The streamed store across
providers](#the-streamed-store-across-providers) and [Serializer](#serializer) still come from the
earlier sitting on an 11th Gen Intel Core i9-11900H with 8 physical cores, which also had the Cosmos
DB emulator running; neither was affected by that change, and the Cosmos rows cannot be reproduced
without an emulator. **Do not read a row from one section against a row from the other.**

Read the sitting, not the milliseconds: within a section the tables are comparable to each other and
to very little else, and the reasons are in [Reading the timings](#reading-the-timings).

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
| SaveAggregate (guarded)      | 3 vs 5 | 2 vs 4     | 2 vs 4     |

```bash
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --round-trips --verbose
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --round-trips --sqlserver --verbose
dotnet run -c Release --project benchmarks/Memoria.Benchmarks -- --round-trips --postgres --verbose
```

This is not a BenchmarkDotNet benchmark and needs no statistics: the count is exact and identical on
every run. **Reads cost DCB nothing in round trips. Appends cost it two more**, and `--verbose`
prints the statements so you can see which: it claims the tag head rows and reads the boundary's
position in one statement, writes the events, replaces the tokens, writes the tags, then the
snapshot.

The absolute counts drop by one on the real engines because Entity Framework Core batches more
aggressively there, folding the tag head `UPDATE` into the event `INSERT` and the streamed snapshot
`UPDATE` into its `INSERT`. **The gap is two on all three.**

It was five until three round trips were removed, and what each of them was is worth knowing, because
each looked necessary:

- The append **asked whether a snapshot row existed** before replacing it. It now attempts the
  replace outright and inserts only if it matched nothing — zero rows affected is not a SQL error, so
  a replace that misses costs a statement and does not poison the transaction on PostgreSQL the way a
  failed insert would.
- The append **read the tag head rows for their tokens, then read the boundary position separately**,
  and the two had to describe the same instant. They are read together now, so that holds by
  construction rather than by ordering two statements.
- The append **read the tag head rows twice**: once to find out whether they existed, once for their
  tokens. The second read answers both, so only the tags it did not return are created.

What is left on a real engine is four statements against the streamed store's two, and the two the
gap consists of are the tag head claim and the tag `INSERT`. Neither is easy to remove. The claim is
the concurrency check itself. The tag rows carry the position the database assigns to the event, so
Entity Framework Core has to write the event first and cannot batch the two inserts together —
closing that one would mean assigning positions on the client, which changes what a position means
rather than how it is written.

### What the engines showed

Reads are a wash, and the long ones favour DCB. Across all three engines and 10, 100 and 1000 events,
DCB lands between 0.76x and 1.40x of streams. The spread is not noise: it is almost entirely the
length of the read.

**Use `GetAggregate, snapshot only` to calibrate before believing any of the others.** It is the same
operation in both stores — one row fetched by primary key, no boundary anywhere near it — so its
ratio measures the harness rather than the store, and it ought to be exactly 1.00. It came out at
0.99–1.01 on SQL Server and 1.00–1.03 on PostgreSQL, which is as good as this gets, and 1.09–1.15 on
SQLite. A difference inside those bands is not a finding.

SQLite is the odd one, and the reverse of what you would expect: the in-process engine has the
*loosest* control. Its snapshot read is 22 μs against roughly 480 μs on the containers, so a fixed
couple of microseconds anywhere in the DCB path — a longer key, an extra comparison — is 10% of it.
That is a floor under every SQLite ratio here, not a property of the store.

Three results survive that test:

**`GetEvents` over a thousand events is faster under DCB on every engine** — 0.97x on SQLite, 0.76x
on SQL Server, 0.90x on PostgreSQL. Both stores fetch the same rows, and DCB reaches them through two
key seeks rather than one seek over a wider row. An earlier sitting had this as established on SQLite
and SQL Server and unproven on PostgreSQL; it now reproduces on all three, and SQL Server's margin is
the largest in the table.

**The fold behaves the same way**, which is the useful version of the same result: `GetAggregate,
folded` over a thousand events is 0.95x on SQLite, 0.77x on SQL Server and 0.93x on PostgreSQL.

**DCB allocates about 3% less over a thousand events** — 0.97x on all three engines, the steadiest
number in the set and the one least able to be explained by container noise.

Against those, the cost of a small boundary is real and points the other way. At ten events
`GetEvents` under DCB is 1.40x on SQLite, 1.11x on PostgreSQL and 1.07x on SQL Server, and allocates
1.24x on both SQLite and SQL Server. A boundary's fixed cost has nothing to amortise against when the
entire read is tens of microseconds. **The crossover is around a hundred events**, where every engine
sits within a few percent of 1.00.

| `SaveAggregate`, median | Streams         | DCB             | Ratio         | Allocated |
|-------------------------|-----------------|-----------------|---------------|-----------|
| SQLite, in memory       | 0.64 – 0.69 ms  | 1.02 – 1.06 ms  | 1.62–1.72     | 2.04x     |
| SQL Server, container   | 8.97 – 9.30 ms  | 11.4 – 12.4 ms  | **1.31–1.35** | 2.15x     |
| PostgreSQL, container   | 2.71 – 2.85 ms  | 5.85 – 6.28 ms  | **2.13–2.20** | 1.89x     |

The ranges span 10, 100 and 1000 already-stored events. **An append's cost is flat in the length of
the history behind it** — on all three engines the ratio moves by less than 0.1 across two orders of
magnitude, which is the firmest thing in this table.

One row to distrust: PostgreSQL at 1000 events reported a streamed mean of 3.78 ms against a median
of 2.85 ms, with a standard deviation of 9.4 ms and a `RatioSD` of 3.20. Something outside the store
stalled during that group. The median is in line with the other two sizes, which is the reason this
table reports medians at all.

These numbers replaced a set measured before the append lost three round trips, and the gap narrowed
on every engine: SQL Server from 1.77–1.86 to 1.31–1.35, PostgreSQL from 2.55–2.65 to 2.13–2.20,
allocation from 2.8x and 2.4x to 2.15x and 1.89x. Only part of that is the change — the two sets are
from different machines, so the *ratios* carry the comparison and the milliseconds do not. What the
change was worth on one machine, measured properly by alternating the two commits in a single
sitting, is a 26% faster append on PostgreSQL and 14% on SQL Server, with allocation down 20% and
25%.

Compare the ratios, never the milliseconds, when re-running. Two runs of the *same commit* on the
earlier machine ten minutes apart put the SQL Server streamed append at 13.8 ms and then 18.9 ms — a
37% spread with nothing changed. The ratios were stable across the same pair. If you need to know
whether a change moved the store, run the old and new commits alternately in one sitting and compare
within each pass; a table like this one from two different sittings cannot tell you. That protocol is
what separated the append change above from a machine which drifted 13% across the same runs.

The original reason for running more than one engine was a prediction: that SQLite would understate
the DCB append cost, because the extra round trips are nearly free in process. **It was wrong.** The
absolute cost rises several-fold on a real engine and the ratio stays in the same band, because a
streamed append pays transaction and commit costs that scale the same way.

The ratio does not track engine speed either. PostgreSQL has the fastest streamed append and the
largest DCB ratio, because its baseline is cheaper while the extra commands cost much the same.

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

**This table alone is from an earlier sitting**, because reproducing it needs the `IN` variant checked
out and alternated against `EXISTS` — the benchmark project only contains the shape that shipped. The
two baseline passes bracketing that measurement agreed to 1–2%, which is what makes a 1.05 against a
1.32 readable at all. Do not try to reproduce it on a container.

### Why the harness runs ANALYZE on PostgreSQL

Because without it the PostgreSQL numbers were fiction. A fresh database, a thousand inserted rows and
an immediate query gives the planner no statistics, and the DCB read is unusually sensitive to that:
both its predicates are `= ANY(@array)`, whose selectivity PostgreSQL cannot estimate from a
parameter, so it assumes one row on each side, picks a nested loop semi join and applies the position
match as a filter rather than an index condition.

That measured 77 ms against 3.3 ms for streams — a 21x "regression" that was entirely the missing
statistics. One `ANALYZE` takes it to 3 ms. The harness runs one after seeding, so the benchmarks
measure the store rather than autovacuum lag. SQL Server needs no equivalent: it creates missing
statistics itself on first use, which is why it never showed this.

**The 21x did not reproduce when it was checked again**, on PostgreSQL 15.1 on the newer machine, with
the `ANALYZE` suppressed and verified suppressed rather than assumed. A thousand freshly inserted rows
and an immediate fold gave 1.20x, not 21x — PostgreSQL had usable statistics anyway, presumably from
autoanalyze firing after the bulk insert. Statistics still matter and the `ANALYZE` still earns its
place: the same fold is 0.87x with it and 1.20x without. But the order of magnitude above belongs to
one sitting on one machine and should not be quoted as what missing statistics cost.

Two things follow. A boundary predicate really is `= ANY(@array)` on Npgsql at every cardinality,
including a single tag, where SQL Server expands the collection to ordinary parameters — that part
held up. And rewriting it to an equality was built and measured: it is worth about 10% on the
stale-statistics read and nothing at all once statistics exist, which was not enough to justify the
expression-building machinery it needed. If the estimate ever does become the problem, that is the
knob, and `EF.Constant` or Npgsql's parameterised-collection option reach it without new code.

### Reading the timings

Report medians rather than means for the write benchmarks. Commit spikes skew the mean badly enough
that the `RatioSD` column reaches 1.01 on SQLite; the medians are stable across runs.

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
| GetEvents                   | 1.68 ms | 3.99 ms    | 2.81 ms    | 5.59 ms |
| GetAggregate, snapshot only | 0.49 ms | 2.94 ms    | 1.84 ms    | 0.94 ms |
| GetAggregate, folded        | 1.63 ms | 3.77 ms    | 3.05 ms    | 5.62 ms |
| SaveAggregate               | 0.93 ms | 8.51 ms    | 4.19 ms    | 9.22 ms |

Cosmos wins the point read and loses the query, which is what it is built to do. Reading a snapshot
is a `ReadItemAsync` by id and partition key, and at 0.94 ms it beats both relational engines — three
times faster than SQL Server. Every operation that has to *query* — `GetEvents`, and the fold built
on it — is the slowest of the four. It also allocates roughly twice as much on those queries, 732 KB
against 394 KB on SQLite and 422 KB on SQL Server, which is the SDK's serialization rather than the
store's.

How well these reproduce across sittings varies by provider, and not in the direction you would
guess. Against the previous run of the same commit, Cosmos drifted at most 5% on any cell and
PostgreSQL at most 8%, while SQL Server reached 25% and in-process SQLite 47%. The slowest providers
are the most repeatable: their per-operation cost is large enough to swamp the fixed overheads and
collection pauses that dominate a 0.5 ms SQLite read. Trust the right-hand columns of this table more
than the left-hand ones, and re-run before reading anything into a SQLite change under about 50%.

There are no ratios in this report. Each provider is its own row, because none of them is a baseline
the others vary from: an in-process SQLite, two containers and a local emulator are four different
deployments, not four ways of doing one thing. The snapshot read is also the only row where the
comparison is close to fair — the other three are dominated by how each provider answers a query.

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

| Payload               | Newtonsoft         | System.Text.Json   | Time  | Allocated |
|-----------------------|--------------------|--------------------|-------|-----------|
| Event serialize       | 647 ns / 1,752 B   | 289 ns / 352 B     | 0.45x | 0.20x     |
| Event deserialize     | 997 ns / 3,336 B   | 512 ns / 376 B     | 0.51x | 0.11x     |
| Aggregate serialize   | 977 ns / 2,728 B   | 583 ns / 864 B     | 0.60x | 0.32x     |
| Aggregate deserialize | 1,744 ns / 3,928 B | 1,106 ns / 1,824 B | 0.63x | 0.46x     |

System.Text.Json is between 1.6x and 2.2x faster on every payload and allocates between a ninth and a
half as much. The widest gap is on deserializing an event — the operation a fold performs once per
event, and so the one that scales with history length.

Read that next to the store timings before drawing a conclusion from it. A fold over a thousand
events spends about 3 ms in the store on SQLite, and a thousand event deserializations at the measured
saving of 485 ns each account for roughly half a millisecond of it. The serializer is worth real
allocation on a hot fold; it is not where a remote database's latency goes.
