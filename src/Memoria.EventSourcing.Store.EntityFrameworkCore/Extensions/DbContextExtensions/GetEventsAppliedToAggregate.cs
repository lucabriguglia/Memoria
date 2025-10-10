using Memoria.EventSourcing.Domain;
using Memoria.Results;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Retrieves all domain events that have been applied to a specific aggregate instance.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="aggregateId">The unique identifier for the aggregate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the list of applied domain events or a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await context.GetEventsAppliedToAggregate(aggregateId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var events = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<List<IEvent>>> GetEventsAppliedToAggregate<T>(this IDomainDbContext domainDbContext, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var eventEntitiesAppliedToAggregate = await domainDbContext.GetEventEntitiesAppliedToAggregate(aggregateId, cancellationToken);
        if (eventEntitiesAppliedToAggregate.IsNotSuccess)
        {
            return eventEntitiesAppliedToAggregate.Failure!;
        }

        return eventEntitiesAppliedToAggregate.Value!.Select(entity => entity.ToDomainEvent()).ToList();
    }
}
