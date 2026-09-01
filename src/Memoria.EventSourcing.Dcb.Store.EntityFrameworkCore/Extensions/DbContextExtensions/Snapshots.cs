using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Reads the snapshot of a model folded under a boundary, if there is one.
    /// </summary>
    /// <remarks>
    /// The full boundary is compared, not just the digest that forms the identity, so a digest
    /// collision misses rather than returning a snapshot of a different boundary.
    /// </remarks>
    private static async Task<DcbSnapshotEntity?> GetSnapshotEntity(this IDcbDbContext dcbDbContext,
        string kind, string storeId, TagQuery query, CancellationToken cancellationToken)
    {
        var id = DcbSnapshotEntity.BuildId(kind, storeId, query);
        var canonicalQuery = query.ToString();

        return await dcbDbContext.DcbSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(snapshot => snapshot.Id == id && snapshot.TagQuery == canonicalQuery,
                cancellationToken);
    }

    /// <summary>
    /// Writes a snapshot, replacing any earlier fold of the same model under the same boundary.
    /// </summary>
    private static async Task WriteSnapshot(this IDcbDbContext dcbDbContext, DcbSnapshotEntity snapshot,
        CancellationToken cancellationToken)
    {
        var exists = await dcbDbContext.DcbSnapshots.AsNoTracking()
            .AnyAsync(existing => existing.Id == snapshot.Id, cancellationToken);

        if (exists)
        {
            dcbDbContext.DcbSnapshots.Update(snapshot);
        }
        else
        {
            dcbDbContext.DcbSnapshots.Add(snapshot);
        }

        await dcbDbContext.SaveChangesAsync(cancellationToken);
        dcbDbContext.ChangeTracker.Clear();
    }

    /// <summary>
    /// Gets an aggregate folded from the events inside a boundary.
    /// </summary>
    /// <typeparam name="T">The aggregate type.</typeparam>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="query">The consistency boundary. Part of the snapshot's identity.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="readMode">How the snapshot and any newer events combine.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The aggregate, or null when no snapshot exists and the read mode does not build one.
    /// </returns>
    public static async Task<Result<T?>> GetAggregate<T>(this IDcbDbContext dcbDbContext, TagQuery query,
        IDcbAggregateId<T> aggregateId, ReadMode readMode = ReadMode.SnapshotOnly,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new()
    {
        const string operation = "Get Aggregate";

        try
        {
            var storeId = aggregateId.ToStoreId();

            var snapshot = await dcbDbContext.GetSnapshotEntity(DcbSnapshotEntity.AggregateKind, storeId, query,
                cancellationToken);

            if (snapshot is not null)
            {
                var current = snapshot.ToAggregate<T>();

                if (readMode is ReadMode.SnapshotOnly or ReadMode.SnapshotOrCreate)
                {
                    return current;
                }

                return await dcbDbContext.RefreshAggregate(query, aggregateId, current, cancellationToken);
            }

            if (readMode is ReadMode.SnapshotOnly or ReadMode.SnapshotWithNewEvents)
            {
                return default(T);
            }

            var aggregate = new T();
            var eventEntities = await dcbDbContext.GetEventEntities(query, aggregate.EventTypeFilter,
                cancellationToken);

            if (eventEntities.Count == 0)
            {
                return default(T);
            }

            var versionBefore = aggregate.Version;
            aggregate.Apply(eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));

            DcbDiagnostics.AddModelFoldedEvent(query, storeId,
                appliedFromPosition: eventEntities[0].Position, appliedToPosition: eventEntities[^1].Position,
                appliedCount: eventEntities.Count, versionBefore: versionBefore, versionAfter: aggregate.Version);

            // Nothing applied, so there is no state worth persisting and nothing to distinguish this
            // from an aggregate that was never created.
            if (aggregate.Version == 0)
            {
                return default(T);
            }

            aggregate.LatestPosition = eventEntities[^1].Position;

            await dcbDbContext.WriteSnapshot(aggregate.ToSnapshotEntity(query, aggregateId), cancellationToken);

            return aggregate;
        }
        catch (Exception exception)
        {
            dcbDbContext.ChangeTracker.Clear();
            DcbDiagnostics.AddException(exception, operation, query);
            return DcbStoreFailures.StorageFailure(operation, query);
        }
    }

    /// <summary>
    /// Applies the events appended inside the boundary since this model was folded, and writes the
    /// result back when any of them changed it.
    /// </summary>
    /// <returns>
    /// The refreshed aggregate, or null when it has no state — nothing was folded into it and
    /// nothing new applied, so there is nothing to distinguish it from an aggregate that was never
    /// created. Mirrors the streamed store's equivalent.
    /// </returns>
    private static async Task<Result<T?>> RefreshAggregate<T>(this IDcbDbContext dcbDbContext, TagQuery query,
        IDcbAggregateId<T> aggregateId, T aggregate, CancellationToken cancellationToken)
        where T : IDcbAggregateRoot
    {
        var versionBefore = aggregate.Version;

        var newEventEntities = await dcbDbContext.GetEventEntitiesFromPosition(query,
            aggregate.LatestPosition + 1, aggregate.EventTypeFilter, cancellationToken);

        if (newEventEntities.Count == 0)
        {
            return aggregate.Version > 0 ? aggregate : default(T);
        }

        aggregate.Apply(newEventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));

        DcbDiagnostics.AddModelFoldedEvent(query, aggregateId.ToStoreId(),
            appliedFromPosition: newEventEntities[0].Position,
            appliedToPosition: newEventEntities[^1].Position,
            appliedCount: newEventEntities.Count, versionBefore: versionBefore, versionAfter: aggregate.Version);

        // Every new event matched the type filter and was then ignored by Apply, so the state is the
        // one already stored and rewriting it would buy nothing.
        if (aggregate.Version == versionBefore)
        {
            return aggregate.Version > 0 ? aggregate : default(T);
        }

        aggregate.LatestPosition = newEventEntities[^1].Position;

        await dcbDbContext.WriteSnapshot(aggregate.ToSnapshotEntity(query, aggregateId), cancellationToken);

        return aggregate;
    }

    /// <summary>
    /// Gets a projection folded from the events inside a boundary.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="query">The consistency boundary. Part of the snapshot's identity.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="readMode">How the snapshot and any newer events combine.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The projection, or null when no snapshot exists and the read mode does not build one.
    /// </returns>
    public static async Task<Result<T?>> GetProjection<T>(this IDcbDbContext dcbDbContext, TagQuery query,
        IDcbProjectionId<T> projectionId, ReadMode readMode = ReadMode.SnapshotOnly,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new()
    {
        const string operation = "Get Projection";

        try
        {
            var storeId = projectionId.ToStoreId();

            var snapshot = await dcbDbContext.GetSnapshotEntity(DcbSnapshotEntity.ProjectionKind, storeId, query,
                cancellationToken);

            if (snapshot is not null)
            {
                var current = snapshot.ToProjection<T>();

                if (readMode is ReadMode.SnapshotOnly or ReadMode.SnapshotOrCreate)
                {
                    return current;
                }

                return await dcbDbContext.RefreshProjection(query, projectionId, current, cancellationToken);
            }

            if (readMode is ReadMode.SnapshotOnly or ReadMode.SnapshotWithNewEvents)
            {
                return default(T);
            }

            var projection = new T();
            var eventEntities = await dcbDbContext.GetEventEntities(query, projection.EventTypeFilter,
                cancellationToken);

            if (eventEntities.Count == 0 )
            {
                return default(T);
            }

            projection.Apply(eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));

            if (projection.Version == 0)
            {
                return default(T);
            }

            projection.LatestPosition = eventEntities[^1].Position;

            await dcbDbContext.WriteSnapshot(projection.ToSnapshotEntity(query, projectionId), cancellationToken);

            return projection;
        }
        catch (Exception exception)
        {
            dcbDbContext.ChangeTracker.Clear();
            DcbDiagnostics.AddException(exception, operation, query);
            return DcbStoreFailures.StorageFailure(operation, query);
        }
    }

    private static async Task<Result<T?>> RefreshProjection<T>(this IDcbDbContext dcbDbContext, TagQuery query,
        IDcbProjectionId<T> projectionId, T projection, CancellationToken cancellationToken)
        where T : IDcbProjection
    {
        var newEventEntities = await dcbDbContext.GetEventEntitiesFromPosition(query,
            projection.LatestPosition + 1, projection.EventTypeFilter, cancellationToken);

        if (newEventEntities.Count == 0)
        {
            return projection;
        }

        projection.Apply(newEventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));
        projection.LatestPosition = newEventEntities[^1].Position;

        await dcbDbContext.WriteSnapshot(projection.ToSnapshotEntity(query, projectionId), cancellationToken);

        return projection;
    }

    /// <summary>
    /// Persists a projection snapshot against the boundary it was folded under.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="query">The consistency boundary the projection was folded under.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="projection">The projection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The outcome.</returns>
    /// <remarks>
    /// No concurrency check, matching the streamed stores: a projection snapshot is a derived cache,
    /// and the worst a lost update costs is the work of folding it again.
    /// </remarks>
    public static async Task<Result> SaveProjection<T>(this IDcbDbContext dcbDbContext, TagQuery query,
        IDcbProjectionId<T> projectionId, T projection, CancellationToken cancellationToken = default)
        where T : IDcbProjection
    {
        const string operation = "Save Projection";

        try
        {
            await dcbDbContext.WriteSnapshot(projection.ToSnapshotEntity(query, projectionId), cancellationToken);
            return Result.Ok();
        }
        catch (Exception exception)
        {
            dcbDbContext.ChangeTracker.Clear();
            DcbDiagnostics.AddException(exception, operation, query);
            return DcbStoreFailures.StorageFailure(operation, query);
        }
    }
}
