namespace Memoria.EventSourcing.Domain;

/// <summary>
/// Defines the contract for projections (read models) in the event sourcing domain model.
/// A projection rebuilds and exposes a query-optimised view of state by applying domain events,
/// but unlike an <see cref="IAggregateRoot"/> it never produces new events, so it has no notion
/// of staging or committing uncommitted events.
/// </summary>
public interface IProjection : IEventSourcedModel
{
    /// <summary>
    /// Gets or sets the unique identifier for this projection instance.
    /// </summary>
    /// <value>
    /// A string that uniquely identifies this specific projection snapshot within its type.
    /// This serves as the primary key for the projection and should remain constant throughout its lifetime.
    /// </value>
    string ProjectionId { get; set; }
}
