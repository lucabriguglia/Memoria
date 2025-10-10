using System.Collections.Concurrent;
using Memoria.EventSourcing.Domain;
using OpenCqrs.EventSourcing.Store.Cosmos.Documents;

namespace OpenCqrs.EventSourcing.Store.Cosmos.InMemory;

/// <summary>
/// Shared in-memory storage for Cosmos DB implementation.
/// This class provides thread-safe storage for aggregates, events, and aggregate-event relationships.
/// </summary>
public class InMemoryCosmosStorage
{
    public ConcurrentDictionary<string, AggregateDocument> AggregateDocuments { get; } = new();
    public ConcurrentDictionary<string, EventDocument> EventDocuments { get; } = new();
    public ConcurrentDictionary<string, ConcurrentBag<AggregateEventDocument>> AggregateEventDocuments { get; } = new();
    public ConcurrentDictionary<string, int> StreamSequences { get; } = new();

    public static string CreateAggregateKey<T>(IStreamId streamId, IAggregateId<T> aggregateId) where T : IAggregateRoot, new()
    {
        return $"{streamId.Id}#{aggregateId.ToStoreId()}";
    }

    public static string CreateEventKey(IStreamId streamId, int sequence)
    {
        return $"{streamId.Id}#{sequence}";
    }

    public static string GetEventTypeName(Type eventType)
    {
        var eventTypeName = TypeBindings.EventTypeBindings.FirstOrDefault(kvp => kvp.Value == eventType).Key;
        return eventTypeName ?? eventType.Name;
    }

    /// <summary>
    /// Clears all stored data. Useful for test cleanup.
    /// </summary>
    public void Clear()
    {
        AggregateDocuments.Clear();
        EventDocuments.Clear();
        AggregateEventDocuments.Clear();
        StreamSequences.Clear();
    }
}
