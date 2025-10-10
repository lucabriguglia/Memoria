using Memoria.Results;
using OpenCqrs.EventSourcing.Domain;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Tracks an aggregate's uncommitted events and state changes in the Entity Framework change tracker.
    /// </summary>
    /// <typeparam name="T">The type of the aggregate.</typeparam>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="aggregateId">The unique identifier for the aggregate.</param>
    /// <param name="aggregate">The aggregate instance to track.</param>
    /// <param name="expectedEventSequence">The expected sequence number for concurrency control.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result containing the tracked entities or a failure.</returns>
    /// <example>
    /// <code>
    /// var result = await context.TrackAggregate(streamId, aggregateId, aggregate, expectedSequence);
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// var (eventEntities, aggregateEntity, aggregateEventEntities) = result.Value;
    /// </code>
    /// </example>
    public static async Task<Result<(List<EventEntity>? EventEntities, AggregateEntity? AggregateEntity, List<AggregateEventEntity>? AggregateEventEntities)>> TrackAggregate<T>(this IDomainDbContext domainDbContext, IStreamId streamId, IAggregateId<T> aggregateId, T aggregate, int expectedEventSequence, CancellationToken cancellationToken = default) where T : IAggregateRoot
    {
        if (!aggregate.UncommittedEvents.Any())
        {
            DiagnosticsExtensions.AddActivityEvent(streamId, aggregateId, name: "NoUncommittedEvents");
            return ErrorHandling.DefaultFailure;
        }

        var latestEventSequence = await domainDbContext.GetLatestEventSequence(streamId, cancellationToken: cancellationToken);
        if (latestEventSequence != expectedEventSequence)
        {
            DiagnosticsExtensions.AddActivityEvent(streamId, expectedEventSequence, latestEventSequence);
            return ErrorHandling.DefaultFailure;
        }

        var newLatestEventSequenceForAggregate = latestEventSequence + aggregate.UncommittedEvents.Count();
        var currentAggregateVersion = aggregate.Version - aggregate.UncommittedEvents.Count();

        var trackedEventEntities = domainDbContext.TrackEventEntities(streamId, aggregate.UncommittedEvents.ToArray(), startingEventSequence: latestEventSequence + 1);
        var trackedAggregateEntity = domainDbContext.TrackAggregateEntity(streamId, aggregateId, aggregate, newLatestEventSequenceForAggregate, aggregateIsNew: currentAggregateVersion == 0);
        var trackedAggregateEventEntities = domainDbContext.TrackAggregateEventEntities(trackedAggregateEntity, trackedEventEntities);

        return (trackedEventEntities, trackedAggregateEntity, trackedAggregateEventEntities);
    }
}
