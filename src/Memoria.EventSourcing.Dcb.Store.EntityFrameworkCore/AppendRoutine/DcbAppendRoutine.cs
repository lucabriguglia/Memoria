using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.AppendRoutine;

/// <summary>
/// The database routine a DCB append goes through on engines that have one: the whole guarded
/// append — the boundary check, the tag head locks and the writes — in one round trip, where the
/// Entity Framework Core path sends a read, a batch of writes, another write and a commit.
/// </summary>
/// <remarks>
/// <para>
/// A function on PostgreSQL and a stored procedure on SQL Server. SQLite keeps the Entity Framework
/// Core path, because in process a round trip is almost free.
/// </para>
/// <para>
/// The SQL Server procedure covers a guarded append over a union boundary of the default model so
/// far; everything else on SQL Server keeps the Entity Framework Core path until it covers that too.
/// </para>
/// <para>
/// Generated from the context's model, so it writes wherever the model maps the store's tables and
/// columns, and casts payloads to whatever type the model gives them — the documented <c>jsonb</c>
/// override included. The default model's routine is <c>memoria_dcb_append_v1</c>, in the default
/// schema. Any other mapping gets that name with a hash of the mapping appended, in the schema of
/// its events table, so two contexts mapping different tables in one database never replace each
/// other's routine.
/// </para>
/// <para>
/// The name carries a version. A change to its parameters ships as a new name beside the old one,
/// never as a silent replacement of the old one's shape.
/// </para>
/// </remarks>
internal static class DcbAppendRoutine
{
    public const string Name = "memoria_dcb_append_v1";

    public const string PostgreSqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    public const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>The SQLSTATE PostgreSQL raises when a called function does not exist.</summary>
    public const string PostgreSqlUndefinedFunction = "42883";

    /// <summary>The error number SQL Server raises when a called procedure does not exist.</summary>
    public const int SqlServerUndefinedProcedure = 2812;

    /// <summary>What the default model maps, so its routine keeps the plain name.</summary>
    private static readonly string DefaultMapping = string.Join("|",
        "DcbEvents", "Position", "EventType", "Data", "text", "CreatedDate", "CreatedBy",
        "DcbEventTags", "Position", "Tag",
        "DcbTagHeads", "Tag", "Token",
        "DcbSnapshots", "Id", "SnapshotKind", "StoreId", "TagQuery", "ModelType", "Version", "LatestPosition",
        "Data", "text", "CreatedDate", "CreatedBy", "UpdatedDate", "UpdatedBy");

    /// <summary>The default model's table and column names, whatever their types.</summary>
    private static readonly string DefaultNames = DefaultMapping.Replace("|text|", "|");

    private static readonly ConditionalWeakTable<IModel, Shape> Shapes = new();

    private static readonly ConditionalWeakTable<IModel, Fitted> SqlServerShapes = new();

    /// <summary>
    /// The routine for this context's model, or null where appends go through Entity Framework Core.
    /// Built once per model.
    /// </summary>
    /// <remarks>
    /// The SQL Server procedure is built from the design-time model, because it compares tags under
    /// their columns' collation, which the runtime model does not keep.
    /// </remarks>
    public static Shape? For(DbContext context) => context.Database.ProviderName switch
    {
        PostgreSqlProvider => For(context.Model),
        SqlServerProvider => SqlServerShapes.GetValue(context.GetService<IDesignTimeModel>().Model, BuildSqlServer).Shape,
        _ => null
    };

    /// <summary>
    /// The PostgreSQL routine for a model: a context's runtime model, or a migration's target model.
    /// </summary>
    public static Shape For(IModel model) => Shapes.GetValue(model, Build);

    /// <summary>The engine a routine is written for, which decides how it is called.</summary>
    public enum Engine
    {
        PostgreSql,
        SqlServer
    }

    /// <summary>A model's routine.</summary>
    /// <param name="For">The engine it is written for.</param>
    /// <param name="CallName">The name to call it by, quoted and qualified as needed.</param>
    /// <param name="DisplayName">The name to tell a person, for instance in a failure.</param>
    /// <param name="Definition">The statement that creates or replaces it.</param>
    /// <param name="Drop">The statement that drops it, if it exists.</param>
    public sealed record Shape(Engine For, string CallName, string DisplayName, string Definition, string Drop);

    /// <summary>A model's routine, or null when the model is not one the engine's routine fits yet.</summary>
    private sealed record Fitted(Shape? Shape);

    /// <summary>The routine's parameter types, which identify it to <c>DROP FUNCTION</c>.</summary>
    private const string PostgreSqlSignature =
        "text[], text[], boolean, bigint, text[], text[], int[], text[], timestamptz, text, " +
        "text, text, text, text, text, int, bigint, text";

    /// <summary>The store's four tables as a model maps them.</summary>
    private sealed record StoreTables(MappedTable Events, MappedTable EventTags, MappedTable Heads, MappedTable Snapshots)
    {
        public static StoreTables Of(IModel model) => new(Mapped<DcbEventEntity>(model),
            Mapped<DcbEventTagEntity>(model), Mapped<DcbTagHeadEntity>(model), Mapped<DcbSnapshotEntity>(model));

        public bool AnySchema => Events.Schema is not null || EventTags.Schema is not null || Heads.Schema is not null
                                 || Snapshots.Schema is not null;

        /// <summary>Every table and column name, with the payload columns' types where they say so.</summary>
        public string Mapping(bool withPayloadTypes) => string.Join("|", new[]
        {
            Events.Table, Events.Raw(nameof(DcbEventEntity.Position)), Events.Raw(nameof(DcbEventEntity.EventType)),
            Events.Raw(nameof(DcbEventEntity.Data)),
            withPayloadTypes ? Events.Type(nameof(DcbEventEntity.Data)) : null,
            Events.Raw(nameof(DcbEventEntity.CreatedDate)), Events.Raw(nameof(DcbEventEntity.CreatedBy)),
            EventTags.Table, EventTags.Raw(nameof(DcbEventTagEntity.Position)), EventTags.Raw(nameof(DcbEventTagEntity.Tag)),
            Heads.Table, Heads.Raw(nameof(DcbTagHeadEntity.Tag)), Heads.Raw(nameof(DcbTagHeadEntity.Token)),
            Snapshots.Table, Snapshots.Raw(nameof(DcbSnapshotEntity.Id)), Snapshots.Raw(nameof(DcbSnapshotEntity.SnapshotKind)),
            Snapshots.Raw(nameof(DcbSnapshotEntity.StoreId)), Snapshots.Raw(nameof(DcbSnapshotEntity.TagQuery)),
            Snapshots.Raw(nameof(DcbSnapshotEntity.ModelType)), Snapshots.Raw(nameof(DcbSnapshotEntity.Version)),
            Snapshots.Raw(nameof(DcbSnapshotEntity.LatestPosition)), Snapshots.Raw(nameof(DcbSnapshotEntity.Data)),
            withPayloadTypes ? Snapshots.Type(nameof(DcbSnapshotEntity.Data)) : null,
            Snapshots.Raw(nameof(DcbSnapshotEntity.CreatedDate)), Snapshots.Raw(nameof(DcbSnapshotEntity.CreatedBy)),
            Snapshots.Raw(nameof(DcbSnapshotEntity.UpdatedDate)), Snapshots.Raw(nameof(DcbSnapshotEntity.UpdatedBy))
        }.Where(part => part is not null));
    }

    private static Shape Build(IModel model)
    {
        var tables = StoreTables.Of(model);
        var (events, eventTags, heads, snapshots) = tables;

        var eventsDataType = events.Type(nameof(DcbEventEntity.Data));
        var snapshotsDataType = snapshots.Type(nameof(DcbSnapshotEntity.Data));

        var mapping = tables.Mapping(withPayloadTypes: true);

        string callName;
        string displayName;

        if (mapping == DefaultMapping && !tables.AnySchema)
        {
            callName = Name;
            displayName = Name;
        }
        else
        {
            var name = $"{Name}_{Hash(mapping + "|" + events.Schema + "|" + eventTags.Schema + "|" + heads.Schema + "|" + snapshots.Schema)}";
            callName = Quote(name, events.Schema);
            displayName = events.Schema is null ? name : $"{events.Schema}.{name}";
        }

        return new Shape(Engine.PostgreSql, callName, displayName,
            PostgreSqlDefinition(callName, events, eventTags, heads, snapshots, eventsDataType, snapshotsDataType),
            $"DROP FUNCTION IF EXISTS {callName}({PostgreSqlSignature});");
    }

    /// <summary>
    /// The SQL Server procedure for a model, or none for a model that maps the store's tables
    /// anywhere but where the default model does: the procedure is written for the default names.
    /// </summary>
    private static Fitted BuildSqlServer(IModel model)
    {
        var tables = StoreTables.Of(model);

        if (tables.Mapping(withPayloadTypes: false) != DefaultNames || tables.AnySchema)
        {
            return new Fitted(null);
        }

        var callName = BracketQuote(Name);

        return new Fitted(new Shape(Engine.SqlServer, callName, Name, SqlServerDefinition(callName, tables),
            $"DROP PROCEDURE IF EXISTS {callName};"));
    }

    private static string Hash(string mapping) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(mapping)))[..8];

    /// <summary>
    /// Quotes a PostgreSQL identifier, always. The store's own rule rather than the provider's, which
    /// leaves simple lower-case names bare: one rule wherever the routine is generated — at runtime
    /// or in a migration, which has no provider services — keeps every route's routine identical.
    /// </summary>
    private static string Quote(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

    private static string Quote(string name, string? schema) =>
        schema is null ? Quote(name) : $"{Quote(schema)}.{Quote(name)}";

    /// <summary>One entity's table and columns as the model maps them.</summary>
    private sealed record MappedTable(IReadOnlyEntityType EntityType, StoreObjectIdentifier Store)
    {
        public string Table => Store.Name;

        public string? Schema => Store.Schema;

        /// <summary>The table, quoted and qualified.</summary>
        public string Quoted => Quote(Store.Name, Store.Schema);

        /// <summary>The column a property maps to, unquoted.</summary>
        public string Raw(string property) => EntityType.FindProperty(property)!.GetColumnName(Store)!;

        /// <summary>The column a property maps to, quoted.</summary>
        public string Column(string property) => Quote(Raw(property));

        /// <summary>The store type a property maps to.</summary>
        public string Type(string property) => EntityType.FindProperty(property)!.GetColumnType(Store)!;

        /// <summary>The table, bracketed for SQL Server.</summary>
        public string Bracketed => BracketQuote(Store.Name);

        /// <summary>The column a property maps to, bracketed for SQL Server.</summary>
        public string BracketedColumn(string property) => BracketQuote(Raw(property));

        /// <summary>
        /// A <c>COLLATE</c> clause for the collation a property's column is mapped with, or nothing
        /// where the model leaves the database default.
        /// </summary>
        public string CollateAs(string property) =>
            EntityType.FindProperty(property)!.GetCollation(Store) is { } collation ? $" COLLATE {collation}" : "";
    }

    /// <summary>Quotes a SQL Server identifier.</summary>
    private static string BracketQuote(string name) => $"[{name.Replace("]", "]]")}]";

    /// <remarks>
    /// Found by name rather than by CLR type: a migration's target model is rebuilt from the
    /// migration's own code, which names its entity types rather than referencing them.
    /// </remarks>
    private static MappedTable Mapped<TEntity>(IModel model)
    {
        var entityType = model.FindEntityType(typeof(TEntity).FullName!)
                         ?? throw new InvalidOperationException($"The model does not map {typeof(TEntity).Name}.");

        return new MappedTable(entityType, StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema()));
    }

    /// <summary>The SQL Server procedure.</summary>
    /// <remarks>
    /// <para>
    /// The same shape as the PostgreSQL function: refuse early without locks, lock the tag heads,
    /// read the boundary again with them held, then write. Each statement in a procedure reads
    /// afresh — under locking READ COMMITTED it takes its own locks, under READ_COMMITTED_SNAPSHOT it
    /// gets its own snapshot — so either way the read after the locks sees every append that held
    /// them first. It owns its transaction, so the call is one round trip with no separate BEGIN or
    /// COMMIT, and it answers with one row, as the function does.
    /// </para>
    /// <para>
    /// Collections arrive as JSON, read with typed <c>OPENJSON … WITH</c> rather than table-valued
    /// parameters, which would need user-defined table types and SqlClient's own parameter type.
    /// Typed to the tag column's type and collated to its collation, because a comparison with
    /// <c>OPENJSON</c>'s untyped <c>nvarchar(max)</c> cannot seek.
    /// </para>
    /// <para>
    /// <b>Seeks, never scans, on anything locked, and no <c>HOLDLOCK</c>.</b> A scanning update takes
    /// update locks on every row it reads — other appends' heads, held to their commit — and that
    /// deadlocked appends sharing nothing. A missing key under <c>HOLDLOCK</c> locks the gap around
    /// it, and appends introducing neighbouring tags deadlocked on those gaps. Hence
    /// <c>FORCESEEK</c> on every statement that locks, and nested loops for the foreign key check
    /// from the tags to the events, which the optimiser otherwise made a scan of the events.
    /// </para>
    /// <para>
    /// Heads usually exist, so the token replacement — which is the lock — comes first. Only when it
    /// finds fewer heads than tags are the missing ones created, outside the transaction, a duplicate
    /// key meaning another append created the head first, and the replacement tried again.
    /// </para>
    /// <para>
    /// <b>Nothing in tempdb on a one-event append</b>, the common case: its position comes from
    /// <c>SCOPE_IDENTITY()</c> and its payload through variables. Every call allocating in tempdb
    /// made disjoint writers queue on its allocation pages. A multi-event append keeps one table
    /// variable for the positions its insert generates: identity values follow an
    /// <c>INSERT … SELECT … ORDER BY</c>, so ranked they map back to the events.
    /// </para>
    /// </remarks>
    private static string SqlServerDefinition(string callName, StoreTables tables)
    {
        var (events, eventTags, heads, _) = tables;

        var e = events.Bracketed;
        var ePosition = events.BracketedColumn(nameof(DcbEventEntity.Position));
        var eType = events.BracketedColumn(nameof(DcbEventEntity.EventType));
        var eData = events.BracketedColumn(nameof(DcbEventEntity.Data));
        var eCreatedDate = events.BracketedColumn(nameof(DcbEventEntity.CreatedDate));
        var eCreatedBy = events.BracketedColumn(nameof(DcbEventEntity.CreatedBy));
        var eventTypeType = events.Type(nameof(DcbEventEntity.EventType));
        var eventDataType = events.Type(nameof(DcbEventEntity.Data));
        var createdByType = events.Type(nameof(DcbEventEntity.CreatedBy));

        var t = eventTags.Bracketed;
        var tPosition = eventTags.BracketedColumn(nameof(DcbEventTagEntity.Position));
        var tTag = eventTags.BracketedColumn(nameof(DcbEventTagEntity.Tag));
        var tagType = eventTags.Type(nameof(DcbEventTagEntity.Tag));
        var tCollate = eventTags.CollateAs(nameof(DcbEventTagEntity.Tag));

        var h = heads.Bracketed;
        var hTag = heads.BracketedColumn(nameof(DcbTagHeadEntity.Tag));
        var hToken = heads.BracketedColumn(nameof(DcbTagHeadEntity.Token));
        var hCollate = heads.CollateAs(nameof(DcbTagHeadEntity.Tag));

        var boundaryRead = $$"""
                    SELECT @latest = ISNULL(MAX(latest.[Position]), 0)
                    FROM OPENJSON(@boundary_tags) WITH ([Tag] {{tagType}} '$') b
                    CROSS APPLY (SELECT TOP (1) t.{{tPosition}} AS [Position]
                                 FROM {{t}} t WITH (FORCESEEK)
                                 WHERE t.{{tTag}} = b.[Tag]{{tCollate}}
                                 ORDER BY t.{{tPosition}} DESC) latest;
        """;

        return $$"""
            CREATE OR ALTER PROCEDURE {{callName}}
                @affected_tags nvarchar(max),
                @boundary_tags nvarchar(max),
                @after_position bigint,
                @events nvarchar(max),
                @created_date datetimeoffset,
                @created_by {{createdByType}}
            AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                DECLARE @latest bigint;

                IF @boundary_tags IS NOT NULL
                BEGIN
            {{boundaryRead}}

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

                    UPDATE h SET {{hToken}} = NEWID()
                    FROM OPENJSON(@affected_tags) WITH ([Tag] {{tagType}} '$') a
                    JOIN {{h}} h WITH (FORCESEEK, ROWLOCK) ON h.{{hTag}} = a.[Tag]{{hCollate}}
                    OPTION (FORCE ORDER, LOOP JOIN);

                    IF @@ROWCOUNT = @affected_count BREAK;

                    ROLLBACK TRANSACTION;

                    SET @attempts += 1;
                    IF @attempts > 10 THROW 50000, 'The tag heads for this append could not be created.', 1;

                    BEGIN TRY
                        INSERT {{h}} ({{hTag}}, {{hToken}})
                        SELECT a.[Tag]{{hCollate}}, NEWID()
                        FROM OPENJSON(@affected_tags) WITH ([Tag] {{tagType}} '$') a
                        WHERE NOT EXISTS (SELECT 1 FROM {{h}} h WITH (FORCESEEK)
                                          WHERE h.{{hTag}} = a.[Tag]{{hCollate}});
                    END TRY
                    BEGIN CATCH
                        IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
                    END CATCH;
                END;

                IF @boundary_tags IS NOT NULL
                BEGIN
            {{boundaryRead}}

                    IF @latest <> @after_position
                    BEGIN
                        ROLLBACK TRANSACTION;
                        SELECT CAST(0 AS bit) AS [Appended], @latest AS [LatestPosition];
                        RETURN;
                    END;
                END;

                DECLARE @count int = (SELECT COUNT(*) FROM OPENJSON(@events));
                DECLARE @last bigint;

                IF @count = 1
                BEGIN
                    DECLARE @event_type {{eventTypeType}}, @event_data {{eventDataType}};
                    SELECT @event_type = x.[type], @event_data = x.[data]
                    FROM OPENJSON(@events) WITH ([type] {{eventTypeType}} '$.type', [data] {{eventDataType}} '$.data') x;

                    INSERT {{e}} ({{eType}}, {{eData}}, {{eCreatedDate}}, {{eCreatedBy}})
                    VALUES (@event_type, @event_data, @created_date, @created_by);

                    SET @last = CAST(SCOPE_IDENTITY() AS bigint);

                    INSERT {{t}} ({{tPosition}}, {{tTag}})
                    SELECT @last, tag.[Tag]{{tCollate}}
                    FROM OPENJSON(@events, '$[0].tags') WITH ([Tag] {{tagType}} '$') tag
                    OPTION (LOOP JOIN);
                END
                ELSE
                BEGIN
                    DECLARE @positions TABLE ([Position] bigint PRIMARY KEY);

                    INSERT {{e}} ({{eType}}, {{eData}}, {{eCreatedDate}}, {{eCreatedBy}})
                    OUTPUT INSERTED.{{ePosition}} INTO @positions ([Position])
                    SELECT x.[type], x.[data], @created_date, @created_by
                    FROM OPENJSON(@events) e
                    CROSS APPLY OPENJSON(e.[value]) WITH ([type] {{eventTypeType}} '$.type', [data] {{eventDataType}} '$.data') x
                    ORDER BY CAST(e.[key] AS int);

                    INSERT {{t}} ({{tPosition}}, {{tTag}})
                    SELECT p.[Position], tag.[Tag]{{tCollate}}
                    FROM (SELECT [Position], ROW_NUMBER() OVER (ORDER BY [Position]) - 1 AS [Ordinal] FROM @positions) p
                    JOIN OPENJSON(@events) e ON CAST(e.[key] AS int) = p.[Ordinal]
                    CROSS APPLY OPENJSON(e.[value], '$.tags') WITH ([Tag] {{tagType}} '$') tag
                    OPTION (LOOP JOIN);

                    SET @last = (SELECT MAX([Position]) FROM @positions);
                END;

                COMMIT TRANSACTION;

                SELECT CAST(1 AS bit) AS [Appended], @last AS [LatestPosition];
            END
            """;
    }

    /// <summary>
    /// The PostgreSQL function, a plpgsql function rather than one statement of data-modifying CTEs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Under READ COMMITTED a single statement reads from the snapshot it started with, so one that
    /// waited on a tag head lock would still check the boundary against the events visible before the
    /// wait, and accept an append it should refuse. Inside a volatile plpgsql function every
    /// statement takes a fresh snapshot, so the boundary is read again once the locks are held.
    /// </para>
    /// <para>
    /// Pessimistic where the Entity Framework Core path is optimistic: it refuses early if the
    /// boundary has already moved, then locks the tag heads — in the caller's order, which is ordinal,
    /// so two appends over overlapping tags wait on each other instead of deadlocking — and reads the
    /// boundary again. It still replaces every token, so an append through the Entity Framework Core
    /// path that read a token before this one committed is refused by its guarded update.
    /// </para>
    /// <para>
    /// Positions are taken from the identity sequence in event order before the events are written,
    /// so each event's tags can be written against its own position.
    /// </para>
    /// <para>
    /// A boundary is either a union — events carrying any of its tags — or an intersection — events
    /// carrying every one of them, found as the positions with a row for each tag. With no boundary
    /// the append is unconditional: nothing is checked, but the heads are still locked and their
    /// tokens replaced, so it still refuses every conditioned append over its tags that read before it.
    /// </para>
    /// <para>
    /// A snapshot, when there is one, is written in the same transaction, at the later of the position
    /// the model had folded to and the last event written. It never replaces a snapshot that is
    /// further along: a save of an instance read before somebody else's save would otherwise put
    /// the boundary's snapshot back. With no events there is nothing for the condition to guard, so
    /// a snapshot-only save neither checks nor locks, as on the Entity Framework Core path.
    /// </para>
    /// <para>
    /// Payloads arrive as text and are cast to the type their columns are mapped to: there is no
    /// implicit assignment from text to <c>jsonb</c>.
    /// </para>
    /// </remarks>
    private static string PostgreSqlDefinition(string callName, MappedTable events, MappedTable eventTags,
        MappedTable heads, MappedTable snapshots, string eventsDataType, string snapshotsDataType)
    {
        var e = events.Quoted;
        var ePosition = events.Column(nameof(DcbEventEntity.Position));
        var eType = events.Column(nameof(DcbEventEntity.EventType));
        var eData = events.Column(nameof(DcbEventEntity.Data));
        var eCreatedDate = events.Column(nameof(DcbEventEntity.CreatedDate));
        var eCreatedBy = events.Column(nameof(DcbEventEntity.CreatedBy));
        var sequenceTable = e.Replace("'", "''");
        var sequenceColumn = events.Raw(nameof(DcbEventEntity.Position)).Replace("'", "''");

        var t = eventTags.Quoted;
        var tPosition = eventTags.Column(nameof(DcbEventTagEntity.Position));
        var tTag = eventTags.Column(nameof(DcbEventTagEntity.Tag));

        var h = heads.Quoted;
        var hTag = heads.Column(nameof(DcbTagHeadEntity.Tag));
        var hToken = heads.Column(nameof(DcbTagHeadEntity.Token));

        var s = snapshots.Quoted;
        var sId = snapshots.Column(nameof(DcbSnapshotEntity.Id));
        var sKind = snapshots.Column(nameof(DcbSnapshotEntity.SnapshotKind));
        var sStoreId = snapshots.Column(nameof(DcbSnapshotEntity.StoreId));
        var sTagQuery = snapshots.Column(nameof(DcbSnapshotEntity.TagQuery));
        var sModelType = snapshots.Column(nameof(DcbSnapshotEntity.ModelType));
        var sVersion = snapshots.Column(nameof(DcbSnapshotEntity.Version));
        var sLatestPosition = snapshots.Column(nameof(DcbSnapshotEntity.LatestPosition));
        var sData = snapshots.Column(nameof(DcbSnapshotEntity.Data));
        var sCreatedDate = snapshots.Column(nameof(DcbSnapshotEntity.CreatedDate));
        var sCreatedBy = snapshots.Column(nameof(DcbSnapshotEntity.CreatedBy));
        var sUpdatedDate = snapshots.Column(nameof(DcbSnapshotEntity.UpdatedDate));
        var sUpdatedBy = snapshots.Column(nameof(DcbSnapshotEntity.UpdatedBy));

        var boundaryRead = $$"""
                    IF p_boundary_is_intersection THEN
                        SELECT coalesce(max(matched.position), 0) INTO v_latest
                        FROM (SELECT {{tPosition}} AS position FROM {{t}}
                              WHERE {{tTag}} = ANY (p_boundary_tags)
                              GROUP BY {{tPosition}}
                              HAVING count(*) = cardinality(p_boundary_tags)) AS matched;
                    ELSE
                        SELECT coalesce(max({{tPosition}}), 0) INTO v_latest
                        FROM {{t}} WHERE {{tTag}} = ANY (p_boundary_tags);
                    END IF;
        """;

        return $$"""
            CREATE OR REPLACE FUNCTION {{callName}}(
                p_affected_tags text[],
                p_boundary_tags text[],
                p_boundary_is_intersection boolean,
                p_after_position bigint,
                p_event_types text[],
                p_event_data text[],
                p_tag_event_index int[],
                p_tag_values text[],
                p_created_date timestamptz,
                p_created_by text,
                p_snapshot_id text,
                p_snapshot_kind text,
                p_snapshot_store_id text,
                p_snapshot_tag_query text,
                p_snapshot_model_type text,
                p_snapshot_version int,
                p_snapshot_latest_position bigint,
                p_snapshot_data text)
            RETURNS TABLE (appended boolean, latest_position bigint)
            LANGUAGE plpgsql VOLATILE AS $memoria$
            DECLARE
                v_latest bigint;
                v_positions bigint[];
                v_last bigint;
            BEGIN
                IF cardinality(p_event_types) > 0 THEN
                    IF p_boundary_tags IS NOT NULL THEN
            {{boundaryRead}}

                        IF v_latest <> p_after_position THEN
                            RETURN QUERY SELECT false, v_latest;
                            RETURN;
                        END IF;
                    END IF;

                    INSERT INTO {{h}} ({{hTag}}, {{hToken}})
                    SELECT affected.tag, gen_random_uuid()
                    FROM unnest(p_affected_tags) WITH ORDINALITY AS affected (tag, ordinal)
                    ORDER BY affected.ordinal
                    ON CONFLICT ({{hTag}}) DO NOTHING;

                    PERFORM 1
                    FROM unnest(p_affected_tags) WITH ORDINALITY AS affected (tag, ordinal)
                    JOIN {{h}} head ON head.{{hTag}} = affected.tag
                    ORDER BY affected.ordinal
                    FOR UPDATE OF head;

                    IF p_boundary_tags IS NOT NULL THEN
            {{boundaryRead}}

                        IF v_latest <> p_after_position THEN
                            RETURN QUERY SELECT false, v_latest;
                            RETURN;
                        END IF;
                    END IF;

                    UPDATE {{h}} SET {{hToken}} = gen_random_uuid() WHERE {{hTag}} = ANY (p_affected_tags);

                    SELECT array_agg(nextval(pg_get_serial_sequence('{{sequenceTable}}', '{{sequenceColumn}}')) ORDER BY event)
                    INTO v_positions
                    FROM generate_series(1, cardinality(p_event_types)) AS event;

                    INSERT INTO {{e}} ({{ePosition}}, {{eType}}, {{eData}}, {{eCreatedDate}}, {{eCreatedBy}})
                    SELECT written.position, written.event_type, written.data::{{eventsDataType}}, p_created_date, p_created_by
                    FROM unnest(v_positions, p_event_types, p_event_data) AS written (position, event_type, data);

                    INSERT INTO {{t}} ({{tPosition}}, {{tTag}})
                    SELECT v_positions[tagged.event], tagged.tag
                    FROM unnest(p_tag_event_index, p_tag_values) AS tagged (event, tag);

                    v_last := v_positions[cardinality(v_positions)];
                END IF;

                IF p_snapshot_id IS NOT NULL THEN
                    INSERT INTO {{s}} AS existing ({{sId}}, {{sKind}}, {{sStoreId}}, {{sTagQuery}}, {{sModelType}}, {{sVersion}},
                        {{sLatestPosition}}, {{sData}}, {{sCreatedDate}}, {{sCreatedBy}}, {{sUpdatedDate}}, {{sUpdatedBy}})
                    VALUES (p_snapshot_id, p_snapshot_kind, p_snapshot_store_id, p_snapshot_tag_query,
                        p_snapshot_model_type, p_snapshot_version, greatest(p_snapshot_latest_position, v_last),
                        p_snapshot_data::{{snapshotsDataType}}, p_created_date, p_created_by, p_created_date, p_created_by)
                    ON CONFLICT ({{sId}}) DO UPDATE SET
                        {{sKind}} = EXCLUDED.{{sKind}},
                        {{sStoreId}} = EXCLUDED.{{sStoreId}},
                        {{sTagQuery}} = EXCLUDED.{{sTagQuery}},
                        {{sModelType}} = EXCLUDED.{{sModelType}},
                        {{sVersion}} = EXCLUDED.{{sVersion}},
                        {{sLatestPosition}} = EXCLUDED.{{sLatestPosition}},
                        {{sData}} = EXCLUDED.{{sData}},
                        {{sUpdatedDate}} = EXCLUDED.{{sUpdatedDate}},
                        {{sUpdatedBy}} = EXCLUDED.{{sUpdatedBy}}
                    WHERE existing.{{sLatestPosition}} <= EXCLUDED.{{sLatestPosition}};
                END IF;

                RETURN QUERY SELECT true, v_last;
            END
            $memoria$;
            """;
    }
}
