using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Results;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves all event entities that have been applied to a specific aggregate instance.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="aggregateId">The unique identifier for the aggregate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the list of applied event entities or a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await context.GetEventEntitiesAppliedToAggregate(aggregateId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var entities = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<List<EventEntity>>> GetEventEntitiesAppliedToAggregate<T>(this IDomainDbContext domainDbContext, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregateEventEntitiesResult = await domainDbContext.GetAggregateEventEntities(aggregateId, cancellationToken);
        if (aggregateEventEntitiesResult.IsNotSuccess)
        {
            return aggregateEventEntitiesResult.Failure!;
        }

        return aggregateEventEntitiesResult.Value!.Select(entity => entity.Event).ToList();
    }
}
