using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Results;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Folds every event inside a boundary into a fresh projection, without persisting a snapshot.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryProjection<T>(this IDcbDbContext dcbDbContext,
        IDcbProjectionId<T> projectionId, CancellationToken cancellationToken = default)
        where T : IDcbProjection, new()
    {
        var projection = NewProjection(projectionId);

        var eventEntities = await dcbDbContext.GetEventEntities(projectionId.Boundary, projection.EventTypeFilter, cancellationToken);

        return Fold(projection, projectionId, eventEntities);
    }

    /// <summary>
    /// Folds the events inside a boundary up to a position into a fresh projection.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryProjection<T>(this IDcbDbContext dcbDbContext,
        IDcbProjectionId<T> projectionId, long upToPosition,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new()
    {
        var projection = NewProjection(projectionId);

        var eventEntities = await dcbDbContext.GetEventEntitiesUpToPosition(projectionId.Boundary, upToPosition,
            projection.EventTypeFilter, cancellationToken);

        return Fold(projection, projectionId, eventEntities);
    }

    /// <summary>
    /// Folds the events inside a boundary up to a date into a fresh projection.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryProjection<T>(this IDcbDbContext dcbDbContext,
        IDcbProjectionId<T> projectionId, DateTimeOffset upToDate,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new()
    {
        var projection = NewProjection(projectionId);

        var eventEntities = await dcbDbContext.GetEventEntitiesUpToDate(projectionId.Boundary, upToDate,
            projection.EventTypeFilter, cancellationToken);

        return Fold(projection, projectionId, eventEntities);
    }

    /// <summary>
    /// Creates the model with its boundary already set, so <c>Apply</c> can read it. The mirror of
    /// <see cref="NewAggregate{T}"/>: a read model is folded exactly as a write model is.
    /// </summary>
    private static T NewProjection<T>(IDcbProjectionId<T> projectionId) where T : IDcbProjection, new() =>
        new() { Tags = projectionId.Boundary.Tags };

    private static T Fold<T>(T projection, IDcbProjectionId<T> projectionId, List<DcbEventEntity> eventEntities)
        where T : IDcbProjection
    {
        if (eventEntities.Count == 0)
        {
            return projection;
        }

        projection.Apply(eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));

        if (projection.Version == 0)
        {
            return projection;
        }

        projection.ProjectionId = projectionId.ToStoreId();
        projection.LatestPosition = eventEntities[^1].Position;

        return projection;
    }
}
