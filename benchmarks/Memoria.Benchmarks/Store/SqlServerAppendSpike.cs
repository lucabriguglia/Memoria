using System.Data;
using System.Text.Json;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;
using Memoria.EventSourcing.Domain;
using Microsoft.Data.SqlClient;

namespace Memoria.Benchmarks.Store;

/// <summary>
/// SPIKE: a DCB append on SQL Server as one call to a stored procedure, the counterpart of
/// <see cref="PostgresAppendSpike"/>.
/// </summary>
/// <remarks>
/// <para>
/// Same shape as the PostgreSQL function: refuse early without locks, create missing heads, lock the
/// heads, read the boundary again with them held, then write. The procedure owns its transaction,
/// so the call is one RPC: no separate BEGIN or COMMIT requests, which the store's path pays for on
/// SqlClient.
/// </para>
/// <para>
/// Collections travel as JSON in <c>nvarchar(max)</c> parameters, read with typed
/// <c>OPENJSON … WITH</c> (SQL Server 2016+), rather than as table-valued parameters, which would
/// need user-defined table types and SqlClient-specific parameters. Typed, because comparing a tag
/// column with <c>OPENJSON</c>'s untyped <c>nvarchar(max)</c> value cannot seek. Every tag is
/// collated to the tag columns' case-sensitive collation before it is compared.
/// </para>
/// <para>
/// <b>No tempdb on the common path.</b> The first version unpacked the JSON into four table
/// variables; every call then allocated in tempdb, and on LocalDB's single tempdb file sixteen
/// disjoint writers queued on its allocation pages (PAGELATCH_UP on 2:1:1 and 2:1:3) at ~1,000
/// appends/s. Tags are now read straight from OPENJSON, and a one-event append takes its position
/// from SCOPE_IDENTITY(). Only a multi-event append keeps one table variable, for the positions
/// its INSERT generates (a variable JSON path, which would avoid it, needs SQL Server 2017).
/// </para>
/// <para>
/// <b>Seeks, never scans, on anything locked.</b> On small tables the optimiser preferred scans,
/// and a scanning UPDATE takes update locks on every row it reads: other appends' heads and
/// snapshots, held to their commit. That deadlocked disjoint appends. Hence FORCESEEK on every
/// statement that locks, and no HOLDLOCK anywhere: a missing key under HOLDLOCK locks the gap
/// around it, and two appends introducing neighbouring tags deadlocked on those gaps.
/// </para>
/// <para>
/// Statements in a procedure each read afresh: under locking READ COMMITTED they take their own
/// locks, and under READ_COMMITTED_SNAPSHOT each statement gets its own snapshot. Either way the
/// boundary read that follows the head locks sees every append that held them before. Checked with
/// both settings.
/// </para>
/// <para>
/// Spike limits as for PostgreSQL: union boundaries only; a caller's open transaction is not
/// joined.
/// </para>
/// </remarks>
public static class SqlServerAppendSpike
{
    private const string Collation = "SQL_Latin1_General_CP1_CS_AS";

    public const string InstallSql = $$"""
        CREATE OR ALTER PROCEDURE [dbo].[memoria_dcb_append_v1]
            @affected_tags nvarchar(max),
            @boundary_tags nvarchar(max),
            @after_position bigint,
            @events nvarchar(max),
            @now datetimeoffset,
            @user nvarchar(255),
            @snapshot_id nvarchar(400),
            @snapshot_kind nvarchar(20),
            @snapshot_store_id nvarchar(255),
            @snapshot_tag_query nvarchar(max),
            @snapshot_model_type nvarchar(255),
            @snapshot_version int,
            @snapshot_latest_position bigint,
            @snapshot_data nvarchar(max),
            @appended bit OUTPUT,
            @latest_position bigint OUTPUT
        AS
        BEGIN
            SET NOCOUNT ON;
            SET XACT_ABORT ON;

            -- Early refusal, before any lock.
            IF @boundary_tags IS NOT NULL
            BEGIN
                SELECT @latest_position = ISNULL(MAX(t.[Position]), 0)
                FROM OPENJSON(@boundary_tags) WITH ([Tag] nvarchar(255) '$') b
                JOIN [dbo].[DcbEventTags] t WITH (FORCESEEK) ON t.[Tag] = b.[Tag] COLLATE {{Collation}};

                IF @latest_position <> @after_position
                BEGIN
                    SET @appended = 0;
                    RETURN;
                END;
            END;

            -- Lock every head this append contends on by replacing its token: X locks held to
            -- commit, and the replacement any optimistic append over these tags is guarded against.
            -- Taken in the caller's (ordinal) order: the JSON array drives a forced nested loop.
            --
            -- Heads usually exist, so the update comes first and touches the heads page once. Only
            -- when it finds fewer heads than tags are the missing ones created - outside the
            -- transaction and without range locks, a duplicate key meaning another append created
            -- the head first - and the update tried again. Probing for missing heads on every append
            -- doubled the latching on the heads page, which sixteen writers queued on.
            DECLARE @affected_count int = (SELECT COUNT(*) FROM OPENJSON(@affected_tags));
            DECLARE @attempts int = 0;

            WHILE 1 = 1
            BEGIN
                BEGIN TRANSACTION;

                UPDATE h SET [Token] = NEWID()
                FROM OPENJSON(@affected_tags) WITH ([Tag] nvarchar(255) '$') a
                JOIN [dbo].[DcbTagHeads] h WITH (FORCESEEK, ROWLOCK) ON h.[Tag] = a.[Tag] COLLATE {{Collation}}
                OPTION (FORCE ORDER, LOOP JOIN);

                IF @@ROWCOUNT = @affected_count BREAK;

                ROLLBACK TRANSACTION;

                SET @attempts += 1;
                IF @attempts > 10 THROW 50000, 'Tag heads could not be created.', 1;

                BEGIN TRY
                    INSERT [dbo].[DcbTagHeads] ([Tag], [Token])
                    SELECT a.[Tag] COLLATE {{Collation}}, NEWID()
                    FROM OPENJSON(@affected_tags) WITH ([Tag] nvarchar(255) '$') a
                    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[DcbTagHeads] h
                                      WHERE h.[Tag] = a.[Tag] COLLATE {{Collation}});
                END TRY
                BEGIN CATCH
                    IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
                END CATCH;
            END;

            -- The check: a fresh read with the locks held.
            IF @boundary_tags IS NOT NULL
            BEGIN
                SELECT @latest_position = ISNULL(MAX(t.[Position]), 0)
                FROM OPENJSON(@boundary_tags) WITH ([Tag] nvarchar(255) '$') b
                JOIN [dbo].[DcbEventTags] t WITH (FORCESEEK) ON t.[Tag] = b.[Tag] COLLATE {{Collation}};

                IF @latest_position <> @after_position
                BEGIN
                    ROLLBACK TRANSACTION;
                    SET @appended = 0;
                    RETURN;
                END;
            END;

            DECLARE @count int = (SELECT COUNT(*) FROM OPENJSON(@events));
            DECLARE @last bigint;

            IF @count = 1
            BEGIN
                -- The common case, with nothing in tempdb. Through variables rather than INSERT … SELECT
                -- FROM OPENJSON: that shape staged the nvarchar(max) payload in tempdb, and sixteen
                -- writers queued on its allocation page (PAGELATCH_UP 2:1:1).
                DECLARE @event_type nvarchar(255), @event_data nvarchar(max);
                SELECT @event_type = x.[type], @event_data = x.[data]
                FROM OPENJSON(@events) WITH ([type] nvarchar(255) '$.type', [data] nvarchar(max) '$.data') x;

                INSERT [dbo].[DcbEvents] ([EventType], [Data], [CreatedDate], [CreatedBy])
                VALUES (@event_type, @event_data, @now, @user);

                SET @last = CAST(SCOPE_IDENTITY() AS bigint);

                INSERT [dbo].[DcbEventTags] ([Position], [Tag])
                SELECT @last, t.[Tag] COLLATE {{Collation}}
                FROM OPENJSON(@events) WITH ([tags] nvarchar(max) '$.tags' AS JSON) e
                CROSS APPLY OPENJSON(e.[tags]) WITH ([Tag] nvarchar(255) '$') t
                -- The foreign key check reads DcbEvents. OPENJSON's row estimate made the optimiser
                -- scan it, which waits on other appends' uncommitted events and deadlocked them;
                -- nested loops make it a seek on this event's own row.
                OPTION (LOOP JOIN);
            END
            ELSE IF @count > 1
            BEGIN
                -- INSERT … SELECT … ORDER BY guarantees identity values follow the ORDER BY, so the
                -- generated positions, ranked, map back to the events' ordinals.
                DECLARE @positions TABLE ([Position] bigint PRIMARY KEY);

                INSERT [dbo].[DcbEvents] ([EventType], [Data], [CreatedDate], [CreatedBy])
                OUTPUT INSERTED.[Position] INTO @positions ([Position])
                SELECT x.[type], x.[data], @now, @user
                FROM OPENJSON(@events) e
                CROSS APPLY OPENJSON(e.[value]) WITH ([type] nvarchar(255) '$.type', [data] nvarchar(max) '$.data') x
                ORDER BY CAST(e.[key] AS int);

                INSERT [dbo].[DcbEventTags] ([Position], [Tag])
                SELECT p.[Position], t.[Tag] COLLATE {{Collation}}
                FROM (SELECT [Position], ROW_NUMBER() OVER (ORDER BY [Position]) - 1 AS [Ordinal] FROM @positions) p
                JOIN OPENJSON(@events) e ON CAST(e.[key] AS int) = p.[Ordinal]
                CROSS APPLY OPENJSON(e.[value], '$.tags') WITH ([Tag] nvarchar(255) '$') t
                OPTION (LOOP JOIN);

                SET @last = (SELECT MAX([Position]) FROM @positions);
            END;

            IF @snapshot_id IS NOT NULL
            BEGIN
                DECLARE @snapshot_position bigint =
                    CASE WHEN @last IS NULL OR @snapshot_latest_position > @last
                         THEN @snapshot_latest_position ELSE @last END;

                -- No HOLDLOCK, for the same reason as the heads. Appends to one boundary are already
                -- serialised by its head locks; the race left is a read writing a snapshot back, and
                -- a duplicate key from that falls back to the update. XACT_ABORT is off for the
                -- insert so that error ends the statement, not the transaction.
                UPDATE s
                SET [SnapshotKind] = @snapshot_kind, [StoreId] = @snapshot_store_id,
                    [TagQuery] = @snapshot_tag_query, [ModelType] = @snapshot_model_type,
                    [Version] = @snapshot_version, [LatestPosition] = @snapshot_position,
                    [Data] = @snapshot_data, [UpdatedDate] = @now, [UpdatedBy] = @user
                FROM [dbo].[DcbSnapshots] s WITH (FORCESEEK, ROWLOCK)
                WHERE s.[Id] = @snapshot_id;

                IF @@ROWCOUNT = 0
                BEGIN
                    SET XACT_ABORT OFF;
                    BEGIN TRY
                        INSERT [dbo].[DcbSnapshots] ([Id], [SnapshotKind], [StoreId], [TagQuery], [ModelType],
                            [Version], [LatestPosition], [Data], [CreatedDate], [CreatedBy], [UpdatedDate], [UpdatedBy])
                        VALUES (@snapshot_id, @snapshot_kind, @snapshot_store_id, @snapshot_tag_query,
                            @snapshot_model_type, @snapshot_version, @snapshot_position, @snapshot_data,
                            @now, @user, @now, @user);
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
                            [Data] = @snapshot_data, [UpdatedDate] = @now, [UpdatedBy] = @user
                        FROM [dbo].[DcbSnapshots] s WITH (FORCESEEK, ROWLOCK)
                        WHERE s.[Id] = @snapshot_id;
                    END CATCH;
                    SET XACT_ABORT ON;
                END;
            END;

            COMMIT TRANSACTION;

            SET @appended = 1;
            SET @latest_position = ISNULL(@last, @after_position);
        END
        """;

    public static async Task Install(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(InstallSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Saves an aggregate's uncommitted events and its snapshot in one round trip.</summary>
    public static async Task<(bool Appended, long LatestPosition)> SaveAggregate<T>(string connectionString,
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

        string? boundaryTags = null;
        if (condition is not null)
        {
            if (condition.Query.TagGroups.Any(group => group.Count != 1))
            {
                throw new NotSupportedException("Spike: union boundaries only.");
            }

            boundaryTags = JsonSerializer.Serialize(condition.Query.Tags.Select(tag => tag.ToString()));
        }

        var eventsJson = JsonSerializer.Serialize(events.Select(taggedEvent => new
        {
            type = TypeBindings.GetEventBindingKey(taggedEvent.Event.GetType()),
            data = DomainSerializer.Current.Serialize(taggedEvent.Event),
            tags = taggedEvent.Tags.Select(tag => tag.ToString())
        }));

        var snapshot = aggregate.Version > 0 ? aggregate.ToSnapshotEntity(aggregateId) : null;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("[dbo].[memoria_dcb_append_v1]", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var p = command.Parameters;
        p.Add("@affected_tags", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(affectedTags);
        p.Add("@boundary_tags", SqlDbType.NVarChar, -1).Value = (object?)boundaryTags ?? DBNull.Value;
        p.Add("@after_position", SqlDbType.BigInt).Value = condition?.AfterPosition ?? AppendCondition.NoEvents;
        p.Add("@events", SqlDbType.NVarChar, -1).Value = eventsJson;
        p.Add("@now", SqlDbType.DateTimeOffset).Value = DateTimeOffset.UtcNow;
        p.Add("@user", SqlDbType.NVarChar, 255).Value = (object?)user ?? DBNull.Value;
        p.Add("@snapshot_id", SqlDbType.NVarChar, 400).Value = (object?)snapshot?.Id ?? DBNull.Value;
        p.Add("@snapshot_kind", SqlDbType.NVarChar, 20).Value = (object?)snapshot?.SnapshotKind ?? DBNull.Value;
        p.Add("@snapshot_store_id", SqlDbType.NVarChar, 255).Value = (object?)snapshot?.StoreId ?? DBNull.Value;
        p.Add("@snapshot_tag_query", SqlDbType.NVarChar, -1).Value = (object?)snapshot?.TagQuery ?? DBNull.Value;
        p.Add("@snapshot_model_type", SqlDbType.NVarChar, 255).Value = (object?)snapshot?.ModelType ?? DBNull.Value;
        p.Add("@snapshot_version", SqlDbType.Int).Value = snapshot?.Version ?? 0;
        p.Add("@snapshot_latest_position", SqlDbType.BigInt).Value = snapshot?.LatestPosition ?? 0L;
        p.Add("@snapshot_data", SqlDbType.NVarChar, -1).Value = (object?)snapshot?.Data ?? DBNull.Value;
        var appendedParameter = p.Add("@appended", SqlDbType.Bit);
        appendedParameter.Direction = ParameterDirection.Output;
        var latestParameter = p.Add("@latest_position", SqlDbType.BigInt);
        latestParameter.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync(cancellationToken);

        var appended = (bool)appendedParameter.Value;
        var latestPosition = (long)latestParameter.Value;

        if (appended)
        {
            aggregate.LatestPosition = Math.Max(aggregate.LatestPosition, latestPosition);
        }

        return (appended, latestPosition);
    }
}
