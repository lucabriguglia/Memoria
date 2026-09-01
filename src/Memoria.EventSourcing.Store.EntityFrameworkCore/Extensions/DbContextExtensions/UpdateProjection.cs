using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Updates an existing projection with new events from its stream.
    /// </summary>
    /// <typeparam name="T">The type of the projection.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="projectionId">The unique identifier for the projection.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the updated projection or a failure.</returns>
    /// <remarks>
    /// The counterpart of <see cref="UpdateAggregate{T}(IDomainDbContext, IStreamId, IAggregateId{T}, CancellationToken)"/>.
    /// The refresh itself already existed and backed
    /// <see cref="ReadMode.SnapshotWithNewEvents"/>; it is exposed here because a read model differs
    /// from a write model only in never producing events, and refreshing one from its stream is the
    /// same operation.
    /// </remarks>
    /// <example>
    /// <code>
    /// var result = await context.UpdateProjection(streamId, projectionId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var projection = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<T?>> UpdateProjection<T>(this IDomainDbContext domainDbContext,
        IStreamId streamId, IProjectionId<T> projectionId, CancellationToken cancellationToken = default)
        where T : IProjection, new()
    {
        var projectionEntity = await domainDbContext.Projections.AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == projectionId.ToStoreId(), cancellationToken);

        var projection = projectionEntity is null ? new T() : projectionEntity.ToProjection<T>();

        return await domainDbContext.UpdateProjection(streamId, projectionId, projection, cancellationToken);
    }
}
