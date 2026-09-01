using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Memoria.EventSourcing.Store.Cosmos.Extensions;
using Memoria.Extensions;
using Memoria.Results;
using Microsoft.AspNetCore.Http;

namespace Memoria.EventSourcing.Store.Cosmos.InMemory;

/// <summary>
/// In-memory implementation of ICosmosDataStore for fast testing.
/// Uses shared InMemoryCosmosStorage for data persistence.
/// </summary>
public class InMemoryCosmosDataStore(InMemoryCosmosStorage storage, TimeProvider timeProvider, IHttpContextAccessor httpContextAccessor) : ICosmosDataStore
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

    public Task<Result<List<EventDocument>>> GetEventDocuments(
        IStreamId streamId,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => MatchesEventTypeFilter(doc, eventTypeFilter))
            .Where(doc => MatchesEventPropertyFilter(doc, eventPropertyFilter))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsBetweenSequences(
        IStreamId streamId,
        int fromSequence,
        int toSequence,
        Type[]? eventTypeFilter,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.Sequence >= fromSequence && doc.Sequence <= toSequence)
            .Where(doc => MatchesEventTypeFilter(doc, eventTypeFilter))
            .Where(doc => MatchesEventPropertyFilter(doc, eventPropertyFilter))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsFromSequence(
        IStreamId streamId,
        int fromSequence,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.Sequence >= fromSequence)
            .Where(doc => MatchesEventTypeFilter(doc, eventTypeFilter))
            .Where(doc => MatchesEventPropertyFilter(doc, eventPropertyFilter))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsUpToSequence(
        IStreamId streamId,
        int upToSequence,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.Sequence <= upToSequence)
            .Where(doc => MatchesEventTypeFilter(doc, eventTypeFilter))
            .Where(doc => MatchesEventPropertyFilter(doc, eventPropertyFilter))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsUpToDate(
        IStreamId streamId,
        DateTimeOffset upToDate,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.CreatedDate <= upToDate)
            .Where(doc => MatchesEventTypeFilter(doc, eventTypeFilter))
            .Where(doc => MatchesEventPropertyFilter(doc, eventPropertyFilter))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsFromDate(
        IStreamId streamId,
        DateTimeOffset fromDate,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.CreatedDate >= fromDate)
            .Where(doc => MatchesEventTypeFilter(doc, eventTypeFilter))
            .Where(doc => MatchesEventPropertyFilter(doc, eventPropertyFilter))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    public Task<Result<List<EventDocument>>> GetEventDocumentsBetweenDates(
        IStreamId streamId,
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        Type[]? eventTypeFilter = null,
        IDictionary<string, string>? eventPropertyFilter = null,
        CancellationToken cancellationToken = default)
    {
        var documents = storage.EventDocuments.Values
            .Where(doc => doc.StreamId == streamId.Id)
            .Where(doc => doc.CreatedDate >= fromDate && doc.CreatedDate <= toDate)
            .Where(doc => MatchesEventTypeFilter(doc, eventTypeFilter))
            .Where(doc => MatchesEventPropertyFilter(doc, eventPropertyFilter))
            .OrderBy(doc => doc.Sequence)
            .ToList();

        return Task.FromResult(Result<List<EventDocument>>.Ok(documents));
    }

    private static bool MatchesEventTypeFilter(EventDocument document, Type[]? eventTypeFilter)
    {
        if (eventTypeFilter is not { Length: > 0 })
        {
            return true;
        }

        return eventTypeFilter.Any(t => InMemoryCosmosStorage.GetEventTypeName(t) == document.EventType);
    }

    private static bool MatchesEventPropertyFilter(EventDocument document, IDictionary<string, string>? eventPropertyFilter)
    {
        if (eventPropertyFilter is not { Count: > 0 })
        {
            return true;
        }

        foreach (var filter in eventPropertyFilter)
        {
            var propertyFilter = $"\"{filter.Key}\":\"{filter.Value}\"";
            if (!document.Data.Contains(propertyFilter))
            {
                return false;
            }
        }

        return true;
    }

    public async Task<Result<T?>> UpdateAggregateDocument<T>(
        IStreamId streamId,
        IAggregateId<T> aggregateId,
        AggregateDocument? aggregateDocument,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        var aggregateKey = InMemoryCosmosStorage.CreateAggregateKey(streamId, aggregateId);

        var aggregate = aggregateDocument is null ? new T() : aggregateDocument.ToAggregate<T>();

        var currentAggregateVersion = aggregate.Version;

        var newEventDocumentsResult = await GetEventDocumentsFromSequence(streamId, fromSequence: aggregate.LatestEventSequence + 1, aggregate.EventTypeFilter, cancellationToken: cancellationToken);
        if (newEventDocumentsResult.IsNotSuccess)
        {
            return newEventDocumentsResult.Failure!;
        }
        var newEventDocuments = newEventDocumentsResult.Value!;
        if (newEventDocuments.Count == 0)
        {
            return aggregate.Version > 0 ? aggregate : default;
        }

        var newEvents = newEventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
        aggregate.Apply(newEvents);

        AggregateDiagnostics.AddAggregateFoldedEvent(streamId, aggregateId,
            appliedFromSequence: newEventDocuments.Min(eventDocument => eventDocument.Sequence),
            appliedToSequence: newEventDocuments.Max(eventDocument => eventDocument.Sequence),
            appliedCount: newEventDocuments.Count, versionBefore: currentAggregateVersion,
            versionAfter: aggregate.Version);

        if (aggregate.Version == currentAggregateVersion)
        {
            return aggregate.Version > 0 ? aggregate : default;
        }

        var newLatestEventSequenceForAggregate = newEventDocuments.OrderBy(eventEntity => eventEntity.Sequence).Last().Sequence;
        var timeStamp = timeProvider.GetUtcNow();
        var currentUserNameIdentifier = httpContextAccessor.GetCurrentUserNameIdentifier();

        var aggregateDocumentToUpdate = aggregate.ToAggregateDocument(streamId, aggregateId, newLatestEventSequenceForAggregate);
        aggregateDocumentToUpdate.CreatedDate = aggregateDocument?.CreatedDate ?? timeStamp;
        aggregateDocumentToUpdate.CreatedBy = aggregateDocument?.CreatedBy ?? currentUserNameIdentifier;
        aggregateDocumentToUpdate.UpdatedDate = timeStamp;
        aggregateDocumentToUpdate.UpdatedBy = currentUserNameIdentifier;
        storage.AggregateDocuments.AddOrUpdate(aggregateKey, aggregateDocumentToUpdate, (_, _) => aggregateDocumentToUpdate);

        return aggregate;
    }

    public Task<Result<ProjectionDocument?>> GetProjectionDocument<T>(IStreamId streamId,
        IProjectionId<T> projectionId, CancellationToken cancellationToken = default)
        where T : IProjection, new()
    {
        var key = CreateProjectionKey(streamId, projectionId);
        var document = storage.ProjectionDocuments.TryGetValue(key, out var projectionDocument)
            ? projectionDocument
            : null;
        return Task.FromResult(Result<ProjectionDocument?>.Ok(document));
    }

    public async Task<Result<T?>> UpdateProjectionDocument<T>(IStreamId streamId,
        IProjectionId<T> projectionId, ProjectionDocument? projectionDocument,
        CancellationToken cancellationToken = default) where T : IProjection, new()
    {
        var projection = projectionDocument is null ? new T() : projectionDocument.ToProjection<T>();

        var currentProjectionVersion = projection.Version;

        var newEventDocumentsResult = await GetEventDocumentsFromSequence(streamId,
            fromSequence: projection.LatestEventSequence + 1, projection.EventTypeFilter, projectionId.EventPropertyFilter,
            cancellationToken: cancellationToken);
        if (newEventDocumentsResult.IsNotSuccess)
        {
            return newEventDocumentsResult.Failure!;
        }

        var newEventDocuments = newEventDocumentsResult.Value!;
        if (newEventDocuments.Count == 0)
        {
            return projection.Version > 0 ? projection : default;
        }

        var newEvents = newEventDocuments.Select(eventDocument => eventDocument.ToDomainEvent()).ToList();
        projection.Apply(newEvents);

        var foldedSequences = newEventDocuments.Select(eventDocument => eventDocument.Sequence).ToList();
        ProjectionDiagnostics.AddProjectionFoldedEvent(streamId, projectionId,
            appliedFromSequence: foldedSequences.Min(), appliedToSequence: foldedSequences.Max(),
            appliedCount: newEventDocuments.Count, versionBefore: currentProjectionVersion,
            versionAfter: projection.Version);

        if (projection.Version == currentProjectionVersion)
        {
            return projection.Version > 0 ? projection : default;
        }

        projection.LatestEventSequence =
            newEventDocuments.OrderBy(eventDocument => eventDocument.Sequence).Last().Sequence;

        var timeStamp = timeProvider.GetUtcNow();
        var currentUserNameIdentifier = httpContextAccessor.GetCurrentUserNameIdentifier();

        var projectionDocumentToUpsert = projection.ToProjectionDocument(streamId, projectionId);
        projectionDocumentToUpsert.CreatedDate = projectionDocument?.CreatedDate ?? timeStamp;
        projectionDocumentToUpsert.CreatedBy = projectionDocument?.CreatedBy ?? currentUserNameIdentifier;
        projectionDocumentToUpsert.UpdatedDate = timeStamp;
        projectionDocumentToUpsert.UpdatedBy = currentUserNameIdentifier;

        var key = CreateProjectionKey(streamId, projectionId);
        storage.ProjectionDocuments.AddOrUpdate(key, projectionDocumentToUpsert, (_, _) => projectionDocumentToUpsert);

        return projection;
    }

    private static string CreateProjectionKey<T>(IStreamId streamId, IProjectionId<T> projectionId)
        where T : IProjection => $"{streamId.Id}#{projectionId.ToStoreId()}";

    public void Dispose()
    {
        // Storage is shared, so we don't clear it here
        GC.SuppressFinalize(this);
    }
}
