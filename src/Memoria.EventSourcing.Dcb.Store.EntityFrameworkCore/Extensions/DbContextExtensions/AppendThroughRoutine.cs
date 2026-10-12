using System.Data.Common;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.AppendRoutine;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Whether this append goes through the database's append routine rather than Entity Framework
    /// Core.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only on an engine that has a routine, only from a <see cref="DcbDbContext"/>, which carries
    /// the audit values the routine stamps, and only where the model is the one the routine is
    /// written for.
    /// </para>
    /// <para>
    /// Every boundary the public factories build is a union or a single intersection, and the
    /// routine takes both, or none for an unconditional append. A boundary mixing the two cannot be
    /// built today; it keeps the Entity Framework Core path, which refuses it loudly.
    /// </para>
    /// </remarks>
    private static bool AppendsThroughRoutine(this IDcbDbContext dcbDbContext, AppendCondition? condition) =>
        dcbDbContext is DcbDbContext context
        && DcbAppendRoutine.Definition(dcbDbContext.Database.ProviderName) is not null
        && DcbAppendRoutine.FitsModel(context.Model)
        && (condition is null || IsUnion(condition.Query) || IsIntersection(condition.Query));

    private static bool IsUnion(TagQuery query) => query.TagGroups.All(group => group.Count == 1);

    private static bool IsIntersection(TagQuery query) => query.TagGroups.Count == 1;

    /// <summary>
    /// Appends through the database's append routine: the boundary check, the tag head locks and
    /// the writes in one round trip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Through the execution strategy, like the Entity Framework Core path, so a deployment that
    /// retries transient failures retries this too. A refusal leaves as a value and is never retried.
    /// The refusal carries the boundary's latest position from the same call, so it costs no re-read.
    /// </para>
    /// <para>
    /// Changes the caller staged on the context have always been saved with the append, all or
    /// nothing. With some staged, the call runs in a transaction after a save of them that is not
    /// accepted until the append commits: a refusal rolls them back and leaves them pending, and a
    /// retried attempt saves them again. Not through <c>Appending</c>, which clears the tracker
    /// between attempts and would throw them away; nothing else is attached here to clear.
    /// </para>
    /// </remarks>
    private static async Task<Result> AppendThroughRoutine(this IDcbDbContext dcbDbContext, TaggedEvent[] events,
        AppendCondition? condition, List<string> affectedTags, string operation, CancellationToken cancellationToken,
        DcbSnapshotEntity? snapshot = null, Action<long>? appendedTo = null)
    {
        var context = (DcbDbContext)dcbDbContext;
        var staged = context.ChangeTracker.HasChanges();

        try
        {
            var outcome = await context.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
            {
                if (!staged)
                {
                    return await dcbDbContext.CallAppendRoutine(events, condition, affectedTags, snapshot, token);
                }

                await using var transaction = await context.Database.BeginTransactionAsync(token);
                await context.SaveChangesAsync(acceptAllChangesOnSuccess: false, token);

                var attempt = await dcbDbContext.CallAppendRoutine(events, condition, affectedTags, snapshot, token);
                if (attempt.Appended)
                {
                    await transaction.CommitAsync(token);
                }

                return attempt;
            }, cancellationToken);

            if (outcome.Appended)
            {
                if (staged)
                {
                    context.ChangeTracker.AcceptAllChanges();
                }

                if (outcome.LatestPosition is { } lastWritten)
                {
                    appendedTo?.Invoke(lastWritten);
                }

                return Result.Ok();
            }

            // Only a conditioned append can be refused: an unconditional one has nothing to check.
            DcbDiagnostics.AddConcurrencyConflictEvent(condition!.Query, condition.AfterPosition,
                outcome.LatestPosition!.Value);

            return DcbStoreFailures.ConcurrencyConflict(condition.Query, condition.AfterPosition,
                outcome.LatestPosition!.Value);
        }
        catch (Exception exception) when (IsMissingRoutine(exception))
        {
            DcbDiagnostics.AddException(exception, operation, condition?.Query);

            return DcbStoreFailures.StorageFailure(operation, condition?.Query) with
            {
                Description = $"The database has no '{DcbAppendRoutine.Name}' routine, which this store appends " +
                              "through. Install it with Database.EnsureDcbAppendRoutineAsync(), in an EF Core " +
                              "migration, or with the DCB install script for this engine."
            };
        }
        catch (Exception exception)
        {
            return await dcbDbContext.AppendFailure(exception, operation, condition, affectedTags,
                cancellationToken);
        }
    }

    private static bool IsMissingRoutine(Exception exception) =>
        exception is DbException { SqlState: DcbAppendRoutine.PostgreSqlUndefinedFunction };

    private static async Task<AppendRoutineOutcome> CallAppendRoutine(this IDcbDbContext dcbDbContext,
        TaggedEvent[] events, AppendCondition? condition, List<string> affectedTags, DcbSnapshotEntity? snapshot,
        CancellationToken cancellationToken)
    {
        var (createdDate, createdBy) = ((DcbDbContext)dcbDbContext).AuditStamp();

        // Each tag row names its event by its 1-based place in the append.
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

        // Plain values rather than provider parameters, so the store needs no Npgsql reference; the
        // casts tell PostgreSQL what each one is, including the ones that arrive as null.
        object?[] arguments =
        [
            affectedTags.ToArray(),
            condition?.Query.Tags.Select(tag => tag.ToString()).ToArray(),
            condition is not null && !IsUnion(condition.Query),
            condition?.AfterPosition ?? AppendCondition.NoEvents,
            events.Select(taggedEvent => TypeBindings.GetEventBindingKey(taggedEvent.Event.GetType())).ToArray(),
            events.Select(taggedEvent => DomainSerializer.Current.Serialize(taggedEvent.Event)).ToArray(),
            tagEventIndex.ToArray(),
            tagValues.ToArray(),
            createdDate,
            createdBy,
            snapshot?.Id,
            snapshot?.SnapshotKind,
            snapshot?.StoreId,
            snapshot?.TagQuery,
            snapshot?.ModelType,
            snapshot?.Version ?? 0,
            snapshot?.LatestPosition ?? 0L,
            snapshot?.Data
        ];

        var outcomes = await dcbDbContext.Database
            .SqlQueryRaw<AppendRoutineOutcome>(
                $$"""
                SELECT appended AS "Appended", latest_position AS "LatestPosition"
                FROM {{DcbAppendRoutine.Name}}({0}::text[], {1}::text[], {2}::boolean, {3}::bigint,
                    {4}::text[], {5}::text[], {6}::int[], {7}::text[], {8}::timestamptz, {9}::text,
                    {10}::text, {11}::text, {12}::text, {13}::text, {14}::text, {15}::int, {16}::bigint, {17}::text)
                """, arguments!)
            .ToListAsync(cancellationToken);

        return outcomes.Single();
    }

    /// <summary>What the append routine returns: whether it appended, and the boundary's position.</summary>
    /// <remarks>
    /// When it appended, the position of the last event written, or null when there were none and
    /// only a snapshot was written; when it refused, where the boundary had moved to.
    /// </remarks>
    private sealed class AppendRoutineOutcome
    {
        public bool Appended { get; init; }

        public long? LatestPosition { get; init; }
    }
}
