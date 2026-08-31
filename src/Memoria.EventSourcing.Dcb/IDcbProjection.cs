using Memoria.EventSourcing.Domain;

namespace Memoria.EventSourcing.Dcb;

/// <summary>
/// Defines the contract for a DCB read model. Like an <see cref="IProjection"/> it rebuilds a
/// query-optimised view by applying events and never produces new ones, so it has no notion of
/// staging. Like an <see cref="IDcbAggregateRoot"/> it belongs to no stream: the events it folds
/// are selected by a <see cref="TagQuery"/>.
/// </summary>
public interface IDcbProjection : IDcbModel
{
    /// <summary>
    /// Gets or sets the unique identifier for this projection instance.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies this specific projection snapshot within its type. Used to
    /// key the snapshot; it does not select the events the projection is built from.
    /// </value>
    string ProjectionId { get; set; }
}
