using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Interceptors;

/// <summary>
/// Stamps <see cref="IAuditableEntity"/> on insert and <see cref="IEditableEntity"/> on every write,
/// and keeps an edit from overwriting the creation stamps it did not set.
/// </summary>
/// <remarks>
/// A DCB event is append-only — nothing edits one — but a snapshot is not: it is rewritten in place
/// every time its boundary moves. The rewrite hands Entity Framework Core a freshly built row rather
/// than one loaded from the database, so every column counts as modified, and without the guard below
/// an edit would write the creation stamps back as their default and lose when the state was first
/// folded.
/// </remarks>
public class AuditInterceptor(TimeProvider timeProvider, IHttpContextAccessor httpContextAccessor)
    : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var utcNow = timeProvider.GetUtcNow();
        var currentUserNameIdentifier = httpContextAccessor.GetCurrentUserNameIdentifier();

        foreach (var changedEntity in context.ChangeTracker.Entries())
        {
            if (changedEntity.Entity is IAuditableEntity auditableEntity)
            {
                switch (changedEntity.State)
                {
                    case EntityState.Added:
                        auditableEntity.CreatedDate = utcNow;
                        auditableEntity.CreatedBy = currentUserNameIdentifier;
                        break;

                    // The row already carries these from whichever write inserted it, and the
                    // instance being saved does not. Excluding them from the UPDATE is what keeps
                    // the stored values rather than replacing them with this instance's defaults.
                    case EntityState.Modified:
                        changedEntity.Property(nameof(IAuditableEntity.CreatedDate)).IsModified = false;
                        changedEntity.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
                        break;
                }
            }

            if (changedEntity.Entity is IEditableEntity editableEntity &&
                changedEntity.State is EntityState.Added or EntityState.Modified)
            {
                editableEntity.UpdatedDate = utcNow;
                editableEntity.UpdatedBy = currentUserNameIdentifier;
            }
        }
    }
}
