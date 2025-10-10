using Memoria.Results;
using Microsoft.EntityFrameworkCore;
using OpenCqrs.EventSourcing.Domain;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves all aggregate-event relationship entities for a specific aggregate.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="aggregateId">The unique identifier for the aggregate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the list of aggregate-event entities or a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await context.GetAggregateEventEntities(aggregateId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var entities = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<List<AggregateEventEntity>>> GetAggregateEventEntities<T>(this IDomainDbContext domainDbContext, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregateEventEntities = await domainDbContext.AggregateEvents.Include(entity => entity.Event).AsNoTracking()
            .Where(entity => entity.AggregateId == aggregateId.ToStoreId())
            .ToListAsync(cancellationToken);

        return aggregateEventEntities.ToList();
    }
}
