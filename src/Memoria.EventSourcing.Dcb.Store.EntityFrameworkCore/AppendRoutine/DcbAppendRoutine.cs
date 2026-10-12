using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.AppendRoutine;

/// <summary>
/// The database routine a DCB append goes through on engines that have one: the whole guarded
/// append — the boundary check, the tag head locks and the writes — in one round trip, where the
/// Entity Framework Core path sends a read, a batch of writes, another write and a commit.
/// </summary>
/// <remarks>
/// <para>
/// PostgreSQL only, for now. SQL Server will follow with a stored procedure; SQLite keeps the Entity
/// Framework Core path, because in process a round trip is almost free.
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

    /// <summary>The SQLSTATE PostgreSQL raises when a called function does not exist.</summary>
    public const string PostgreSqlUndefinedFunction = "42883";

    /// <summary>What the default model maps, so its routine keeps the plain name.</summary>
    private static readonly string DefaultMapping = string.Join("|",
        "DcbEvents", "Position", "EventType", "Data", "text", "CreatedDate", "CreatedBy",
        "DcbEventTags", "Position", "Tag",
        "DcbTagHeads", "Tag", "Token",
        "DcbSnapshots", "Id", "SnapshotKind", "StoreId", "TagQuery", "ModelType", "Version", "LatestPosition",
        "Data", "text", "CreatedDate", "CreatedBy", "UpdatedDate", "UpdatedBy");

    private static readonly ConditionalWeakTable<IModel, Shape> Shapes = new();

    /// <summary>
    /// The routine for this context's model, or null where appends go through Entity Framework Core.
    /// Built once per model.
    /// </summary>
    public static Shape? For(DbContext context) =>
        context.Database.ProviderName == PostgreSqlProvider
            ? Shapes.GetValue(context.Model, model => Build(model, context.GetService<ISqlGenerationHelper>()))
            : null;

    /// <summary>A model's routine.</summary>
    /// <param name="CallName">The name to call it by, quoted and qualified as needed.</param>
    /// <param name="DisplayName">The name to tell a person, for instance in a failure.</param>
    /// <param name="Definition">The statement that creates or replaces it.</param>
    public sealed record Shape(string CallName, string DisplayName, string Definition);

    private static Shape Build(IModel model, ISqlGenerationHelper sql)
    {
        var events = Mapped<DcbEventEntity>(model, sql);
        var eventTags = Mapped<DcbEventTagEntity>(model, sql);
        var heads = Mapped<DcbTagHeadEntity>(model, sql);
        var snapshots = Mapped<DcbSnapshotEntity>(model, sql);

        var eventsDataType = events.Type(nameof(DcbEventEntity.Data));
        var snapshotsDataType = snapshots.Type(nameof(DcbSnapshotEntity.Data));

        var mapping = string.Join("|",
            events.Table, events.Raw(nameof(DcbEventEntity.Position)), events.Raw(nameof(DcbEventEntity.EventType)),
            events.Raw(nameof(DcbEventEntity.Data)), eventsDataType, events.Raw(nameof(DcbEventEntity.CreatedDate)),
            events.Raw(nameof(DcbEventEntity.CreatedBy)),
            eventTags.Table, eventTags.Raw(nameof(DcbEventTagEntity.Position)), eventTags.Raw(nameof(DcbEventTagEntity.Tag)),
            heads.Table, heads.Raw(nameof(DcbTagHeadEntity.Tag)), heads.Raw(nameof(DcbTagHeadEntity.Token)),
            snapshots.Table, snapshots.Raw(nameof(DcbSnapshotEntity.Id)), snapshots.Raw(nameof(DcbSnapshotEntity.SnapshotKind)),
            snapshots.Raw(nameof(DcbSnapshotEntity.StoreId)), snapshots.Raw(nameof(DcbSnapshotEntity.TagQuery)),
            snapshots.Raw(nameof(DcbSnapshotEntity.ModelType)), snapshots.Raw(nameof(DcbSnapshotEntity.Version)),
            snapshots.Raw(nameof(DcbSnapshotEntity.LatestPosition)), snapshots.Raw(nameof(DcbSnapshotEntity.Data)),
            snapshotsDataType, snapshots.Raw(nameof(DcbSnapshotEntity.CreatedDate)),
            snapshots.Raw(nameof(DcbSnapshotEntity.CreatedBy)), snapshots.Raw(nameof(DcbSnapshotEntity.UpdatedDate)),
            snapshots.Raw(nameof(DcbSnapshotEntity.UpdatedBy)));

        string callName;
        string displayName;

        if (mapping == DefaultMapping && events.Schema is null && eventTags.Schema is null && heads.Schema is null
            && snapshots.Schema is null)
        {
            callName = Name;
            displayName = Name;
        }
        else
        {
            var name = $"{Name}_{Hash(mapping + "|" + events.Schema + "|" + eventTags.Schema + "|" + heads.Schema + "|" + snapshots.Schema)}";
            callName = sql.DelimitIdentifier(name, events.Schema);
            displayName = events.Schema is null ? name : $"{events.Schema}.{name}";
        }

        return new Shape(callName, displayName, PostgreSqlDefinition(callName, events, eventTags, heads, snapshots,
            eventsDataType, snapshotsDataType));
    }

    private static string Hash(string mapping) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(mapping)))[..8];

    /// <summary>One entity's table and columns as the model maps them.</summary>
    private sealed record MappedTable(IEntityType EntityType, StoreObjectIdentifier Store, ISqlGenerationHelper Sql)
    {
        public string Table => Store.Name;

        public string? Schema => Store.Schema;

        /// <summary>The table, quoted and qualified.</summary>
        public string Quoted => Sql.DelimitIdentifier(Store.Name, Store.Schema);

        /// <summary>The column a property maps to, unquoted.</summary>
        public string Raw(string property) => EntityType.FindProperty(property)!.GetColumnName(Store)!;

        /// <summary>The column a property maps to, quoted.</summary>
        public string Column(string property) => Sql.DelimitIdentifier(Raw(property));

        /// <summary>The store type a property maps to.</summary>
        public string Type(string property) => EntityType.FindProperty(property)!.GetColumnType(Store)!;
    }

    private static MappedTable Mapped<TEntity>(IModel model, ISqlGenerationHelper sql)
    {
        var entityType = model.FindEntityType(typeof(TEntity))
                         ?? throw new InvalidOperationException($"The model does not map {typeof(TEntity).Name}.");

        return new MappedTable(entityType, StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema()),
            sql);
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
