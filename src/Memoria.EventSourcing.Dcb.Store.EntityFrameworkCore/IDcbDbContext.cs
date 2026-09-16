using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;

/// <summary>
/// The database context surface the DCB store's extension methods work against.
/// </summary>
public interface IDcbDbContext
{
    /// <summary>
    /// Gets the appended events.
    /// </summary>
    DbSet<DcbEventEntity> DcbEvents { get; }

    /// <summary>
    /// Gets the tags on those events.
    /// </summary>
    DbSet<DcbEventTagEntity> DcbEventTags { get; }

    /// <summary>
    /// Gets the per-tag rows appends contend on.
    /// </summary>
    DbSet<DcbTagHeadEntity> DcbTagHeads { get; }

    /// <summary>
    /// Gets the persisted folds of a boundary into an aggregate or a projection.
    /// </summary>
    DbSet<DcbSnapshotEntity> DcbSnapshots { get; }

    /// <summary>
    /// Gets the type bindings this context resolves stored keys through: which CLR type an event or
    /// snapshot key deserialises into, and which key a CLR type in an event filter stands for.
    /// </summary>
    /// <remarks>
    /// The process-wide <see cref="TypeBindingSet.Default"/> unless the context was given a set of
    /// its own, which is what a host reading more than one bounded context's store in one process
    /// does. A default so that an implementation written before this member existed still compiles
    /// and behaves as it did.
    /// </remarks>
    TypeBindingSet TypeBindings => TypeBindingSet.Default;

    /// <summary>
    /// Gets the change tracker.
    /// </summary>
    ChangeTracker ChangeTracker { get; }

    /// <summary>
    /// Gets the database facade, used for transactions.
    /// </summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// Saves pending changes.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of rows written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
