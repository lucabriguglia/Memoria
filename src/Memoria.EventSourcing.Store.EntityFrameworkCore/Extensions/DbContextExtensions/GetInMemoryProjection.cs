using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Results;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves an in-memory projection by folding all matching events from the stream. The
    /// rebuilt projection is not persisted as a snapshot.
    /// </summary>
    /// <typeparam name="T">The type of the projection.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the projection. When no matching events are stored, a projection with <c>Version = 0</c> is returned.</returns>
    /// <example>
    /// <code>
    /// var result = await context.GetInMemoryProjection(streamId, projectionId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var projection = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<T>> GetInMemoryProjection<T>(this IDomainDbContext domainDbContext,
        IStreamId streamId, IProjectionId<T> projectionId, CancellationToken cancellationToken = default)
        where T : IProjection, new()
    {
        var projection = new T();

        var eventEntities = await domainDbContext.GetEventEntities(streamId, projection.EventTypeFilter, projectionId.EventPropertyFilter,
            cancellationToken: cancellationToken);
        if (eventEntities.Count == 0)
        {
            return projection;
        }

        projection.Apply(eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));
        if (projection.Version == 0)
        {
            return projection;
        }

        projection.StreamId = streamId.Id;
        projection.ProjectionId = projectionId.ToStoreId();
        projection.LatestEventSequence = eventEntities[^1].Sequence;

        return projection;
    }

    /// <summary>
    /// Retrieves an in-memory projection by folding matching events from the stream up to a specific
    /// sequence. The rebuilt projection is not persisted as a snapshot.
    /// </summary>
    /// <typeparam name="T">The type of the projection.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="upToSequence">The maximum sequence number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the projection.</returns>
    /// <example>
    /// <code>
    /// var result = await context.GetInMemoryProjection(streamId, projectionId, upToSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var projection = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<T>> GetInMemoryProjection<T>(this IDomainDbContext domainDbContext,
        IStreamId streamId, IProjectionId<T> projectionId, int upToSequence,
        CancellationToken cancellationToken = default) where T : IProjection, new()
    {
        var projection = new T();

        var eventEntities = await domainDbContext.GetEventEntitiesUpToSequence(streamId, upToSequence,
            projection.EventTypeFilter, projectionId.EventPropertyFilter, cancellationToken: cancellationToken);
        if (eventEntities.Count == 0)
        {
            return projection;
        }

        projection.Apply(eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));
        if (projection.Version == 0)
        {
            return projection;
        }

        projection.StreamId = streamId.Id;
        projection.ProjectionId = projectionId.ToStoreId();
        projection.LatestEventSequence = eventEntities[^1].Sequence;

        return projection;
    }

    /// <summary>
    /// Retrieves an in-memory projection by folding matching events from the stream up to a specific
    /// date. The rebuilt projection is not persisted as a snapshot.
    /// </summary>
    /// <typeparam name="T">The type of the projection.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="upToDate">The maximum date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the projection.</returns>
    /// <example>
    /// <code>
    /// var result = await context.GetInMemoryProjection(streamId, projectionId, upToDate);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var projection = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<T>> GetInMemoryProjection<T>(this IDomainDbContext domainDbContext,
        IStreamId streamId, IProjectionId<T> projectionId, DateTimeOffset upToDate,
        CancellationToken cancellationToken = default) where T : IProjection, new()
    {
        var projection = new T();

        var eventEntities = await domainDbContext.GetEventEntitiesUpToDate(streamId, upToDate,
            projection.EventTypeFilter, projectionId.EventPropertyFilter, cancellationToken: cancellationToken);
        if (eventEntities.Count == 0)
        {
            return projection;
        }

        projection.Apply(eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()));
        if (projection.Version == 0)
        {
            return projection;
        }

        projection.StreamId = streamId.Id;
        projection.ProjectionId = projectionId.ToStoreId();
        projection.LatestEventSequence = eventEntities[^1].Sequence;

        return projection;
    }
}
