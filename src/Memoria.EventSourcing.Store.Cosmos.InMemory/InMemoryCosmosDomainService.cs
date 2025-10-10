using Memoria.EventSourcing;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Memoria.EventSourcing.Store.Cosmos.Extensions;
using Memoria.Extensions;
using Memoria.Results;
using Microsoft.AspNetCore.Http;

namespace OpenCqrs.EventSourcing.Store.Cosmos.InMemory;

public class InMemoryCosmosDomainService(
    InMemoryCosmosStorage storage,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : IDomainService
{
    private readonly InMemoryCosmosDataStore _dataStore = new(storage, timeProvider, httpContextAccessor);

    public async Task<Result<T?>> GetAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, ReadMode readMode = ReadMode.SnapshotOnly, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregateDocumentResult = await _dataStore.GetAggregateDocument(streamId, aggregateId, cancellationToken);
        if (aggregateDocumentResult.IsNotSuccess)
        {
            return aggregateDocumentResult.Failure!;
        }

        if (aggregateDocumentResult.Value != null)
        {
            var currentAggregateDocument = aggregateDocumentResult.Value;
            switch (readMode)
            {
                case ReadMode.SnapshotOnly or ReadMode.SnapshotOrCreate:
                    return currentAggregateDocument.ToAggregate<T>();
                case ReadMode.SnapshotWithNewEvents or ReadMode.SnapshotWithNewEventsOrCreate:
                    return await _dataStore.UpdateAggregateDocument(streamId, aggregateId, currentAggregateDocument, cancellationToken);
            }
        }

        if (readMode is ReadMode.SnapshotOnly or ReadMode.SnapshotWithNewEvents)
        {
            return default(T);
        }

        var aggregate = new T();

        var eventDocumentsResult = await _dataStore.GetEventDocuments(streamId, aggregate.EventTypeFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return default(T);
        }

        var events = eventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
        aggregate.Apply(events);
        if (aggregate.Version == 0)
        {
            return default(T);
        }

        var timeStamp = timeProvider.GetUtcNow();
        var currentUserNameIdentifier = httpContextAccessor.GetCurrentUserNameIdentifier();

        try
        {
            var latestEventSequenceForAggregate = eventDocuments.OrderBy(eventDocument => eventDocument.Sequence).Last().Sequence;
            var aggregateDocument = aggregate.ToAggregateDocument(streamId, aggregateId, latestEventSequenceForAggregate);
            aggregateDocument.CreatedDate = timeStamp;
            aggregateDocument.CreatedBy = currentUserNameIdentifier;
            aggregateDocument.UpdatedDate = timeStamp;
            aggregateDocument.UpdatedBy = currentUserNameIdentifier;

            var aggregateKey = InMemoryCosmosStorage.CreateAggregateKey(streamId, aggregateId);
            var aggregateAdded = storage.AggregateDocuments.TryAdd(aggregateKey, aggregateDocument);
            if (!aggregateAdded)
            {
                throw new Exception("Could not add aggregate");
            }

            foreach (var eventDocument in eventDocuments)
            {
                var aggregateEventDocument = new AggregateEventDocument
                {
                    Id = $"{aggregateId.ToStoreId()}|{eventDocument.Id}",
                    StreamId = streamId.Id,
                    AggregateId = aggregateId.ToStoreId(),
                    EventId = eventDocument.Id,
                    AppliedDate = timeStamp
                };

                if (!storage.AggregateEventDocuments.TryGetValue(aggregateKey, out var bag))
                {
                    bag = [];
                    storage.AggregateEventDocuments.TryAdd(aggregateKey, bag);
                }
                bag.Add(aggregateEventDocument);
            }

            return aggregate;
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Get Aggregate");
            return ErrorHandling.DefaultFailure;
        }
    }

    public async Task<Result<List<IEvent>>> GetEvents(IStreamId streamId, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult = await _dataStore.GetEventDocuments(streamId, eventTypeFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    public async Task<Result<List<IEvent>>> GetEventsAppliedToAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregateEventDocumentsResult = await _dataStore.GetAggregateEventDocuments(streamId, aggregateId, cancellationToken);
        if (aggregateEventDocumentsResult.IsNotSuccess)
        {
            return aggregateEventDocumentsResult.Failure!;
        }
        var aggregateEventDocuments = aggregateEventDocumentsResult.Value!;
        if (aggregateEventDocuments.Count == 0)
        {
            return new List<IEvent>();
        }

        var eventDocumentsResult = await _dataStore.GetEventDocuments(streamId, aggregateEventDocuments.Select(ae => ae.EventId).ToArray(), cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        var eventDocuments = eventDocumentsResult.Value!;
        return eventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    public async Task<Result<List<IEvent>>> GetEventsBetweenSequences(IStreamId streamId, int fromSequence, int toSequence, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult = await _dataStore.GetEventDocumentsBetweenSequences(streamId, fromSequence, toSequence, eventTypeFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    public async Task<Result<List<IEvent>>> GetEventsFromSequence(IStreamId streamId, int fromSequence, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult = await _dataStore.GetEventDocumentsFromSequence(streamId, fromSequence, eventTypeFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    public async Task<Result<List<IEvent>>> GetEventsUpToSequence(IStreamId streamId, int upToSequence, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult = await _dataStore.GetEventDocumentsUpToSequence(streamId, upToSequence, eventTypeFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    public async Task<Result<List<IEvent>>> GetEventsUpToDate(IStreamId streamId, DateTimeOffset upToDate, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult = await _dataStore.GetEventDocumentsUpToDate(streamId, upToDate, eventTypeFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    public async Task<Result<List<IEvent>>> GetEventsFromDate(IStreamId streamId, DateTimeOffset fromDate, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult = await _dataStore.GetEventDocumentsFromDate(streamId, fromDate, eventTypeFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    public async Task<Result<List<IEvent>>> GetEventsBetweenDates(IStreamId streamId, DateTimeOffset fromDate, DateTimeOffset toDate, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var eventDocumentsResult = await _dataStore.GetEventDocumentsBetweenDates(streamId, fromDate, toDate, eventTypeFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        return eventDocumentsResult.Value!.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
    }

    public async Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregate = new T();

        var eventDocumentsResult = await _dataStore.GetEventDocuments(streamId, aggregate.EventTypeFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return aggregate;
        }

        aggregate.StreamId = streamId.Id;
        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestEventSequence = eventDocuments.OrderBy(eventEntity => eventEntity.Sequence).Last().Sequence;
        aggregate.Apply(eventDocuments.Select(eventEntity => eventEntity.ToDomainEvent()));

        return aggregate;
    }

    public async Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, int upToSequence, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregate = new T();

        var eventDocumentsResult = await _dataStore.GetEventDocumentsUpToSequence(streamId, upToSequence, aggregate.EventTypeFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return aggregate;
        }

        aggregate.StreamId = streamId.Id;
        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestEventSequence = eventDocuments.OrderBy(eventEntity => eventEntity.Sequence).Last().Sequence;
        aggregate.Apply(eventDocuments.Select(eventEntity => eventEntity.ToDomainEvent()));

        return aggregate;
    }

    public async Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, DateTimeOffset upToDate, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregate = new T();

        var eventDocumentsResult = await _dataStore.GetEventDocumentsUpToDate(streamId, upToDate, aggregate.EventTypeFilter, cancellationToken);
        if (eventDocumentsResult.IsNotSuccess)
        {
            return eventDocumentsResult.Failure!;
        }
        var eventDocuments = eventDocumentsResult.Value!.ToList();
        if (eventDocuments.Count == 0)
        {
            return aggregate;
        }

        aggregate.StreamId = streamId.Id;
        aggregate.AggregateId = aggregateId.ToStoreId();
        aggregate.LatestEventSequence = eventDocuments.OrderBy(eventEntity => eventEntity.Sequence).Last().Sequence;
        aggregate.Apply(eventDocuments.Select(eventEntity => eventEntity.ToDomainEvent()));

        return aggregate;
    }

    public Task<Result<int>> GetLatestEventSequence(IStreamId streamId, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        var latestSequence = storage.StreamSequences.GetOrAdd(streamId.Id, 0);
        return Task.FromResult(Result<int>.Ok(latestSequence));
    }

    public async Task<Result> SaveAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, T aggregate, int expectedEventSequence, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        if (!aggregate.UncommittedEvents.Any())
        {
            return Result.Ok();
        }

        var latestEventSequenceResult = await GetLatestEventSequence(streamId, cancellationToken: cancellationToken);
        if (latestEventSequenceResult.IsNotSuccess)
        {
            return latestEventSequenceResult.Failure!;
        }
        var latestEventSequence = latestEventSequenceResult.Value;
        if (latestEventSequence != expectedEventSequence)
        {
            DiagnosticsExtensions.AddActivityEvent(streamId, expectedEventSequence, latestEventSequence);
            return ErrorHandling.DefaultFailure;
        }

        var newLatestEventSequenceForAggregate = latestEventSequence + aggregate.UncommittedEvents.Count();
        var currentAggregateVersion = aggregate.Version - aggregate.UncommittedEvents.Count();
        var aggregateIsNew = currentAggregateVersion == 0;

        var timeStamp = timeProvider.GetUtcNow();
        var currentUserNameIdentifier = httpContextAccessor.GetCurrentUserNameIdentifier();

        try
        {
            var aggregateDocument = aggregate.ToAggregateDocument(streamId, aggregateId, newLatestEventSequenceForAggregate);
            aggregateDocument.UpdatedDate = timeStamp;
            aggregateDocument.UpdatedBy = currentUserNameIdentifier;
            if (aggregateIsNew)
            {
                aggregateDocument.CreatedDate = timeStamp;
                aggregateDocument.CreatedBy = currentUserNameIdentifier;
            }
            else
            {
                var existingAggregateDocumentResult = await _dataStore.GetAggregateDocument(streamId, aggregateId, cancellationToken);
                if (existingAggregateDocumentResult.IsNotSuccess)
                {
                    return existingAggregateDocumentResult.Failure!;
                }
                var existingAggregateDocument = existingAggregateDocumentResult.Value;
                if (existingAggregateDocument != null)
                {
                    aggregateDocument.CreatedDate = existingAggregateDocument.CreatedDate;
                    aggregateDocument.CreatedBy = existingAggregateDocument.CreatedBy;
                }
                else
                {
                    aggregateDocument.CreatedDate = timeStamp;
                    aggregateDocument.CreatedBy = currentUserNameIdentifier;
                }
            }

            var aggregateKey = InMemoryCosmosStorage.CreateAggregateKey(streamId, aggregateId);
            var aggregateAlreadyExists = storage.AggregateDocuments.ContainsKey(aggregateKey);
            if (!aggregateAlreadyExists)
            {
                var aggregateAdded = storage.AggregateDocuments.TryAdd(aggregateKey, aggregateDocument);
                if (!aggregateAdded)
                {
                    throw new Exception("Could not add aggregate");
                }
            }
            else
            {
                storage.AggregateDocuments[aggregateKey] = aggregateDocument;
            }

            foreach (var @event in aggregate.UncommittedEvents)
            {
                latestEventSequence++;
                var eventDocument = @event.ToEventDocument(streamId, sequence: latestEventSequence);
                eventDocument.CreatedDate = timeStamp;
                eventDocument.CreatedBy = currentUserNameIdentifier;
                var eventKey = InMemoryCosmosStorage.CreateEventKey(streamId, eventDocument.Sequence);
                var evenAdded = storage.EventDocuments.TryAdd(eventKey, eventDocument);
                if (!evenAdded)
                {
                    throw new Exception("Could not add event");
                }

                var sequence = latestEventSequence;
                storage.StreamSequences.AddOrUpdate(streamId.Id, sequence, (key, oldValue) => sequence);

                var aggregateEventDocument = new AggregateEventDocument
                {
                    Id = $"{aggregateId.ToStoreId()}|{eventDocument.Id}",
                    StreamId = streamId.Id,
                    AggregateId = aggregateId.ToStoreId(),
                    EventId = eventDocument.Id,
                    AppliedDate = timeStamp
                };
                if (!storage.AggregateEventDocuments.TryGetValue(aggregateKey, out var bag))
                {
                    bag = [];
                    storage.AggregateEventDocuments.TryAdd(aggregateKey, bag);
                }
                bag.Add(aggregateEventDocument);
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Save Aggregate");
            return ErrorHandling.DefaultFailure;
        }
    }

    public async Task<Result> SaveEvents(IStreamId streamId, IEvent[] events, int expectedEventSequence, CancellationToken cancellationToken = default)
    {
        if (events.Length == 0)
        {
            return Result.Ok();
        }

        var latestEventSequenceResult = await GetLatestEventSequence(streamId, cancellationToken: cancellationToken);
        if (latestEventSequenceResult.IsNotSuccess)
        {
            return latestEventSequenceResult.Failure!;
        }
        var latestEventSequence = latestEventSequenceResult.Value;
        if (latestEventSequence != expectedEventSequence)
        {
            DiagnosticsExtensions.AddActivityEvent(streamId, expectedEventSequence, latestEventSequence);
            return ErrorHandling.DefaultFailure;
        }

        var timeStamp = timeProvider.GetUtcNow();
        var currentUserNameIdentifier = httpContextAccessor.GetCurrentUserNameIdentifier();

        try
        {
            foreach (var @event in events)
            {
                latestEventSequence++;
                var eventDocument = @event.ToEventDocument(streamId, sequence: latestEventSequence);
                eventDocument.CreatedDate = timeStamp;
                eventDocument.CreatedBy = currentUserNameIdentifier;
                var eventKey = InMemoryCosmosStorage.CreateEventKey(streamId, eventDocument.Sequence);
                var evenAdded = storage.EventDocuments.TryAdd(eventKey, eventDocument);
                if (!evenAdded)
                {
                    throw new Exception("Could not add event");
                }

                var sequence = latestEventSequence;
                storage.StreamSequences.AddOrUpdate(streamId.Id, sequence, (key, oldValue) => sequence);
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            ex.AddException(streamId, operation: "Save Domain Events");
            return ErrorHandling.DefaultFailure;
        }
    }

    public async Task<Result<T?>> UpdateAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregateDocumentResult = await _dataStore.GetAggregateDocument(streamId, aggregateId, cancellationToken);
        if (aggregateDocumentResult.IsNotSuccess)
        {
            return aggregateDocumentResult.Failure!;
        }
        var aggregateDocument = aggregateDocumentResult.Value;
        return await _dataStore.UpdateAggregateDocument(streamId, aggregateId, aggregateDocument, cancellationToken);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
