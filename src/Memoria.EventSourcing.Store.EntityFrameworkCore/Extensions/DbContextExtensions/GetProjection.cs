using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves a persisted projection snapshot for the specified projection identifier.
    /// </summary>
    /// <typeparam name="T">The type of the projection.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier the projection belongs to.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the projection, or a null value when no snapshot exists.</returns>
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
        IProjectionId<T> projectionId, CancellationToken cancellationToken = default) where T : IProjection, new()
    {
        var projectionEntity = await domainDbContext.Projections.AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == projectionId.ToStoreId(), cancellationToken);
        if (projectionEntity is null)
        {
            return default(T);
        }

        return projectionEntity.ToProjection<T>();
    }
}
