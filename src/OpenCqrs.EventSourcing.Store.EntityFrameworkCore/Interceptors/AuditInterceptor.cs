using Memoria.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Interceptors;

public class AuditInterceptor(TimeProvider timeProvider, IHttpContextAccessor httpContextAccessor)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

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
                    case EntityState.Modified:
                        context.Entry(auditableEntity).Property(entity => entity.CreatedDate).IsModified = false;
                        context.Entry(auditableEntity).Property(entity => entity.CreatedBy).IsModified = false;
                        break;
                }
            }

            if (changedEntity.Entity is IEditableEntity editableEntity)
            {
                switch (changedEntity.State)
                {
                    case EntityState.Added:
                    case EntityState.Modified:
                        editableEntity.UpdatedDate = utcNow;
                        editableEntity.UpdatedBy = currentUserNameIdentifier;
                        break;
                }
            }

            if (changedEntity.Entity is IApplicableEntity applicableEntity)
            {
                switch (changedEntity.State)
                {
                    case EntityState.Added:
                        applicableEntity.AppliedDate = utcNow;
                        break;
                }
            }
        }
    }
}
