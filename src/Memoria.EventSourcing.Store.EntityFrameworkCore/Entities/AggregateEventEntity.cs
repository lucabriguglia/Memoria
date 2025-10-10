// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;

/// <summary>
/// Represents a many-to-many relationship entity linking aggregates to domain events.
/// </summary>
public class AggregateEventEntity : IApplicableEntity
{
    /// <summary>
    /// Gets or sets the aggregate ID.
    /// </summary>
    public string AggregateId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the event ID.
    /// </summary>
    public string EventId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the applied date.
    /// </summary>
    public DateTimeOffset AppliedDate { get; set; }

    /// <summary>
    /// Gets or sets the associated aggregate entity.
    /// </summary>
    public virtual AggregateEntity Aggregate { get; set; } = null!;

    /// <summary>
    /// Gets or sets the associated event entity.
    /// </summary>
    public virtual EventEntity Event { get; set; } = null!;
}
