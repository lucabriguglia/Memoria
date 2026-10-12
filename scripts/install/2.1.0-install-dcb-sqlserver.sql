/*
    Memoria 2.1.0 — dynamic consistency boundary store schema install (SQL Server)

    Creates the four tables the Entity Framework Core DCB store needs, with their keys, indexes and
    foreign keys, and the append procedure the store calls to append in one round trip. Run this to
    stand up a Memoria DCB store without writing a migration yourself.

    This is the DCB store, not the streamed one. The two share nothing: if you use both, run this
    alongside 1.9.0-install-sqlserver.sql rather than instead of it.

    If you manage this database with EF Core migrations, do NOT run this. Your DbContext derives from
    DcbDbContext, so `dotnet ef migrations add` generates the tables from the model, and
    migrationBuilder.CreateDcbAppendRoutine(TargetModel) adds the procedure. See
    docs/guides/install-the-store-schema.md.

    Assumes the default table names and the dbo schema. If your DbContext maps the tables
    elsewhere, do not adapt this script: install the procedure with
    Database.EnsureDcbAppendRoutineAsync() or a migration instead, which generate it from your model.

    Needs SQL Server 2016 SP1 or later, at database compatibility level 130 or above: the procedure
    uses CREATE OR ALTER and OPENJSON.

    Safe to run more than once: every table and index is guarded, and the procedure is created with
    CREATE OR ALTER, so re-running adds only what is missing and leaves the data alone.

    Deliberately contains no GO separators, so it runs as a single batch under any client — sqlcmd,
    SSMS, Azure Data Studio, or a plain SqlCommand. A procedure has to be created by a batch of its
    own, so it is created through EXEC.

    The two Tag columns are created with an explicitly case-sensitive collation. Tags compare
    ordinally in .NET, so seat:A1 and seat:a1 are two tags; under SQL Server's usual case-insensitive
    default they would be one row, and every boundary naming them would be quietly wider than the
    code says. Do not relax this.

    Upgrading from 1.8.0: the tables are unchanged, so running this script adds only the procedure.
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

/*
    Entity Framework Core indexes the foreign key by convention, so this index is part of the model
    the migration tooling would generate and is created here to match it. It serves the cascade
    above; no read in the store looks a tag up by position.
*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DcbEventTags_Position'
                 AND object_id = OBJECT_ID(N'[dbo].[DcbEventTags]'))
BEGIN
    CREATE INDEX [IX_DcbEventTags_Position] ON [dbo].[DcbEventTags] ([Position]);
END;

/* ----------------------------------------------------------- append procedure */
EXEC (N'CREATE OR ALTER PROCEDURE [memoria_dcb_append_v1]
    @affected_tags nvarchar(max),
    @boundary_tags nvarchar(max),
    @boundary_is_intersection bit,
    @after_position bigint,
    @events nvarchar(max),
    @created_date datetimeoffset,
    @created_by nvarchar(255),
    @snapshot_id nvarchar(400),
    @snapshot_kind nvarchar(20),
    @snapshot_store_id nvarchar(255),
    @snapshot_tag_query nvarchar(max),
    @snapshot_model_type nvarchar(255),
    @snapshot_version int,
    @snapshot_latest_position bigint,
    @snapshot_data nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @latest bigint;
    DECLARE @boundary_count int = (SELECT COUNT(*) FROM OPENJSON(@boundary_tags));
    DECLARE @count int = (SELECT COUNT(*) FROM OPENJSON(@events));
    DECLARE @last bigint;

    IF @count = 0
        BEGIN TRANSACTION;
    ELSE
    BEGIN
        IF @boundary_tags IS NOT NULL
        BEGIN
                IF @boundary_is_intersection = 1
                    SELECT @latest = ISNULL(MAX(matched.[Position]), 0)
                    FROM (SELECT t.[Position] AS [Position]
                          FROM OPENJSON(@boundary_tags) WITH ([Tag] nvarchar(255) ''$'') b
                          JOIN [DcbEventTags] t WITH (FORCESEEK) ON t.[Tag] = b.[Tag] COLLATE SQL_Latin1_General_CP1_CS_AS
                          GROUP BY t.[Position]
                          HAVING COUNT(*) = @boundary_count) matched;
                ELSE
                    SELECT @latest = ISNULL(MAX(latest.[Position]), 0)
                    FROM OPENJSON(@boundary_tags) WITH ([Tag] nvarchar(255) ''$'') b
                    CROSS APPLY (SELECT TOP (1) t.[Position] AS [Position]
                                 FROM [DcbEventTags] t WITH (FORCESEEK)
                                 WHERE t.[Tag] = b.[Tag] COLLATE SQL_Latin1_General_CP1_CS_AS
                                 ORDER BY t.[Position] DESC) latest;

            IF @latest <> @after_position
            BEGIN
                SELECT CAST(0 AS bit) AS [Appended], @latest AS [LatestPosition];
                RETURN;
            END;
        END;

        DECLARE @affected_count int = (SELECT COUNT(*) FROM OPENJSON(@affected_tags));
        DECLARE @attempts int = 0;

        WHILE 1 = 1
        BEGIN
            BEGIN TRANSACTION;
            SAVE TRANSACTION memoria_dcb_append;

            UPDATE h SET [Token] = NEWID()
            FROM OPENJSON(@affected_tags) WITH ([Tag] nvarchar(255) ''$'') a
            JOIN [DcbTagHeads] h WITH (FORCESEEK, ROWLOCK) ON h.[Tag] = a.[Tag] COLLATE SQL_Latin1_General_CP1_CS_AS
            OPTION (FORCE ORDER, LOOP JOIN);

            IF @@ROWCOUNT = @affected_count BREAK;

            ROLLBACK TRANSACTION memoria_dcb_append;
            COMMIT TRANSACTION;

            SET @attempts += 1;
            IF @attempts > 10 THROW 50000, ''The tag heads for this append could not be created.'', 1;

            SET XACT_ABORT OFF;
            BEGIN TRY
                INSERT [DcbTagHeads] ([Tag], [Token])
                SELECT a.[Tag] COLLATE SQL_Latin1_General_CP1_CS_AS, NEWID()
                FROM OPENJSON(@affected_tags) WITH ([Tag] nvarchar(255) ''$'') a
                WHERE NOT EXISTS (SELECT 1 FROM [DcbTagHeads] h WITH (FORCESEEK)
                                  WHERE h.[Tag] = a.[Tag] COLLATE SQL_Latin1_General_CP1_CS_AS);
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
            END CATCH;
            SET XACT_ABORT ON;
        END;

        IF @boundary_tags IS NOT NULL
        BEGIN
                IF @boundary_is_intersection = 1
                    SELECT @latest = ISNULL(MAX(matched.[Position]), 0)
                    FROM (SELECT t.[Position] AS [Position]
                          FROM OPENJSON(@boundary_tags) WITH ([Tag] nvarchar(255) ''$'') b
                          JOIN [DcbEventTags] t WITH (FORCESEEK) ON t.[Tag] = b.[Tag] COLLATE SQL_Latin1_General_CP1_CS_AS
                          GROUP BY t.[Position]
                          HAVING COUNT(*) = @boundary_count) matched;
                ELSE
                    SELECT @latest = ISNULL(MAX(latest.[Position]), 0)
                    FROM OPENJSON(@boundary_tags) WITH ([Tag] nvarchar(255) ''$'') b
                    CROSS APPLY (SELECT TOP (1) t.[Position] AS [Position]
                                 FROM [DcbEventTags] t WITH (FORCESEEK)
                                 WHERE t.[Tag] = b.[Tag] COLLATE SQL_Latin1_General_CP1_CS_AS
                                 ORDER BY t.[Position] DESC) latest;

            IF @latest <> @after_position
            BEGIN
                ROLLBACK TRANSACTION memoria_dcb_append;
                COMMIT TRANSACTION;
                SELECT CAST(0 AS bit) AS [Appended], @latest AS [LatestPosition];
                RETURN;
            END;
        END;

        IF @count = 1
        BEGIN
            DECLARE @event_type nvarchar(255), @event_data nvarchar(max);
            SELECT @event_type = x.[type], @event_data = x.[data]
            FROM OPENJSON(@events) WITH ([type] nvarchar(255) ''$.type'', [data] nvarchar(max) ''$.data'') x;

            INSERT [DcbEvents] ([EventType], [Data], [CreatedDate], [CreatedBy])
            VALUES (@event_type, @event_data, @created_date, @created_by);

            SET @last = CAST(SCOPE_IDENTITY() AS bigint);

            INSERT [DcbEventTags] ([Position], [Tag])
            SELECT @last, tag.[Tag] COLLATE SQL_Latin1_General_CP1_CS_AS
            FROM OPENJSON(@events, ''$[0].tags'') WITH ([Tag] nvarchar(255) ''$'') tag
            OPTION (LOOP JOIN);
        END
        ELSE
        BEGIN
            DECLARE @positions TABLE ([Position] bigint PRIMARY KEY);

            INSERT [DcbEvents] ([EventType], [Data], [CreatedDate], [CreatedBy])
            OUTPUT INSERTED.[Position] INTO @positions ([Position])
            SELECT x.[type], x.[data], @created_date, @created_by
            FROM OPENJSON(@events) e
            CROSS APPLY OPENJSON(e.[value]) WITH ([type] nvarchar(255) ''$.type'', [data] nvarchar(max) ''$.data'') x
            ORDER BY CAST(e.[key] AS int);

            INSERT [DcbEventTags] ([Position], [Tag])
            SELECT p.[Position], tag.[Tag] COLLATE SQL_Latin1_General_CP1_CS_AS
            FROM (SELECT [Position], ROW_NUMBER() OVER (ORDER BY [Position]) - 1 AS [Ordinal] FROM @positions) p
            JOIN OPENJSON(@events) e ON CAST(e.[key] AS int) = p.[Ordinal]
            CROSS APPLY OPENJSON(e.[value], ''$.tags'') WITH ([Tag] nvarchar(255) ''$'') tag
            OPTION (LOOP JOIN);

            SET @last = (SELECT MAX([Position]) FROM @positions);
        END;
    END;

    IF @snapshot_id IS NOT NULL
    BEGIN
        DECLARE @snapshot_position bigint =
            CASE WHEN @last IS NULL OR @snapshot_latest_position > @last
                 THEN @snapshot_latest_position ELSE @last END;

                UPDATE s
                SET [SnapshotKind] = @snapshot_kind, [StoreId] = @snapshot_store_id,
                    [TagQuery] = @snapshot_tag_query, [ModelType] = @snapshot_model_type,
                    [Version] = @snapshot_version, [LatestPosition] = @snapshot_position,
                    [Data] = @snapshot_data, [UpdatedDate] = @created_date, [UpdatedBy] = @created_by
                FROM [DcbSnapshots] s WITH (FORCESEEK, ROWLOCK)
                WHERE s.[Id] = @snapshot_id AND s.[LatestPosition] <= @snapshot_position;

        IF @@ROWCOUNT = 0 AND NOT EXISTS (SELECT 1 FROM [DcbSnapshots] s WITH (FORCESEEK) WHERE s.[Id] = @snapshot_id)
        BEGIN
            SET XACT_ABORT OFF;
            BEGIN TRY
                INSERT [DcbSnapshots] ([Id], [SnapshotKind], [StoreId], [TagQuery], [ModelType], [Version],
                    [LatestPosition], [Data], [CreatedDate], [CreatedBy], [UpdatedDate], [UpdatedBy])
                VALUES (@snapshot_id, @snapshot_kind, @snapshot_store_id, @snapshot_tag_query,
                    @snapshot_model_type, @snapshot_version, @snapshot_position, @snapshot_data,
                    @created_date, @created_by, @created_date, @created_by);
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2601, 2627)
                BEGIN
                    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
                    THROW;
                END;

                UPDATE s
                SET [SnapshotKind] = @snapshot_kind, [StoreId] = @snapshot_store_id,
                    [TagQuery] = @snapshot_tag_query, [ModelType] = @snapshot_model_type,
                    [Version] = @snapshot_version, [LatestPosition] = @snapshot_position,
                    [Data] = @snapshot_data, [UpdatedDate] = @created_date, [UpdatedBy] = @created_by
                FROM [DcbSnapshots] s WITH (FORCESEEK, ROWLOCK)
                WHERE s.[Id] = @snapshot_id AND s.[LatestPosition] <= @snapshot_position;
            END CATCH;
            SET XACT_ABORT ON;
        END;
    END;

    COMMIT TRANSACTION;

    SELECT CAST(1 AS bit) AS [Appended], @last AS [LatestPosition];
END');
