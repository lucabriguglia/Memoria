using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Interceptors;

/// <summary>
/// Stamps <see cref="IAuditableEntity.CreatedDate"/> and <see cref="IAuditableEntity.CreatedBy"/> on
/// insert.
/// </summary>
/// <remarks>
/// A DCB event is append-only — nothing edits one — so unlike the streamed store's interceptor there
/// is no editable-entity branch and no need to protect creation stamps from later modification.
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
            if (changedEntity is { Entity: IAuditableEntity auditableEntity, State: EntityState.Added })
            {
                auditableEntity.CreatedDate = utcNow;
                auditableEntity.CreatedBy = currentUserNameIdentifier;
            }
        }
    }
}
