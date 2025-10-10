using Memoria.EventSourcing.Domain;
using Memoria.Results;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Saves an aggregate to the event store with optimistic concurrency control.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="aggregateId">The unique identifier for the aggregate.</param>
    /// <param name="aggregate">The aggregate instance to save.</param>
    /// <param name="expectedEventSequence">The expected sequence number for concurrency control.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating the success or failure of the save operation.</returns>
    /// <example>
    /// <code>
    /// var result = await context.SaveAggregate(streamId, aggregateId, aggregate, expectedSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// // Save successful
    /// </code>
    /// </example>
    public static async Task<Result> SaveAggregate<T>(this IDomainDbContext domainDbContext, IStreamId streamId, IAggregateId<T> aggregateId, T aggregate, int expectedEventSequence, CancellationToken cancellationToken = default) where T : IAggregateRoot
    {
        try
        {
            var trackResult = await domainDbContext.TrackAggregate(streamId, aggregateId, aggregate, expectedEventSequence, cancellationToken);
            if (trackResult.IsNotSuccess)
            {
                return trackResult.Failure!;
            }

            await domainDbContext.SaveChangesAsync(cancellationToken);

            domainDbContext.DetachAggregate(aggregateId, aggregate);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Save Aggregate");
            return ErrorHandling.DefaultFailure;
        }
    }
}
