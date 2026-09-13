# Memoria Web

Memoria Web is a browser tool for reading a Memoria store. Point it at a database, upload a zip of
your own domain assemblies, and it shows you the events that were appended, the aggregates and
projections snapshotted from them, and the types both were written through.

It is not a sample application and not a package. It lives in the repository at
[`src/Memoria.Web`](https://github.com/lucabriguglia/Memoria/tree/main/src/Memoria.Web), and you
build and run it yourself — see [Deployment](memoria-web-deployment.md).

![The home page: the streamed model and the DCB model side by side](../images/memoria-web/home.png)

- [Configuration](memoria-web-configuration.md) — the connection string, the provider, the Cosmos
  database and container, where uploads are kept
- [Deployment](memoria-web-deployment.md) — publishing it, hosting it, and what has to be true of
  the host
- [Try it with sample data](memoria-web-samples.md) — a store filled in a couple of minutes, with no
  domain of your own needed

## Why it needs your assemblies

A Memoria store holds serialised payloads under type bindings — `OrderPlaced` at version 1, an
aggregate under `[AggregateType("Order")]`. The bindings are names; the shapes they name live in
your assemblies. Without them the tool could list rows and nothing else: it could not say what an
event carries, fold a stream into an aggregate, or work out which snapshots a boundary should hold.

So the tool has no reference to any domain at all. You upload assemblies on the **Settings** page,
it scans them for the types both consistency models declare, and registers what it finds:

| Model    | Scanned for                                                                             |
| -------- | --------------------------------------------------------------------------------------- |
| Streamed | `IStreamId`, `IAggregateRoot`, `IAggregateId`, `IProjection`, `IProjectionId`, `IEvent`  |
| DCB      | `IDcbAggregateRoot`, `IDcbAggregateId`, `IDcbProjection`, `IDcbProjectionId`, `IEvent`   |

Events are one bound set — an event is the same event whichever model appends it — so an event both
models apply is counted under both.

The assemblies are read from bytes rather than from their path, and the registrations are rebuilt
from scratch on every upload, removal and refresh. Nothing restarts, and a type you removed from a
rebuilt assembly stops being offered rather than lingering from the previous load.

The Settings page lists what each archive brought. A **Types** mark on each row of the installed
archives table opens every assembly file in that zip and, under each, the domain types registered
from it — and a file that registered nothing says so on a line of its own, which is the case worth
noticing: a dependency the domain needs, or an assembly that did not load.

A type carrying `[Obsolete]` is marked as such wherever it is named, and says the attribute's own
message wherever it is opened. Retired is not the same as old: a type with a later version beside it
is old and the version says so; a retired type is one nothing should write through any more, and the
attribute is the only place the domain says that.

## What it shows

The two consistency models sit side by side from the home page, and each is laid out the same way:

- **Overview** — what is registered under that model, counted per section
- **Events**, **Aggregates**, **Projections** (and **Streams**, streamed only) — each a section with
  a **Types** page (what the uploaded assemblies declare) and a **Data** page (what the store holds)

A **Types** page reads the registration rather than the store: what each type is bound as, at which
version, and the assembly it came out of.

![Event types: the events the streamed model declares, with the binding for one of them](../images/memoria-web/event-types.png)

Data pages page, sort and filter, and every piece of that state — the filter, the sort, the page, the
page size, which payloads are expanded — travels in the query string, so a view can be bookmarked,
shared and stepped back through.

| Page                          | Narrowed by                                          |
| ----------------------------- | ---------------------------------------------------- |
| Streamed → Events → Data      | Stream, event type, and text in the payload          |
| Streamed → Aggregates → Data  | Aggregate type, identifier, and text                 |
| Streamed → Projections → Data | Projection type, identifier, and text                |
| DCB → Events → Data           | Event type, and text in the payload **or in a tag**  |
| DCB → Aggregates → Data       | Aggregate type, identifier, and text in a tag        |
| DCB → Projections → Data      | Projection type, identifier, and text in a tag       |

![Aggregate data: rows narrowed by stream, aggregate and identifier](../images/memoria-web/aggregate-data.png)

Opening a row reaches a detail page with six tabs: **Info** (how the row is identified and where its
snapshot stands), **State** (the model folded), **Json** (the stored payload itself), **Events** (what
it applied, or what its boundary holds), **Compare** (two versions of the model, side by side), and
**Update**. Across from the heading, **View Type** opens the row's declared type over it — the same
four views the Types pages show, info, state, events and identifiers — so a stored row can be
matched against what its type declares without leaving it. Like every other view here it opens by
address, so it can be linked to and the browser's back button closes it.

**State** and **Json** show the same payload two ways. State reads it through the model's own
properties; Json shows the text the store holds, laid out one value per line and coloured by kind,
in a box that scrolls once it grows past the screen. It is the row's own text rather than the model
serialised again, so a payload the model cannot read back, or one carrying more than the model
declares, is still there to see — and a payload that is not JSON at all is shown as it is, under a
note saying why it could not be laid out. A **Copy** button above the box puts the laid-out text on
the clipboard; it appears only where the browser allows the page to write there, which means a
secure context: `localhost` or HTTPS.

Every table of events offers the same thing per row — the two **Events → Data** pages and the
**Events** tab of any aggregate or projection. Beside the **Payload** column, which opens what the
row was read into, a **Json** column opens what the store actually wrote in a pop-up over the table,
with the same Copy button. It is a link like every other view here, so it can be bookmarked and is
closed by the browser's back button as well as by the sheet's own close.

An event has a detail page of its own too, opened by clicking a row on either **Events → Data**
page, the way a row on the aggregate and projection lists opens. It has the three tabs a model's
page opens with and no more: **Info** (the stream and key, or the tags, the type the row was
written under, its sequence or position, and when and by whom it was appended), **State** (the
payload read through the event's own properties), and **Json** (the stored payload itself). An
event has no history to list, nothing to compare and nothing to update, so those tabs are absent
rather than empty. Across from the heading, **View Type** opens the event's declared type over it,
in the two views an event has, info and state — and it is there only when an uploaded assembly
registers the type the row was written under, since a key nothing claims has no declaration to
open. The state tab still says why such a payload will not read back, and the Json tab still shows
what was written.

On a model's **Events** tab, every row also starts with a mark saying whether the stored snapshot
has applied it: a tick when it has, a clock when it has not yet. The mark is read off the snapshot's
own record of the latest sequence or position it folded, so an event at or below that place is in
the snapshot and one above it is not — and with no snapshot stored, none is. Text typed into the
filter above the table is marked wherever the table matched it.

**Info** says where the snapshot stands against its history. When the stored version is below the
number of events the model is folded from, the version carries the same clock and a sentence saying
how far behind it is, with a link to **Update**, which would fold the rest in.

**Compare** lays two versions of the model over each other. A version is the model's own count of
applied events, so it climbs by one down the Events tab where a sequence or a position counts the
whole stream or log — and each row's **Compare** link opens the version that event produced against
the one before it, so a history is walked one event at a time. The tab folds the model in memory at
each version, through the store's up-to-sequence or up-to-position read, and writes nothing. Two
cards say what each version is: the version, the sequence or position it was folded up to, the event
that produced it, and when it was appended, as the store holds it. Under them, the State tab's rows
with a value from each version and a mark on every row where the two differ — changed, added or
removed — nested rows kept aligned by path. A form takes any two versions, bounded by the last, and
two links step the pair one version up or down. A compared version the stored snapshot has not
reached says so beside its number, with the same link to **Update**.

![An aggregate folded from its events, on the State tab](../images/memoria-web/aggregate-details.png)

## The one thing it writes

**Update** refreshes a stored snapshot: the events the model has not applied yet are applied to it
and the result is written back. That is the whole of it. The tool never appends an event, never
deletes a row, and never creates a database, a container or a table — it opens what is already
there, and a store whose schema is missing is an error the page reports rather than something it
installs.

A snapshot that is already current, and a model with nothing to fold, both say so and write nothing.

## What each store answers

| Engine     | Streamed | DCB | Notes                                                    |
| ---------- | -------- | --- | -------------------------------------------------------- |
| PostgreSQL | ✅       | ✅  | Read through Entity Framework Core                       |
| SQL Server | ✅       | ✅  | Read through Entity Framework Core                       |
| SQLite     | ✅       | ✅  | Against a file; an in-memory database is refused         |
| Cosmos DB  | ✅       | ❌  | Read through the Cosmos SDK, the way the store writes it |

There is no Cosmos DB store for dynamic consistency boundaries, and the model would not build on that
provider if there were — see
[Providers](../concepts/providers.md#why-there-is-no-cosmos-db-provider-for-dcb). Under a Cosmos
store the DCB menu is not shown and its addresses answer 404, so a bookmark says the same thing the
menu does.

Cosmos also cannot fully order a page of results, and falls back to ordering by date alone: every row
is there, but rows written at the same moment can move between pages. Pages under that store say so,
and the note can be turned off under **Preferences**, which opens from your name in the header
(or stands in its place when the tool runs open).

## Everyone shares one set of types

Type registration lives in Memoria's process-wide bindings, so one person's upload, removal or
refresh changes what every user of that server resolves. There is no push: other people's open pages
catch up when the browser is reloaded. The Settings page says this under both tabs that can cause it.

Browser preferences — theme, rows per page, whether the ordering note is shown — are the exception.
They are stored in the browser and the server is never told.

## About

**About**, linked from the footer, is the page about the tool itself: the version running and the
commit it was built from, each linked to where it is looked up — the release notes and GitHub —
the licence, and the documentation, this page among it. Both the version and the commit are read
off the running assembly, so a deployment shows what was actually built rather than what a file
says it should be.

## Security

> **Uploading is running code.** An operator who can reach `/settings` can upload a `.dll` that this
> process will load and execute, with no restriction on what it may do. Sign-in decides who can
> reach it; nothing decides what they may do once they have.

**Operators sign in through your OpenID Connect provider.** Nothing — no page, no form post —
answers anyone who has not, and the tool refuses to start until it is told which provider, or told
in so many words to run open. See [Configuration](memoria-web-configuration.md#signing-operators-in)
for the settings and [Deployment](memoria-web-deployment.md#signing-operators-in) for what to
register at the provider.

**What an operator may do is their role.** A Reader reads every page; an Updater may also press
**Update**; an Administrator may also use Settings. Every signed-in operator is a Reader until a
claim the provider sends is mapped to one of the other two — see
[Roles](memoria-web-configuration.md#roles). Map Administrator only to the people you would give
shell access on the host to, and treat it as exactly that.

Run it open — `Authentication:Disabled=true`, which is how `dotnet run` runs it on localhost — only
on localhost or behind a proxy that authenticates every request including the form posts. Every
start-up while it is open logs a warning saying so.

Two more things worth knowing before pointing it at anything that matters:

- **The connection string is the tool's whole authority.** Give it a read-only account unless you
  intend to use **Update**, which needs to write snapshots.
- **Uploaded archives persist.** They are kept on disk under the extensions directory and read again
  at the next start-up — see [Configuration](memoria-web-configuration.md#extensions).

## Requirements

- .NET 10 SDK to build it, or the ASP.NET Core 10 runtime to run a published build
- A store created by Memoria 1.9.0. The tool reads today's schema — `DomainEvents`,
  `DomainAggregates`, `DomainProjections` and the four `Dcb*` tables — so a database from an earlier
  version needs its [upgrade](../guides/upgrade-1.9.0.md) applied first
- Assemblies compiled against the same Memoria version the tool was built from. One built against an
  earlier version still loads, then contributes no types at all, and the Settings page reports the
  load error
