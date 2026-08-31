namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;

/// <summary>
/// An entity whose creation is stamped automatically by <see cref="Interceptors.AuditInterceptor"/>.
/// </summary>
/// <remarks>
/// Deliberately declared here rather than reused from
/// <c>Memoria.EventSourcing.Store.EntityFrameworkCore</c>: referencing that package would pull the
/// whole streamed store into an application that wants only DCB. If the audit types grow, they are
/// the obvious candidates for a shared abstractions package.
/// </remarks>
public interface IAuditableEntity
{
    /// <summary>
    /// Gets or sets the date the entity was created.
    /// </summary>
    DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the user that created the entity.
    /// </summary>
    string? CreatedBy { get; set; }
}
