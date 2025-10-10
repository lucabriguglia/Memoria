using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Results;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Updates an existing aggregate with new events from its stream.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="aggregateId">The unique identifier for the aggregate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the updated aggregate or a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await context.UpdateAggregate(streamId, aggregateId);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var aggregate = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<T?>> UpdateAggregate<T>(this IDomainDbContext domainDbContext, IStreamId streamId, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregateEntity = await domainDbContext.Aggregates.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == aggregateId.ToStoreId(), cancellationToken);
        var aggregate = aggregateEntity is null ? new T() : aggregateEntity.ToAggregate<T>();
        return await domainDbContext.UpdateAggregate(streamId, aggregateId, aggregate, cancellationToken);
    }
}
