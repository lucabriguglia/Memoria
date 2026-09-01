using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;

/// <summary>
/// Converts between DCB models and their persisted snapshots.
/// </summary>
public static class SnapshotExtensions
{
    /// <summary>
    /// Builds the snapshot row for an aggregate folded under a boundary.
    /// </summary>
    public static DcbSnapshotEntity ToSnapshotEntity<T>(this T aggregate, IDcbAggregateId<T> aggregateId)
        where T : IDcbAggregateRoot
    {
        aggregate.AggregateId = aggregateId.ToStoreId();
        var query = aggregateId.Boundary;

        return new DcbSnapshotEntity
        {
            Id = DcbSnapshotEntity.BuildId(DcbSnapshotEntity.AggregateKind, aggregateId.ToStoreId(), query),
            SnapshotKind = DcbSnapshotEntity.AggregateKind,
            StoreId = aggregateId.ToStoreId(),
            TagQuery = query.ToString(),
            ModelType = DcbTypeBindings.GetAggregateBindingKey(aggregate.GetType()),
            Version = aggregate.Version,
            LatestPosition = aggregate.LatestPosition,
            Data = DomainSerializer.Current.Serialize(aggregate)
        };
    }

    /// <summary>
    /// Builds the snapshot row for a projection folded under a boundary.
    /// </summary>
    public static DcbSnapshotEntity ToSnapshotEntity<T>(this T projection, IDcbProjectionId<T> projectionId)
        where T : IDcbProjection
    {
        projection.ProjectionId = projectionId.ToStoreId();
        var query = projectionId.Boundary;

        return new DcbSnapshotEntity
        {
            Id = DcbSnapshotEntity.BuildId(DcbSnapshotEntity.ProjectionKind, projectionId.ToStoreId(), query),
            SnapshotKind = DcbSnapshotEntity.ProjectionKind,
            StoreId = projectionId.ToStoreId(),
            TagQuery = query.ToString(),
            ModelType = DcbTypeBindings.GetProjectionBindingKey(projection.GetType()),
            Version = projection.Version,
            LatestPosition = projection.LatestPosition,
            Data = DomainSerializer.Current.Serialize(projection)
        };
    }

    /// <summary>
    /// Rebuilds an aggregate from its snapshot row.
    /// </summary>
    public static T ToAggregate<T>(this DcbSnapshotEntity snapshot) where T : IDcbAggregateRoot
    {
        var found = DcbTypeBindings.AggregateTypeBindings.TryGetValue(snapshot.ModelType, out var modelType);
        if (found is false)
        {
            throw new InvalidOperationException(
                $"DCB aggregate type {snapshot.ModelType} not found in DcbTypeBindings");
        }

        var aggregate = (T)DomainSerializer.Current.Deserialize(snapshot.Data, modelType!);
        aggregate.AggregateId = snapshot.StoreId;
        aggregate.Version = snapshot.Version;
        aggregate.LatestPosition = snapshot.LatestPosition;
        return aggregate;
    }

    /// <summary>
    /// Rebuilds a projection from its snapshot row.
    /// </summary>
    public static T ToProjection<T>(this DcbSnapshotEntity snapshot) where T : IDcbProjection
    {
        var found = DcbTypeBindings.ProjectionTypeBindings.TryGetValue(snapshot.ModelType, out var modelType);
        if (found is false)
        {
            throw new InvalidOperationException(
                $"DCB projection type {snapshot.ModelType} not found in DcbTypeBindings");
        }

        var projection = (T)DomainSerializer.Current.Deserialize(snapshot.Data, modelType!);
        projection.ProjectionId = snapshot.StoreId;
        projection.Version = snapshot.Version;
        projection.LatestPosition = snapshot.LatestPosition;
        return projection;
    }
}
