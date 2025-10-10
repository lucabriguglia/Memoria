namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;

/// <summary>
/// Defines an entity with an applied date.
/// </summary>
public interface IApplicableEntity
{
    /// <summary>
    /// Gets or sets the applied date.
    /// </summary>
    DateTimeOffset AppliedDate { get; set; }
}
