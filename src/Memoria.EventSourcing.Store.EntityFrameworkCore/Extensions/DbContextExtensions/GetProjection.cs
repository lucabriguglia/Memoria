using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves a projection for the specified projection identifier, using the selected
    /// <see cref="ReadMode"/> to control how the snapshot and subsequent events are combined.
    /// </summary>
    /// <typeparam name="T">The type of the projection.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier the projection belongs to.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="readMode">The mode in which the projection should be read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the projection, or a null value when no snapshot exists (or, for reconstruction modes, no events could be applied).</returns>
    /// <example>
    /// <code>
    /// var result = await context.GetProjection(streamId, projectionId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var projection = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<T?>> GetProjection<T>(this IDomainDbContext domainDbContext, IStreamId streamId,
        IProjectionId<T> projectionId, ReadMode readMode = ReadMode.SnapshotOnly,
        CancellationToken cancellationToken = default) where T : IProjection, new()
    {
        var projectionEntity = await domainDbContext.Projections.AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == projectionId.ToStoreId(), cancellationToken);
        if (projectionEntity is not null)
        {
            var currentProjection = projectionEntity.ToProjection<T>();
            switch (readMode)
            {
                case ReadMode.SnapshotOnly or ReadMode.SnapshotOrCreate:
                    return currentProjection;
                case ReadMode.SnapshotWithNewEvents or ReadMode.SnapshotWithNewEventsOrCreate:
                    return await domainDbContext.UpdateProjection(streamId, projectionId, currentProjection,
                        cancellationToken);
            }
        }

        if (readMode is ReadMode.SnapshotOnly or ReadMode.SnapshotWithNewEvents)
        {
            return default(T);
        }

        var projection = new T();

        var eventEntities = await domainDbContext.GetEventEntities(streamId, projection.EventTypeFilter,
            projectionId.EventPropertyFilter, cancellationToken: cancellationToken);
        if (eventEntities.Count == 0)
        {
            return default(T);
        }

        var events = eventEntities.Select(eventEntity => eventEntity.ToDomainEvent()).ToList();
        var versionBefore = projection.Version;
        projection.Apply(events);

        ProjectionDiagnostics.AddProjectionFoldedEvent(streamId, projectionId,
            appliedFromSequence: eventEntities[0].Sequence, appliedToSequence: eventEntities[^1].Sequence,
            appliedCount: eventEntities.Count, versionBefore: versionBefore,
            versionAfter: projection.Version);

        if (projection.Version == 0)
        {
            return default(T);
        }

        projection.LatestEventSequence = eventEntities[^1].Sequence;

        try
        {
            var projectionEntityToSave = projection.ToProjectionEntity(streamId, projectionId);
            domainDbContext.Projections.Add(projectionEntityToSave);
            await domainDbContext.SaveChangesAsync(cancellationToken);
            domainDbContext.DetachProjection(projectionId, projection);
        }
        catch (Exception ex)
        {
            const string operation = "Get Projection";
            DiagnosticsExtensions.AddException(ex, streamId, operation);
            return StoreFailures.StorageFailure(operation, streamId);
        }

        return projection;
    }
}
