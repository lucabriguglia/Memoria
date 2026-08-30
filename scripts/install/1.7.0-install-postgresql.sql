/*
    Memoria 1.7.0 — event store schema install (PostgreSQL)

    Creates the three tables the Entity Framework Core store needs, with their keys, indexes and
    foreign keys. Run this to stand up a Memoria store without writing a migration yourself.

    If you manage this database with EF Core migrations, do NOT run this. Your DbContext derives from
    DomainDbContext, so `dotnet ef migrations add` generates the same schema from the model and keeps
    __EFMigrationsHistory in step. See docs/guides/install-the-store-schema.md.

    Assumes the default table names and the public schema. Identifiers are quoted because EF creates
    them case-sensitively; adjust them if your DbContext maps them elsewhere. Note that `events` is
    unquoted, matching what the provider generates for an already-lowercase name.

    Safe to run more than once: every object is created IF NOT EXISTS, so re-running adds only what is
    missing. It does not alter or drop anything that already exists — for upgrading an existing
    database, use the migration scripts under scripts/migrations instead.
*/

/* --------------------------------------------------------------- aggregates */
CREATE TABLE IF NOT EXISTS public."DomainAggregates" (
    "Id" character varying(255) NOT NULL,
    "StreamId" character varying(255) NOT NULL,
    "AggregateType" text NOT NULL,
    "Version" integer NOT NULL,
    "LatestEventSequence" integer NOT NULL,
    "Data" text NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(255),
    "UpdatedDate" timestamp with time zone NOT NULL,
    "UpdatedBy" character varying(255),
    CONSTRAINT "PK_DomainAggregates" PRIMARY KEY ("Id")
);

/* -------------------------------------------------------------- projections */
CREATE TABLE IF NOT EXISTS public."DomainProjections" (
    "Id" character varying(255) NOT NULL,
    "StreamId" character varying(255) NOT NULL,
    "ProjectionType" text NOT NULL,
    "Version" integer NOT NULL,
    "LatestEventSequence" integer NOT NULL,
    "Data" text NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(255),
    "UpdatedDate" timestamp with time zone NOT NULL,
    "UpdatedBy" character varying(255),
    CONSTRAINT "PK_DomainProjections" PRIMARY KEY ("Id")
);

/* ------------------------------------------------------------------- events */
CREATE TABLE IF NOT EXISTS public.events (
    "Id" text NOT NULL,
    "StreamId" character varying(255) NOT NULL,
    "EventType" character varying(255) NOT NULL,
    "Sequence" integer NOT NULL,
    "Data" text NOT NULL,
    "CreatedDate" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(255),
    CONSTRAINT "PK_events" PRIMARY KEY ("Id")
);

/* ------------------------------------------------------------------ indexes */
CREATE INDEX IF NOT EXISTS "IX_Events_EventType"
    ON public.events ("EventType");

CREATE INDEX IF NOT EXISTS "IX_Events_StreamId_CreatedDate"
    ON public.events ("StreamId", "CreatedDate");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Events_StreamId_Sequence"
    ON public.events ("StreamId", "Sequence");
