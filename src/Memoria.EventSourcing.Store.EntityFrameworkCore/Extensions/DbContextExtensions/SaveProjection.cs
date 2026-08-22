using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Saves a projection snapshot, upserting it into the aggregate snapshot table.
    /// </summary>
    /// <typeparam name="T">The type of the projection.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The stream identifier the projection belongs to.</param>
    /// <param name="projectionId">The projection identifier.</param>
    /// <param name="projection">The projection instance to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating the success or failure of the save operation.</returns>
    /// <example>
    /// <code>
    /// var result = await context.SaveProjection(streamId, projectionId, projection);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// </code>
    /// </example>
    public static async Task<Result> SaveProjection<T>(this IDomainDbContext domainDbContext, IStreamId streamId,
        IProjectionId<T> projectionId, T projection, CancellationToken cancellationToken = default)
        where T : IProjection
    {
        try
        {
            var projectionEntity = projection.ToProjectionEntity(streamId, projectionId);

            var exists = await domainDbContext.Projections.AsNoTracking()
                .AnyAsync(entity => entity.Id == projectionEntity.Id, cancellationToken);
            if (exists)
            {
                domainDbContext.Projections.Update(projectionEntity);
            }
            else
            {
                domainDbContext.Projections.Add(projectionEntity);
            }

            await domainDbContext.SaveChangesAsync(cancellationToken);

            domainDbContext.DetachProjection(projectionId, projection);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Save Projection");
            return ErrorHandling.DefaultFailure;
        }
    }
}
