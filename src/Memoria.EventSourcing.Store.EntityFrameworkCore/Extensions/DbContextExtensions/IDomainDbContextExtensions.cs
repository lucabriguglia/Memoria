using Memoria.EventSourcing.Domain;
using Memoria.Results;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    private static async Task<Result<T?>> UpdateAggregate<T>(this IDomainDbContext domainDbContext, IStreamId streamId, IAggregateId<T> aggregateId, T aggregate, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var currentAggregateVersion = aggregate.Version;

        var newEventEntities = await domainDbContext.GetEventEntitiesFromSequence(streamId, fromSequence: aggregate.LatestEventSequence + 1, aggregate.EventTypeFilter, cancellationToken);
        if (newEventEntities.Count == 0)
        {
            return aggregate.Version > 0 ? aggregate : default;
        }

        var newEvents = newEventEntities.Select(eventEntity => eventEntity.ToDomainEvent()).ToList();
        aggregate.Apply(newEvents);

        if (aggregate.Version == currentAggregateVersion)
        {
            return aggregate.Version > 0 ? aggregate : default;
        }

        var latestEventSequenceForAggregate = newEventEntities.OrderBy(eventEntity => eventEntity.Sequence).Last().Sequence;
        var trackedAggregateEntity = domainDbContext.TrackAggregateEntity(streamId, aggregateId, aggregate, latestEventSequenceForAggregate, aggregateIsNew: currentAggregateVersion == 0);
        domainDbContext.TrackAggregateEventEntities(trackedAggregateEntity, newEventEntities);

        try
        {
            await domainDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Update Aggregate");
            return ErrorHandling.DefaultFailure;
        }

        domainDbContext.DetachAggregate(aggregateId, aggregate);

        return aggregate;
    }

    private static List<EventEntity> TrackEventEntities(this IDomainDbContext domainDbContext, IStreamId streamId, IEvent[] events, int startingEventSequence)
    {
        var eventEntities = events.Select((@event, i) => @event.ToEventEntity(streamId, sequence: startingEventSequence + i)).ToList();
        domainDbContext.Events.AddRange(eventEntities);
        return eventEntities;
    }

    private static AggregateEntity TrackAggregateEntity<T>(this IDomainDbContext domainDbContext, IStreamId streamId, IAggregateId<T> aggregateId, IAggregateRoot aggregate, int newLatestEventSequence, bool aggregateIsNew) where T : IAggregateRoot
    {
        var aggregateEntity = aggregate.ToAggregateEntity(streamId, aggregateId, newLatestEventSequence);
        if (!aggregateIsNew)
        {
            domainDbContext.Aggregates.Update(aggregateEntity);
        }
        else
        {
            domainDbContext.Aggregates.Add(aggregateEntity);
        }
        return aggregateEntity;
    }

    private static List<AggregateEventEntity> TrackAggregateEventEntities(this IDomainDbContext domainDbContext, AggregateEntity aggregateEntity, List<EventEntity> eventEntities)
    {
        var aggregateEventEntities = eventEntities.Select(eventEntity => new AggregateEventEntity { AggregateId = aggregateEntity.Id, EventId = eventEntity.Id }).ToList();
        domainDbContext.AggregateEvents.AddRange(aggregateEventEntities);
        return aggregateEventEntities;
    }
}
