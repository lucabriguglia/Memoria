using Memoria.Results;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;

public static partial class IDomainDbContextExtensions
{
    /// <summary>
    /// Saves all pending changes in the domain database context to the underlying data store.
    /// </summary>
    /// <param name="domainDbContext">The domain database context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating the success or failure of the save operation.</returns>
    /// <example>
    /// <code>
    /// var result = await context.Save();
    /// if (!result.IsSuccess)
    /// {
    ///     return result.Failure;
    /// }
    /// // Save successful
    /// </code>
    /// </example>
    public static async Task<Result> Save(this IDomainDbContext domainDbContext, CancellationToken cancellationToken = default)
    {
        try
        {
            await domainDbContext.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            ex.AddException(operation: "Save");
            return ErrorHandling.DefaultFailure;
        }
    }
}
