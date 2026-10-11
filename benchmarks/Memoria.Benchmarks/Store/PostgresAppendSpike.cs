using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;
using Memoria.EventSourcing.Domain;
using Npgsql;
using NpgsqlTypes;

namespace Memoria.Benchmarks.Store;

/// <summary>
/// SPIKE: a DCB append on PostgreSQL as one call to a plpgsql function, instead of the four commands
/// plus commit the Entity Framework Core store sends.
/// </summary>
/// <remarks>
/// <para>
/// A function rather than one statement of data-modifying CTEs. Under READ COMMITTED a statement reads
/// from the snapshot it started with, so one that waited on a head row lock would still check the
/// boundary against the events visible before the wait — and pass when it should refuse. Inside a
/// volatile plpgsql function every statement takes a fresh snapshot, so the boundary is read after
/// the locks are held, and the call is still one round trip.
/// </para>
/// <para>
/// Pessimistic where the store is optimistic: it locks the head rows in tag order, then reads the
/// boundary. It still replaces every token, so an append made through the store that read a token
/// before this one committed is refused by its guarded update, exactly as before.
/// </para>
/// <para>
/// Spike limits: union boundaries only (no <c>AllOf</c>); the snapshot's serialized data carries the
/// position the caller had, while its <c>LatestPosition</c> column carries the new one (reads take the
/// column); no execution strategy or failure classification; locks are ordered by the C collation,
/// which matches the store's ordinal order for anything outside surrogate pairs.
/// </para>
/// </remarks>
public static class PostgresAppendSpike
{
    public const string InstallSql = """
        CREATE OR REPLACE FUNCTION memoria_dcb_append(
            p_affected_tags text[],
            p_boundary_tags text[],
            p_after_position bigint,
            p_event_types text[],
            p_event_data text[],
            p_tag_event_index int[],
            p_tag_values text[],
            p_now timestamptz,
            p_user text,
            p_snapshot_id text,
            p_snapshot_kind text,
            p_snapshot_store_id text,
            p_snapshot_tag_query text,
            p_snapshot_model_type text,
            p_snapshot_version int,
            p_snapshot_latest_position bigint,
            p_snapshot_data text)
        RETURNS TABLE (appended boolean, latest_position bigint)
        LANGUAGE plpgsql VOLATILE AS $fn$
        DECLARE
            v_latest bigint;
            v_positions bigint[];
            v_last bigint;
        BEGIN
            -- Early refusal, before any lock: a boundary that has already moved can only be refused,
            -- so a stale append should not queue behind the head locks to find that out. Not the
            -- check — the boundary can still move between here and the locks, so it is read again
            -- below with them held.
            IF p_boundary_tags IS NOT NULL THEN
                SELECT coalesce(max("Position"), 0) INTO v_latest
                FROM "DcbEventTags" WHERE "Tag" = ANY (p_boundary_tags);

                IF v_latest <> p_after_position THEN
                    RETURN QUERY SELECT false, v_latest;
                    RETURN;
                END IF;
            END IF;

            -- Heads for tags seen for the first time. Ordered, so two appends introducing the same
            -- tags wait on each other rather than deadlock.
            INSERT INTO "DcbTagHeads" ("Tag", "Token")
            SELECT tag, gen_random_uuid() FROM unnest(p_affected_tags) AS tag ORDER BY tag
            ON CONFLICT ("Tag") DO NOTHING;

            -- Every append contends on the heads of the tags it writes and the tags it is conditioned
            -- on, taken in one order.
            PERFORM 1 FROM "DcbTagHeads" WHERE "Tag" = ANY (p_affected_tags) ORDER BY "Tag" FOR UPDATE;

            -- A fresh snapshot, taken with the locks held: any append that could move this boundary
            -- has either committed already or is waiting behind us.
            IF p_boundary_tags IS NOT NULL THEN
                SELECT coalesce(max("Position"), 0) INTO v_latest
                FROM "DcbEventTags" WHERE "Tag" = ANY (p_boundary_tags);

                IF v_latest <> p_after_position THEN
                    RETURN QUERY SELECT false, v_latest;
                    RETURN;
                END IF;
            END IF;

            -- Replaced so that an optimistic append through the store, holding a token read before
            -- this commits, matches nothing and is refused.
            UPDATE "DcbTagHeads" SET "Token" = gen_random_uuid() WHERE "Tag" = ANY (p_affected_tags);

            SELECT array_agg(nextval(pg_get_serial_sequence('"DcbEvents"', 'Position')) ORDER BY i)
            INTO v_positions
            FROM generate_series(1, coalesce(cardinality(p_event_types), 0)) AS i;

            INSERT INTO "DcbEvents" ("Position", "EventType", "Data", "CreatedDate", "CreatedBy")
            SELECT position, event_type, data, p_now, p_user
            FROM unnest(v_positions, p_event_types, p_event_data) AS e (position, event_type, data);

            INSERT INTO "DcbEventTags" ("Position", "Tag")
            SELECT v_positions[event_index], tag
            FROM unnest(p_tag_event_index, p_tag_values) AS t (event_index, tag);

            v_last := v_positions[cardinality(v_positions)];

            IF p_snapshot_id IS NOT NULL THEN
                INSERT INTO "DcbSnapshots" ("Id", "SnapshotKind", "StoreId", "TagQuery", "ModelType",
                    "Version", "LatestPosition", "Data", "CreatedDate", "CreatedBy", "UpdatedDate", "UpdatedBy")
                VALUES (p_snapshot_id, p_snapshot_kind, p_snapshot_store_id, p_snapshot_tag_query,
                    p_snapshot_model_type, p_snapshot_version,
                    greatest(p_snapshot_latest_position, v_last), p_snapshot_data, p_now, p_user, p_now, p_user)
                ON CONFLICT ("Id") DO UPDATE SET
                    "SnapshotKind" = EXCLUDED."SnapshotKind",
                    "StoreId" = EXCLUDED."StoreId",
                    "TagQuery" = EXCLUDED."TagQuery",
                    "ModelType" = EXCLUDED."ModelType",
                    "Version" = EXCLUDED."Version",
                    "LatestPosition" = EXCLUDED."LatestPosition",
                    "Data" = EXCLUDED."Data",
                    "UpdatedDate" = EXCLUDED."UpdatedDate",
                    "UpdatedBy" = EXCLUDED."UpdatedBy";
            END IF;

            RETURN QUERY SELECT true, coalesce(v_last, p_after_position);
        END
        $fn$;
        """;

    public static async Task Install(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(InstallSql);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Saves an aggregate's uncommitted events and its snapshot in one round trip.</summary>
    /// <returns>Whether it was appended, and the boundary's latest position (the refusal's, or the last written).</returns>
    public static async Task<(bool Appended, long LatestPosition)> SaveAggregate<T>(NpgsqlDataSource dataSource,
        IDcbAggregateId<T> aggregateId, T aggregate, AppendCondition? condition, string? user,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot
    {
        var events = aggregate.UncommittedEvents.ToArray();

        var affectedTags = events
            .SelectMany(taggedEvent => taggedEvent.Tags)
            .Concat(condition?.Query.Tags ?? [])
            .Select(tag => tag.ToString())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[]? boundaryTags = null;
        if (condition is not null)
        {
            if (condition.Query.TagGroups.Any(group => group.Count != 1))
            {
                throw new NotSupportedException("Spike: union boundaries only.");
            }

            boundaryTags = condition.Query.Tags.Select(tag => tag.ToString()).ToArray();
        }

        var tagEventIndex = new List<int>();
        var tagValues = new List<string>();
        for (var i = 0; i < events.Length; i++)
        {
            foreach (var tag in events[i].Tags)
            {
                tagEventIndex.Add(i + 1);
                tagValues.Add(tag.ToString());
            }
        }

        var snapshot = aggregate.Version > 0 ? aggregate.ToSnapshotEntity(aggregateId) : null;

        await using var command = dataSource.CreateCommand(
            "SELECT appended, latest_position FROM memoria_dcb_append(" +
            "$1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17)");

        var parameters = command.Parameters;
        parameters.Add(new NpgsqlParameter { Value = affectedTags, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        parameters.Add(new NpgsqlParameter { Value = (object?)boundaryTags ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        parameters.Add(new NpgsqlParameter { Value = condition?.AfterPosition ?? AppendCondition.NoEvents, NpgsqlDbType = NpgsqlDbType.Bigint });
        parameters.Add(new NpgsqlParameter
        {
            Value = events.Select(e => TypeBindings.GetEventBindingKey(e.Event.GetType())).ToArray(),
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
        });
        parameters.Add(new NpgsqlParameter
        {
            Value = events.Select(e => DomainSerializer.Current.Serialize(e.Event)).ToArray(),
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
        });
        parameters.Add(new NpgsqlParameter { Value = tagEventIndex.ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Integer });
        parameters.Add(new NpgsqlParameter { Value = tagValues.ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
        parameters.Add(new NpgsqlParameter { Value = DateTimeOffset.UtcNow, NpgsqlDbType = NpgsqlDbType.TimestampTz });
        parameters.Add(Text(user));
        parameters.Add(Text(snapshot?.Id));
        parameters.Add(Text(snapshot?.SnapshotKind));
        parameters.Add(Text(snapshot?.StoreId));
        parameters.Add(Text(snapshot?.TagQuery));
        parameters.Add(Text(snapshot?.ModelType));
        parameters.Add(new NpgsqlParameter { Value = snapshot?.Version ?? 0, NpgsqlDbType = NpgsqlDbType.Integer });
        parameters.Add(new NpgsqlParameter { Value = snapshot?.LatestPosition ?? 0L, NpgsqlDbType = NpgsqlDbType.Bigint });
        parameters.Add(Text(snapshot?.Data));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        var appended = reader.GetBoolean(0);
        var latestPosition = reader.GetInt64(1);

        if (appended)
        {
            aggregate.LatestPosition = Math.Max(aggregate.LatestPosition, latestPosition);
        }

        return (appended, latestPosition);
    }

    private static NpgsqlParameter Text(string? value) =>
        new() { Value = (object?)value ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text };

}
