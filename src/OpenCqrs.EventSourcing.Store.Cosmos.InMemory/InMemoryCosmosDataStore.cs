using OpenCqrs.EventSourcing.Domain;
using OpenCqrs.EventSourcing.Store.Cosmos.Documents;
using OpenCqrs.Results;

namespace OpenCqrs.EventSourcing.Store.Cosmos.InMemory;

/// <summary>
/// In-memory implementation of ICosmosDataStore for fast testing.
/// Uses shared InMemoryCosmosStorage for data persistence.
/// </summary>
public class InMemoryCosmosDataStore(InMemoryCosmosStorage storage) : ICosmosDataStore
{
    public Task<Result<AggregateDocument?>> GetAggregateDocument<T>(
        IStreamId streamId,
        IAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var key = InMemoryCosmosStorage.CreateAggregateKey(streamId, aggregateId);
        var document = storage.AggregateDocuments.TryGetValue(key, out var aggregateDocument)
            ? aggregateDocument
            : null;
        return Task.FromResult(Result<AggregateDocument?>.Ok(document));
    }

    public Task<Result<List<AggregateEventDocument>>> GetAggregateEventDocuments<T>(
        IStreamId streamId,
        IAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var key = InMemoryCosmosStorage.CreateAggregateKey(streamId, aggregateId);
        var documents = storage.AggregateEventDocuments.TryGetValue(key, out var eventDocuments)
            ? eventDocuments.OrderBy(d => d.AppliedDate).ToList()
            : [];
        return Task.FromResult(Result<List<AggregateEventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocuments(
        IStreamId streamId,
        Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => eventTypeFilter == null || eventTypeFilter.Any(t => InMemoryCosmosStorage.GetEventTypeName(t) == doc.EventType))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocuments(
        IStreamId streamId,
        string[] eventIds,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id && eventIds.Contains(doc.Id))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsBetweenSequences(
        IStreamId streamId,
        int fromSequence,
        int toSequence,
        Type[]? eventTypeFilter,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.Sequence >= fromSequence && doc.Sequence <= toSequence)
            .Where(doc => eventTypeFilter == null || eventTypeFilter.Any(t => InMemoryCosmosStorage.GetEventTypeName(t) == doc.EventType))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsFromSequence(
        IStreamId streamId,
        int fromSequence,
        Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.Sequence >= fromSequence)
            .Where(doc => eventTypeFilter == null || eventTypeFilter.Any(t => InMemoryCosmosStorage.GetEventTypeName(t) == doc.EventType))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsUpToSequence(
        IStreamId streamId,
        int upToSequence,
        Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.Sequence <= upToSequence)
            .Where(doc => eventTypeFilter == null || eventTypeFilter.Any(t => InMemoryCosmosStorage.GetEventTypeName(t) == doc.EventType))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsUpToDate(
        IStreamId streamId,
        DateTimeOffset upToDate,
        Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.CreatedDate <= upToDate)
            .Where(doc => eventTypeFilter == null || eventTypeFilter.Any(t => InMemoryCosmosStorage.GetEventTypeName(t) == doc.EventType))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsFromDate(
        IStreamId streamId,
        DateTimeOffset fromDate,
        Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.CreatedDate >= fromDate)
            .Where(doc => eventTypeFilter == null || eventTypeFilter.Any(t => InMemoryCosmosStorage.GetEventTypeName(t) == doc.EventType))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsBetweenDates(
        IStreamId streamId,
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.CreatedDate >= fromDate && doc.CreatedDate <= toDate)
            .Where(doc => eventTypeFilter == null || eventTypeFilter.Any(t => InMemoryCosmosStorage.GetEventTypeName(t) == doc.EventType))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<T?>> UpdateAggregateDocument<T>(
        IStreamId streamId,
        IAggregateId<T> aggregateId,
        AggregateDocument? aggregateDocument,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var key = InMemoryCosmosStorage.CreateAggregateKey(streamId, aggregateId);

        if (aggregateDocument == null)
        {
            storage.AggregateDocuments.TryRemove(key, out _);
            return Task.FromResult(Result<T?>.Ok(default(T)));
        }

        storage.AggregateDocuments.AddOrUpdate(key, aggregateDocument, (_, _) => aggregateDocument);
        var aggregate = aggregateDocument.ToAggregate<T>();
        return Task.FromResult(Result<T?>.Ok(aggregate));
    }

    public void Dispose()
    {
        // Storage is shared, so we don't clear it here
        GC.SuppressFinalize(this);
    }
}
