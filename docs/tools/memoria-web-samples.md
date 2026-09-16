# Try Memoria Web with sample data

[Memoria Web](memoria-web.md) shows you a store through your own domain types. To try it before you
have either — or to see what a store with something interesting in it looks like — the repository
ships the samples, three projects with two jobs between them:

- `Memoria.Web.Samples.Streamed` and `Memoria.Web.Samples.Dcb` **carry a sample domain**, an
  ecommerce one, modelled once in each consistency model — one assembly per model, so that either
  can be uploaded on its own
- `Memoria.Web.Samples` **fills a store** with data written through that domain, using the framework
  itself

The types the tool displays are the types the seeder exercises, so nothing reaches the tool that was
never written through Memoria first.

Five steps, and about five minutes:

1. [Point the seeder at a store](#1-point-the-seeder-at-a-store)
2. [Run it](#2-run-it)
3. [Package the sample assemblies](#3-package-the-sample-assemblies)
4. [Point the tool at the same store](#4-point-the-tool-at-the-same-store)
5. [Upload a zip and look around](#5-upload-a-zip-and-look-around)

## 1. Point the seeder at a store

The connection string is `ConnectionStrings:Memoria` in
[`src/Memoria.Web.Samples/appsettings.json`](https://github.com/lucabriguglia/Memoria/blob/main/src/Memoria.Web.Samples/appsettings.json).
As checked in, it is a local PostgreSQL database called `memoria_samples`:

```json
{
  "ConnectionStrings": {
    "Memoria": "Host=localhost;Port=5432;Database=memoria_samples;Username=postgres;Password=password"
  }
}
```

Any of the four engines works. The string itself says which one — the resolution is the tool's own
[`DatabaseConnection`](https://github.com/lucabriguglia/Memoria/blob/main/src/Memoria.Web/Data/DatabaseConnection.cs),
compiled into both, so a string that reaches one reaches the other. See
[which engine it is](memoria-web-configuration.md#which-engine-it-is) for the keywords and for
`Database:Provider`.

| Engine     | Example                                                                |
| ---------- | ---------------------------------------------------------------------- |
| PostgreSQL | `Host=localhost;Port=5432;Database=memoria_samples;Username=postgres;Password=password` |
| SQL Server | `Server=(localdb)\\MSSQLLocalDB;Initial Catalog=memoria_samples;Trusted_Connection=True` |
| SQLite     | `Data Source=memoria_samples.db`                                       |
| Cosmos DB  | `AccountEndpoint=https://localhost:8081/;AccountKey=<emulator key>`    |

For Cosmos, set the database and container as well, and start the emulator first — the run stops with
"Could not reach the database" before asking anything otherwise:

```json
{
  "Database": {
    "Cosmos": {
      "DatabaseName": "memoria_samples",
      "ContainerName": "Domain"
    }
  }
}
```

You do not have to edit the file. Every setting can be overridden on the command line, which is how
to point one run at a scratch store:

```bash
dotnet run --project src/Memoria.Web.Samples -- \
  "--ConnectionStrings:Memoria=Data Source=scratch.db"
```

Unlike the tool, the seeder **creates what is missing** before it writes: the database and each
store's tables on a relational engine, the database and the container on Cosmos.

## 2. Run it

```bash
dotnet run --project src/Memoria.Web.Samples
```

```
Memoria.Web.Samples
  store    : Npgsql
  writing  : memoria_samples
  bound    : 30 events, 5+5 aggregates, 4+6 projections (streamed+dcb)
  schema   : installed
```

It then asks two questions, answered by number on standard input:

| Answer | Action                                               |
| ------ | ---------------------------------------------------- |
| `1`    | Add — writes alongside whatever is already there     |
| `2`    | Replace — empties the store, then writes             |
| `3`    | Delete — empties the store and writes nothing        |
| (none) | Nothing chosen; the run stops without touching a row |

Then, **only when the store holds both models**, which data to write: `1` streamed, `2` DCB, `3`
both. A Cosmos store holds the streamed model only, so it is never asked there.

End of input counts as an answer — it means nobody is there to answer — so a piped run works and an
unattended one stops rather than hanging:

```bash
echo "2" | dotnet run --project src/Memoria.Web.Samples
```

Exit code `0` means the run finished, including a run where nothing was chosen; `1` means the store
could not be reached and nothing was written.

### What it writes

**Streamed** — customers, the orders they place, and the reviews products collect. Every order is
driven through the `Order` aggregate's own methods, so the log holds only sequences the domain would
have allowed. Some reviews go through `ProductReviewV1` rather than `ProductReview`, so the store
ends up holding snapshots of two shapes of one model side by side.

**DCB** — a catalogue, stock for it, orders holding some of that stock, and the purchase orders that
restock it. Every append follows the read-decide-append cycle on condition that the boundary has not
moved, the way an application would write it.

Snapshots are deliberately left in three states, because a store where everything is current has
nothing to demonstrate:

| State       | How it is reached                                                 |
| ----------- | ----------------------------------------------------------------- |
| Up to date  | Snapshotted after the last event on its stream                    |
| Behind      | Snapshotted, then more events appended without snapshotting again |
| No snapshot | Events appended and never snapshotted                             |

The run ends by listing every model it wrote and where its snapshot stands, so you know which rows
have an **Update** waiting for them in the tool:

```
store    kind       model                 identifier            values     snapshot
streamed aggregate  Order                 OrderId               o-5b0f     v4, up to date
streamed aggregate  CustomerAccount       CustomerAccountId     c-37e7     v2 of 6 — 4 behind
streamed projection OrderSummary          OrderSummaryId        o-a6f6     no snapshot — 2 events waiting
```

## 3. Package the sample assemblies

The tool has no reference to these projects — it reads uploaded archives and nothing else. So
build them and zip each domain assembly with its manifest, one archive per model. Each sample
project carries a `memoria.json` that the build copies beside its assembly, declaring one service
each — **Samples Streamed** and **Samples DCB**, browsed at `/samples-streamed` and `/samples-dcb`
— over the `Memoria` connection string:

```bash
dotnet build src/Memoria.Web.Samples --configuration Release
```

```powershell
Compress-Archive -Path src\Memoria.Web.Samples.Streamed\bin\Release\net10.0\Memoria.Web.Samples.Streamed.dll, `
                       src\Memoria.Web.Samples.Streamed\bin\Release\net10.0\memoria.json `
                 -DestinationPath Memoria.Web.Samples.Streamed.zip -Force
Compress-Archive -Path src\Memoria.Web.Samples.Dcb\bin\Release\net10.0\Memoria.Web.Samples.Dcb.dll, `
                       src\Memoria.Web.Samples.Dcb\bin\Release\net10.0\memoria.json `
                 -DestinationPath Memoria.Web.Samples.Dcb.zip -Force
```

```bash
# bash
cd src/Memoria.Web.Samples.Streamed/bin/Release/net10.0 && zip ~/Memoria.Web.Samples.Streamed.zip Memoria.Web.Samples.Streamed.dll memoria.json
cd src/Memoria.Web.Samples.Dcb/bin/Release/net10.0 && zip ~/Memoria.Web.Samples.Dcb.zip Memoria.Web.Samples.Dcb.dll memoria.json
```

Each archive holds its one `.dll` and its `memoria.json` at the root, and nothing else. A zip
without the manifest is refused. Never put a `Memoria*` core assembly in one — see
[what to put in a zip](memoria-web-configuration.md#what-to-put-in-a-zip).

Two archives rather than one, each declaring a service of its own, so that you can upload one
model alone. The home page lists whichever are installed, and each service's page is laid out for
the model it registered types under: **Samples Streamed**, at `/samples-streamed`, for the streamed
model; **Samples DCB**, at `/samples-dcb`, for dynamic consistency boundaries — each with that
model's sections on the bar.

## 4. Point the tool at the same store

Give `src/Memoria.Web/appsettings.json` the same connection string the seeder used — and, on Cosmos,
the same database and container names, or the tool opens a container the seeder never filled.

```bash
dotnet run --project src/Memoria.Web
```

Then open `http://localhost:5159`.

## 5. Upload a zip and look around

Go to **Settings → Installed → Upload**, choose a zip, and upload it — then the other, if you want
both models. The page reports what registered; **Settings → Types** counts it per model. Nothing
restarts. Each row of the installed table opens a sheet over its zip: the service the manifest
declares, **Samples Streamed** or **Samples DCB**, the address it is browsed at, the `Memoria`
connection string it reads over, and the types registered from the assembly. **Home** then lists the services installed; open one, and
its page leads into its events, aggregates and projections — at `/samples-streamed/streamed/...`
and `/samples-dcb/dcb/...`.

Things worth opening first:

- **Streamed → Aggregates → Data.** Two versions of `ProductReview` sit here under one name, because
  some reviews were written through `ProductReviewV1`. The version column is what tells them apart.
- **A row the seeder left behind.** Open it, then the **Events** tab — the events its snapshot has
  not applied yet — then **Update**, which applies them and writes the result back. The **Info** tab
  then agrees with the stream.
- **A row with no snapshot at all.** The same **Update** builds one from scratch.
- **DCB → Events → Data.** Filter by a tag rather than by a stream: a DCB event belongs to no stream,
  so its tags are the handle a boundary finds it through.
- **DCB → Aggregates → Data.** Each row is a snapshot keyed by the boundary that produced it. The
  tags column is that boundary, whole.

## Limits

**Cosmos holds the streamed model only.** There is no Cosmos dynamic consistency boundary store, and
the model would not build on that provider if there were. A Cosmos run is never offered the DCB half
rather than being offered it and refused on the first write.

**In-memory SQLite is refused.** `Data Source=:memory:` lives only as long as the connection that
opened it, and both the seeder and the tool open a connection per unit of work — they would find an
empty store rather than the one that was seeded. Point them at a file.

**Do not upload the DCB samples alongside `Memoria.Examples.Ecommerce.Dcb`.** Both claim the event
types `ProductCreated`, `ProductDeleted` and `ProductDetailsChanged` at version 1, and the DCB
aggregate `Product` at version 1. Whichever loses the name loses its bindings, and its pages then
report no events inside the boundary. Remove one archive before uploading the other.

**A version bump means a rebuild.** The assemblies are compiled against the `<Version>` in
[`Directory.Build.props`](https://github.com/lucabriguglia/Memoria/blob/main/Directory.Build.props).
One built against an older version still loads, and then contributes no types at all.

## Where things are

| Path                                     | What is in it                                                           |
| ---------------------------------------- | ----------------------------------------------------------------------- |
| `src/Memoria.Web.Samples.Streamed/`      | The streamed model: streams, aggregates, projections, events            |
| `src/Memoria.Web.Samples.Dcb/`           | The dynamic consistency boundary model: aggregates, projections, events |
| `src/Memoria.Web.Samples/Seeding/`       | The run itself — the menu, the store it writes to, and the data         |
| `src/Memoria.Web.Samples/Data/`          | The two contexts a relational store is written through                  |
