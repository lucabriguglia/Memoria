using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Memoria.Results;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    private static async Task<Result<T?>> UpdateAggregate<T>(this IDomainDbContext domainDbContext, IStreamId streamId,
        IAggregateId<T> aggregateId, T aggregate, CancellationToken cancellationToken = default)
        where T : IAggregateRoot, new()
    {
        var currentAggregateVersion = aggregate.Version;

        var newEventEntities = await domainDbContext.GetEventEntitiesFromSequence(streamId,
            fromSequence: aggregate.LatestEventSequence + 1, aggregate.EventTypeFilter, aggregateId.EventPropertyFilter,
            cancellationToken: cancellationToken);
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

        var latestEventSequenceForAggregate =
            newEventEntities.OrderBy(eventEntity => eventEntity.Sequence).Last().Sequence;
        var trackedAggregateEntity = domainDbContext.TrackAggregateEntity(streamId, aggregateId, aggregate,
            latestEventSequenceForAggregate, aggregateIsNew: currentAggregateVersion == 0);
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

    private static async Task<Result<T?>> UpdateProjection<T>(this IDomainDbContext domainDbContext,
        IStreamId streamId, IProjectionId<T> projectionId, T projection,
        CancellationToken cancellationToken = default) where T : IProjection, new()
    {
        var currentProjectionVersion = projection.Version;

        var newEventEntities = await domainDbContext.GetEventEntitiesFromSequence(streamId,
            fromSequence: projection.LatestEventSequence + 1, projection.EventTypeFilter,
            cancellationToken: cancellationToken);
        if (newEventEntities.Count == 0)
        {
            return projection.Version > 0 ? projection : default;
        }

        var newEvents = newEventEntities.Select(eventEntity => eventEntity.ToDomainEvent()).ToList();
        projection.Apply(newEvents);

        if (projection.Version == currentProjectionVersion)
        {
            return projection.Version > 0 ? projection : default;
        }

        projection.LatestEventSequence =
            newEventEntities.OrderBy(eventEntity => eventEntity.Sequence).Last().Sequence;

        try
        {
            var projectionEntity = projection.ToProjectionEntity(streamId, projectionId);
            var projectionIsNew = currentProjectionVersion == 0;
            if (projectionIsNew)
            {
                domainDbContext.Projections.Add(projectionEntity);
            }
            else
            {
                domainDbContext.Projections.Update(projectionEntity);
            }

            await domainDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Update Projection");
            return ErrorHandling.DefaultFailure;
        }

        domainDbContext.DetachProjection(projectionId, projection);

        return projection;
    }

    private static List<EventEntity> TrackEventEntities(this IDomainDbContext domainDbContext, IStreamId streamId,
        IEvent[] events, int startingEventSequence)
    {
        var eventEntities = events
            .Select((@event, i) => @event.ToEventEntity(streamId, sequence: startingEventSequence + i)).ToList();
        domainDbContext.Events.AddRange(eventEntities);
        return eventEntities;
    }

    private static AggregateEntity TrackAggregateEntity<T>(this IDomainDbContext domainDbContext, IStreamId streamId,
        IAggregateId<T> aggregateId, IAggregateRoot aggregate, int newLatestEventSequence, bool aggregateIsNew)
        where T : IAggregateRoot
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

    private static List<AggregateEventEntity> TrackAggregateEventEntities(this IDomainDbContext domainDbContext,
        AggregateEntity aggregateEntity, List<EventEntity> eventEntities)
    {
        var aggregateEventEntities = eventEntities.Select(eventEntity => new AggregateEventEntity
            { AggregateId = aggregateEntity.Id, EventId = eventEntity.Id }).ToList();
        domainDbContext.AggregateEvents.AddRange(aggregateEventEntities);
        return aggregateEventEntities;
    }
}