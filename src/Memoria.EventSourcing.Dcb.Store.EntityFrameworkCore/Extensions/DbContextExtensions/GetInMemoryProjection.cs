using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Results;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Folds every event inside a boundary into a fresh projection, without persisting a snapshot.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryProjection<T>(this IDcbDbContext dcbDbContext,
        TagQuery query, IDcbProjectionId<T> projectionId, CancellationToken cancellationToken = default)
        where T : IDcbProjection, new()
    {
        var projection = new T();

        var eventEntities = await dcbDbContext.GetEventEntities(query, projection.EventTypeFilter, cancellationToken);

        return Fold(projection, projectionId, eventEntities);
    }

    /// <summary>
    /// Folds the events inside a boundary up to a position into a fresh projection.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryProjection<T>(this IDcbDbContext dcbDbContext,
        TagQuery query, IDcbProjectionId<T> projectionId, long upToPosition,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new()
    {
        var projection = new T();

        var eventEntities = await dcbDbContext.GetEventEntitiesUpToPosition(query, upToPosition,
            projection.EventTypeFilter, cancellationToken);

        return Fold(projection, projectionId, eventEntities);
    }

    /// <summary>
    /// Folds the events inside a boundary up to a date into a fresh projection.
    /// </summary>
    public static async Task<Result<T>> GetInMemoryProjection<T>(this IDcbDbContext dcbDbContext,
        TagQuery query, IDcbProjectionId<T> projectionId, DateTimeOffset upToDate,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new()
    {
        var projection = new T();

        var eventEntities = await dcbDbContext.GetEventEntitiesUpToDate(query, upToDate,
            projection.EventTypeFilter, cancellationToken);

        return Fold(projection, projectionId, eventEntities);
    }

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
