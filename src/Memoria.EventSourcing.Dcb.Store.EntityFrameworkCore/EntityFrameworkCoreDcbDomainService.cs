using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Domain;
using Memoria.Results;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;

/// <summary>
/// The Entity Framework Core implementation of <see cref="IDcbDomainService"/>.
/// </summary>
/// <param name="dcbDbContext">The context to read and append through.</param>
/// <param name="maxEventsPerAppend">
/// The batch limit for a single append. Defaults to
/// <see cref="DcbDbContextExtensions.DefaultMaxEventsPerAppend"/>.
/// </param>
/// <remarks>
/// A thin front for the context extension methods, which are the store's real surface and remain
/// usable directly. The service exists so an application can depend on the abstraction and swap
/// providers, exactly as <c>EntityFrameworkCoreDomainService</c> does for the streamed model.
/// </remarks>
public class EntityFrameworkCoreDcbDomainService(
    IDcbDbContext dcbDbContext,
    int maxEventsPerAppend = DcbDbContextExtensions.DefaultMaxEventsPerAppend)
    : IDcbDomainService
{
    /// <inheritdoc />
    public Task<Result<T?>> GetAggregate<T>(IDcbAggregateId<T> aggregateId,
        ReadMode readMode = ReadMode.SnapshotOnly, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot, new() =>
        dcbDbContext.GetAggregate(aggregateId, readMode, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<List<IEvent>>> GetEvents(TagQuery query, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        await Guarded("Get Events", query, () => dcbDbContext.GetEvents(query, eventTypeFilter, cancellationToken));

    /// <inheritdoc />
    public async Task<Result<List<IEvent>>> GetEventsFromPosition(TagQuery query, long fromPosition,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        await Guarded("Get Events From Position", query,
            () => dcbDbContext.GetEventsFromPosition(query, fromPosition, eventTypeFilter, cancellationToken));

    /// <inheritdoc />
    public async Task<Result<List<IEvent>>> GetEventsUpToPosition(TagQuery query, long upToPosition,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        await Guarded("Get Events Up To Position", query,
            () => dcbDbContext.GetEventsUpToPosition(query, upToPosition, eventTypeFilter, cancellationToken));

    /// <inheritdoc />
    public async Task<Result<List<IEvent>>> GetEventsBetweenPositions(TagQuery query, long fromPosition,
        long toPosition, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        await Guarded("Get Events Between Positions", query,
            () => dcbDbContext.GetEventsBetweenPositions(query, fromPosition, toPosition, eventTypeFilter,
                cancellationToken));

    /// <inheritdoc />
    public async Task<Result<List<IEvent>>> GetEventsFromDate(TagQuery query, DateTimeOffset fromDate,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        await Guarded("Get Events From Date", query,
            () => dcbDbContext.GetEventsFromDate(query, fromDate, eventTypeFilter, cancellationToken));

    /// <inheritdoc />
    public async Task<Result<List<IEvent>>> GetEventsUpToDate(TagQuery query, DateTimeOffset upToDate,
        Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        await Guarded("Get Events Up To Date", query,
            () => dcbDbContext.GetEventsUpToDate(query, upToDate, eventTypeFilter, cancellationToken));

    /// <inheritdoc />
    public async Task<Result<List<IEvent>>> GetEventsBetweenDates(TagQuery query, DateTimeOffset fromDate,
        DateTimeOffset toDate, Type[]? eventTypeFilter = null, CancellationToken cancellationToken = default) =>
        await Guarded("Get Events Between Dates", query,
            () => dcbDbContext.GetEventsBetweenDates(query, fromDate, toDate, eventTypeFilter, cancellationToken));

    /// <inheritdoc />
    public async Task<Result<long>> GetLatestPosition(TagQuery query, Type[]? eventTypeFilter = null,
        CancellationToken cancellationToken = default) =>
        await Guarded("Get Latest Position", query,
            () => dcbDbContext.GetLatestPosition(query, eventTypeFilter, cancellationToken));

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryAggregate<T>(IDcbAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new() =>
        dcbDbContext.GetInMemoryAggregate(aggregateId, cancellationToken);

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryAggregate<T>(IDcbAggregateId<T> aggregateId,
        long upToPosition, CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new() =>
        dcbDbContext.GetInMemoryAggregate(aggregateId, upToPosition, cancellationToken);

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryAggregate<T>(IDcbAggregateId<T> aggregateId,
        DateTimeOffset upToDate, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot, new() =>
        dcbDbContext.GetInMemoryAggregate(aggregateId, upToDate, cancellationToken);

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryProjection<T>(IDcbProjectionId<T> projectionId,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new() =>
        dcbDbContext.GetInMemoryProjection(projectionId, cancellationToken);

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryProjection<T>(IDcbProjectionId<T> projectionId,
        long upToPosition, CancellationToken cancellationToken = default) where T : IDcbProjection, new() =>
        dcbDbContext.GetInMemoryProjection(projectionId, upToPosition, cancellationToken);

    /// <inheritdoc />
    public Task<Result<T>> GetInMemoryProjection<T>(IDcbProjectionId<T> projectionId,
        DateTimeOffset upToDate, CancellationToken cancellationToken = default)
        where T : IDcbProjection, new() =>
        dcbDbContext.GetInMemoryProjection(projectionId, upToDate, cancellationToken);

    /// <inheritdoc />
    public Task<Result<T?>> GetProjection<T>(IDcbProjectionId<T> projectionId,
        ReadMode readMode = ReadMode.SnapshotOnly, CancellationToken cancellationToken = default)
        where T : IDcbProjection, new() =>
        dcbDbContext.GetProjection(projectionId, readMode, cancellationToken);

    /// <inheritdoc />
    public Task<Result> SaveProjection<T>(IDcbProjectionId<T> projectionId, T projection,
        CancellationToken cancellationToken = default) where T : IDcbProjection =>
        dcbDbContext.SaveProjection(projectionId, projection, cancellationToken);

    /// <inheritdoc />
    public Task<Result> SaveAggregate<T>(IDcbAggregateId<T> aggregateId, T aggregate,
        AppendCondition? condition, CancellationToken cancellationToken = default)
        where T : IDcbAggregateRoot =>
        dcbDbContext.SaveAggregate(aggregateId, aggregate, condition, maxEventsPerAppend, cancellationToken);

    /// <inheritdoc />
    public Task<Result> SaveEvents(TaggedEvent[] events, AppendCondition? condition,
        CancellationToken cancellationToken = default) =>
        dcbDbContext.SaveEvents(events, condition, maxEventsPerAppend, cancellationToken);

    /// <inheritdoc />
    public Task<Result<T?>> UpdateAggregate<T>(IDcbAggregateId<T> aggregateId,
        CancellationToken cancellationToken = default) where T : IDcbAggregateRoot, new() =>
        dcbDbContext.UpdateAggregate(aggregateId, cancellationToken);

    /// <inheritdoc />
    public Task<Result<T?>> UpdateProjection<T>(IDcbProjectionId<T> projectionId,
        CancellationToken cancellationToken = default) where T : IDcbProjection, new() =>
        dcbDbContext.UpdateProjection(projectionId, cancellationToken);

    /// <summary>
    /// Maps an unhandled provider exception onto a storage failure.
    /// </summary>
    /// <remarks>
    /// The read extensions deliberately do not catch: they are usable directly, where an exception is
    /// the clearer signal. The service is the boundary where the contract says every outcome is a
    /// <see cref="Result{TValue}"/>, so the translation belongs here.
    /// </remarks>
    private static async Task<Result<TValue>> Guarded<TValue>(string operation, TagQuery query, Func<Task<TValue>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception exception)
        {
            DcbDiagnostics.AddException(exception, operation, query);
            return DcbStoreFailures.StorageFailure(operation, query);
        }
    }

    /// <inheritdoc />
    public void Dispose() => GC.SuppressFinalize(this);
}
