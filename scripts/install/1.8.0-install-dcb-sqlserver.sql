/*
    Memoria 1.8.0 — dynamic consistency boundary store schema install (SQL Server)

    Creates the four tables the Entity Framework Core DCB store needs, with their keys, indexes and
    foreign keys. Run this to stand up a Memoria DCB store without writing a migration yourself.

    This is the DCB store, not the streamed one. The two share nothing: if you use both, run this
    alongside 1.8.0-install-sqlserver.sql rather than instead of it.

    If you manage this database with EF Core migrations, do NOT run this. Your DbContext derives from
    DcbDbContext, so `dotnet ef migrations add` generates the same schema from the model and keeps
    __EFMigrationsHistory in step. See docs/guides/install-the-store-schema.md.

    Assumes the default table names and the dbo schema. Adjust the identifiers below if your
    DbContext maps them elsewhere.

    Safe to run more than once: every object is guarded, so re-running adds only what is missing. It
    does not alter or drop anything that already exists.

    Deliberately contains no GO separators, so it runs as a single batch under any client — sqlcmd,
    SSMS, Azure Data Studio, or a plain SqlCommand.

    The two Tag columns are created with an explicitly case-sensitive collation. Tags compare
    ordinally in .NET, so seat:A1 and seat:a1 are two tags; under SQL Server's usual case-insensitive
    default they would be one row, and every boundary naming them would be quietly wider than the
    code says. Do not relax this.
*/

SET XACT_ABORT ON;

/* ------------------------------------------------------------------- events */
IF OBJECT_ID(N'[dbo].[DcbEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DcbEvents] (
        [Position] bigint NOT NULL IDENTITY,
        [EventType] nvarchar(255) NOT NULL,
        [Data] nvarchar(max) NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(255) NULL,
        CONSTRAINT [PK_DcbEvents] PRIMARY KEY ([Position])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DcbEvents_CreatedDate'
                 AND object_id = OBJECT_ID(N'[dbo].[DcbEvents]'))
BEGIN
    CREATE INDEX [IX_DcbEvents_CreatedDate] ON [dbo].[DcbEvents] ([CreatedDate]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DcbEvents_EventType'
                 AND object_id = OBJECT_ID(N'[dbo].[DcbEvents]'))
BEGIN
    CREATE INDEX [IX_DcbEvents_EventType] ON [dbo].[DcbEvents] ([EventType]);
END;

/* --------------------------------------------------------------- snapshots */
IF OBJECT_ID(N'[dbo].[DcbSnapshots]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DcbSnapshots] (
        [Id] nvarchar(400) NOT NULL,
        [SnapshotKind] nvarchar(20) NOT NULL,
        [StoreId] nvarchar(255) NOT NULL,
        [TagQuery] nvarchar(max) NOT NULL,
        [ModelType] nvarchar(255) NOT NULL,
        [Version] int NOT NULL,
        [LatestPosition] bigint NOT NULL,
        [Data] nvarchar(max) NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(255) NULL,
        [UpdatedDate] datetimeoffset NOT NULL,
        [UpdatedBy] nvarchar(255) NULL,
        CONSTRAINT [PK_DcbSnapshots] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DcbSnapshots_Kind_StoreId'
                 AND object_id = OBJECT_ID(N'[dbo].[DcbSnapshots]'))
BEGIN
    CREATE INDEX [IX_DcbSnapshots_Kind_StoreId] ON [dbo].[DcbSnapshots] ([SnapshotKind], [StoreId]);
END;

/* --------------------------------------------------------------- tag heads */
IF OBJECT_ID(N'[dbo].[DcbTagHeads]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DcbTagHeads] (
        [Tag] nvarchar(255) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
        [Token] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_DcbTagHeads] PRIMARY KEY ([Tag])
    );
END;

/* --------------------------------------------------------------- event tags */
IF OBJECT_ID(N'[dbo].[DcbEventTags]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DcbEventTags] (
        [Position] bigint NOT NULL,
        [Tag] nvarchar(255) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
        CONSTRAINT [PK_DcbEventTags] PRIMARY KEY ([Tag], [Position]),
        CONSTRAINT [FK_DcbEventTags_DcbEvents_Position] FOREIGN KEY ([Position])
            REFERENCES [dbo].[DcbEvents] ([Position]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DcbEventTags_Position'
                 AND object_id = OBJECT_ID(N'[dbo].[DcbEventTags]'))
BEGIN
    CREATE INDEX [IX_DcbEventTags_Position] ON [dbo].[DcbEventTags] ([Position]);
END;
