using Memoria.EventSourcing.Domain;
using Memoria.Results;

namespace Memoria.EventSourcing;

public class DefaultDomainService : IDomainService
{
    private static string NotImplementedMessage => "No store provider has been configured. Please register a store provider such as Cosmos or Entity Framework Core.";

    public Task<Result<T?>> GetAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, ReadMode readMode,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<List<IEvent>>> GetEvents(IStreamId streamId, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<List<IEvent>>> GetEventsAppliedToAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<List<IEvent>>> GetEventsBetweenSequences(IStreamId streamId, int fromSequence, int toSequence, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<List<IEvent>>> GetEventsFromSequence(IStreamId streamId, int fromSequence, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<List<IEvent>>> GetEventsUpToSequence(IStreamId streamId, int upToSequence, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<List<IEvent>>> GetEventsUpToDate(IStreamId streamId, DateTimeOffset upToDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<List<IEvent>>> GetEventsFromDate(IStreamId streamId, DateTimeOffset fromDate, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<List<IEvent>>> GetEventsBetweenDates(IStreamId streamId, DateTimeOffset fromDate, DateTimeOffset toDate,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, int upToSequence,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<T>> GetInMemoryAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, DateTimeOffset upToDate,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<int>> GetLatestEventSequence(IStreamId streamId, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result> SaveAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, T aggregate, int expectedEventSequence,
        CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result> SaveEvents(IStreamId streamId, IEvent[] events, int expectedEventSequence,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public Task<Result<T?>> UpdateAggregate<T>(IStreamId streamId, IAggregateId<T> aggregateId, CancellationToken cancellationToken = default) where T : IAggregateRoot, new()
    {
        throw new NotImplementedException(NotImplementedMessage);
    }

    public void Dispose()
    {
        throw new NotImplementedException(NotImplementedMessage);
    }
}
