/*
    Memoria 1.7.0 — drop the aggregate-to-event link table (PostgreSQL)

    Memoria 1.7.0 no longer writes or reads DomainAggregateEvents. The table is inert after you
    upgrade: nothing in the store touches it, and leaving it in place costs only the storage it
    already occupies. Run this when you are ready to reclaim that space.

    Assumes the default table names and the public schema. Identifiers are quoted because EF creates
    them case-sensitively; adjust them if your DbContext maps them elsewhere.

    Safe to run more than once.

    If you manage this database with EF Core migrations, do NOT run this. Your DbContext derives from
    DomainDbContext, so `dotnet ef migrations add` generates the drop from the model and keeps
    __EFMigrationsHistory in step. See docs/guides/upgrade-1.7.0.md.

    This DROP is irreversible and the data is not reproducible: the link rows recorded which events
    were applied to which aggregate at the time they were applied, and that cannot be reconstructed
    from the event stream afterwards. If you still need that history, copy the table somewhere first.

    DomainAggregateEvents is the dependent side of both its foreign keys — nothing references it — so
    dropping the table takes its keys and indexes with it and leaves no orphaned constraints.
*/

DROP TABLE IF EXISTS public."DomainAggregateEvents";
