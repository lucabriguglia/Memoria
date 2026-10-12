using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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

    /// <summary>
    /// Whether the routine fits this model: the tables it writes under their default names and
    /// schema, and the event payload as <c>text</c>.
    /// </summary>
    /// <remarks>
    /// The routine is written for the default model. A context that maps a table elsewhere, or
    /// stores payloads as <c>jsonb</c> — a documented override — keeps the Entity Framework Core path
    /// rather than calling a routine that would write to the wrong place or the wrong type.
    /// </remarks>
    public static bool FitsModel(IModel model) =>
        IsDefaultTable<DcbEventEntity>(model, "DcbEvents")
        && IsDefaultTable<DcbEventTagEntity>(model, "DcbEventTags")
        && IsDefaultTable<DcbTagHeadEntity>(model, "DcbTagHeads")
        && model.FindEntityType(typeof(DcbEventEntity))?
            .FindProperty(nameof(DcbEventEntity.Data))?.GetColumnType() == "text";

    private static bool IsDefaultTable<TEntity>(IModel model, string table) =>
        model.FindEntityType(typeof(TEntity)) is { } entityType
        && entityType.GetTableName() == table
        && entityType.GetSchema() is null or "public";

    /// <summary>
    /// The routine's definition for the given provider, or null where appends go through Entity
    /// Framework Core.
    /// </summary>
    public static string? Definition(string? providerName) =>
        providerName == PostgreSqlProvider ? PostgreSqlDefinition : null;

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
    /// </remarks>
    private const string PostgreSqlDefinition = """
        CREATE OR REPLACE FUNCTION memoria_dcb_append_v1(
            p_affected_tags text[],
            p_boundary_tags text[],
            p_after_position bigint,
            p_event_types text[],
            p_event_data text[],
            p_tag_event_index int[],
            p_tag_values text[],
            p_created_date timestamptz,
            p_created_by text)
        RETURNS TABLE (appended boolean, latest_position bigint)
        LANGUAGE plpgsql VOLATILE AS $memoria$
        DECLARE
            v_latest bigint;
            v_positions bigint[];
        BEGIN
            IF p_boundary_tags IS NOT NULL THEN
                SELECT coalesce(max("Position"), 0) INTO v_latest
                FROM "DcbEventTags" WHERE "Tag" = ANY (p_boundary_tags);

                IF v_latest <> p_after_position THEN
                    RETURN QUERY SELECT false, v_latest;
                    RETURN;
                END IF;
            END IF;

            INSERT INTO "DcbTagHeads" ("Tag", "Token")
            SELECT affected.tag, gen_random_uuid()
            FROM unnest(p_affected_tags) WITH ORDINALITY AS affected (tag, ordinal)
            ORDER BY affected.ordinal
            ON CONFLICT ("Tag") DO NOTHING;

            PERFORM 1
            FROM unnest(p_affected_tags) WITH ORDINALITY AS affected (tag, ordinal)
            JOIN "DcbTagHeads" head ON head."Tag" = affected.tag
            ORDER BY affected.ordinal
            FOR UPDATE OF head;

            IF p_boundary_tags IS NOT NULL THEN
                SELECT coalesce(max("Position"), 0) INTO v_latest
                FROM "DcbEventTags" WHERE "Tag" = ANY (p_boundary_tags);

                IF v_latest <> p_after_position THEN
                    RETURN QUERY SELECT false, v_latest;
                    RETURN;
                END IF;
            END IF;

            UPDATE "DcbTagHeads" SET "Token" = gen_random_uuid() WHERE "Tag" = ANY (p_affected_tags);

            SELECT array_agg(nextval(pg_get_serial_sequence('"DcbEvents"', 'Position')) ORDER BY event)
            INTO v_positions
            FROM generate_series(1, cardinality(p_event_types)) AS event;

            INSERT INTO "DcbEvents" ("Position", "EventType", "Data", "CreatedDate", "CreatedBy")
            SELECT written.position, written.event_type, written.data, p_created_date, p_created_by
            FROM unnest(v_positions, p_event_types, p_event_data) AS written (position, event_type, data);

            INSERT INTO "DcbEventTags" ("Position", "Tag")
            SELECT v_positions[tagged.event], tagged.tag
            FROM unnest(p_tag_event_index, p_tag_values) AS tagged (event, tag);

            RETURN QUERY SELECT true, v_positions[cardinality(v_positions)];
        END
        $memoria$;
        """;
}
