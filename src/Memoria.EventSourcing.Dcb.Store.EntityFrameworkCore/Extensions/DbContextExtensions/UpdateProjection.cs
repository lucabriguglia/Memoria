using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Results;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class DcbDbContextExtensions
{
    /// <summary>
    /// Brings a projection's snapshot up to date with the events appended inside its boundary since
    /// it was written, and persists the result.
    /// </summary>
    /// <typeparam name="T">The projection type.</typeparam>
    /// <param name="dcbDbContext">The context.</param>
    /// <param name="projectionId">The projection identifier, which carries the boundary.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The refreshed projection, or null when there is nothing to refresh — no snapshot and no events
    /// inside the boundary that this projection applies.
    /// </returns>
    /// <remarks>
    /// The exact counterpart of <see cref="UpdateAggregate{T}"/>. A read model differs from a write
    /// model only in never producing events; refreshing one from its boundary is the same operation,
    /// so it is offered the same way rather than left reachable only through
    /// <see cref="ReadMode.SnapshotWithNewEvents"/>.
    /// </remarks>
    public static async Task<Result<T?>> UpdateProjection<T>(this IDcbDbContext dcbDbContext,
        IDcbProjectionId<T> projectionId, CancellationToken cancellationToken = default)
        where T : IDcbProjection, new()
    {
        const string operation = "Update Projection";

        try
        {
            var snapshot = await dcbDbContext.GetSnapshotEntity(DcbSnapshotEntity.ProjectionKind,
                projectionId.ToStoreId(), projectionId.Boundary, cancellationToken);

            var projection = snapshot is null ? new T() : snapshot.ToProjection<T>();
            projection.Tags = projectionId.Boundary.Tags;

            return await dcbDbContext.RefreshProjection(projectionId, projection,
                snapshotExists: snapshot is not null, cancellationToken);
        }
        catch (Exception exception)
        {
            dcbDbContext.ChangeTracker.Clear();
            DcbDiagnostics.AddException(exception, operation, projectionId.Boundary);
            return DcbStoreFailures.StorageFailure(operation, projectionId.Boundary);
        }
    }
}
