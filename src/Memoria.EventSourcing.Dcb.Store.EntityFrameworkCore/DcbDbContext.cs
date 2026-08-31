using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Configurations;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;

/// <summary>
/// The database context for the dynamic consistency boundary store. Derive from it, or add its two
/// entity configurations to a context of your own.
/// </summary>
/// <param name="options">The context options.</param>
/// <param name="timeProvider">Supplies the append timestamp.</param>
/// <param name="httpContextAccessor">Supplies the appending user.</param>
/// <example>
/// <code>
/// public class BoxOfficeDbContext(
///     DbContextOptions&lt;DcbDbContext&gt; options,
///     TimeProvider timeProvider,
///     IHttpContextAccessor httpContextAccessor)
///     : DcbDbContext(options, timeProvider, httpContextAccessor);
/// </code>
/// </example>
/// <remarks>
/// Nothing here is shared with <c>DomainDbContext</c>. An application using both consistency models
/// may put all five tables in one context by applying both sets of configurations, but the two
/// stores never read each other's tables.
/// </remarks>
public abstract class DcbDbContext(
    DbContextOptions<DcbDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DbContext(options), IDcbDbContext
{
    /// <summary>
    /// Gets the collation applied to the tag column, or null to leave the database default.
    /// </summary>
    /// <remarks>
    /// Tags compare ordinally in .NET, so <c>seat:A1</c> and <c>seat:a1</c> are two tags. SQL
    /// Server's default collation is case-insensitive and would make them one row, quietly widening
    /// every boundary that uses them — a correctness problem, not a tidiness one. The default here
    /// resolves a case-sensitive collation for the providers Memoria ships against; override it to
    /// pin a different one, or return null to accept the database default deliberately.
    /// </remarks>
    protected virtual string? TagCollation => Database.ProviderName switch
    {
        "Microsoft.EntityFrameworkCore.SqlServer" => "SQL_Latin1_General_CP1_CS_AS",
        "Npgsql.EntityFrameworkCore.PostgreSQL" => "C",
        // SQLite is ordinal by default for non-NOCASE columns, and the in-memory provider does not
        // model collation at all. Anything else is the consumer's to pin.
        _ => null
    };

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.AddInterceptors(new AuditInterceptor(timeProvider, httpContextAccessor));
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new DcbEventEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DcbEventTagEntityConfiguration(TagCollation));
        modelBuilder.ApplyConfiguration(new DcbTagHeadEntityConfiguration(TagCollation));
        modelBuilder.ApplyConfiguration(new DcbSnapshotEntityConfiguration());
    }

    /// <summary>
    /// Gets or sets the appended events.
    /// </summary>
    public DbSet<DcbEventEntity> DcbEvents { get; set; } = null!;

    /// <summary>
    /// Gets or sets the tags on those events.
    /// </summary>
    public DbSet<DcbEventTagEntity> DcbEventTags { get; set; } = null!;

    /// <summary>
    /// Gets or sets the per-tag rows appends contend on.
    /// </summary>
    public DbSet<DcbTagHeadEntity> DcbTagHeads { get; set; } = null!;

    /// <summary>
    /// Gets or sets the persisted folds of a boundary into an aggregate or a projection.
    /// </summary>
    public DbSet<DcbSnapshotEntity> DcbSnapshots { get; set; } = null!;
}
