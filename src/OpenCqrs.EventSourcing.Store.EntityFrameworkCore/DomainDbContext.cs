using Memoria.EventSourcing.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Configurations;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Entities;
using OpenCqrs.EventSourcing.Store.EntityFrameworkCore.Interceptors;

namespace OpenCqrs.EventSourcing.Store.EntityFrameworkCore;

/// <summary>
/// Provides the foundational database context for event sourcing operations using Entity Framework Core.
/// This abstract context integrates audit functionality, configures event sourcing entities, and provides
/// the infrastructure for aggregate and event persistence in event-driven architectures.
/// </summary>
/// <param name="options">
/// The database context options that configure the Entity Framework Core behavior, including
/// connection strings, database provider settings, and other configuration parameters.
/// </param>
/// <param name="timeProvider">
/// The time provider abstraction used for generating consistent timestamps across the application.
/// Enables testable time handling and supports different time zones and clock implementations.
/// </param>
/// <param name="httpContextAccessor">
/// The HTTP context accessor that provides access to the current HTTP request context.
/// Used by audit functionality to extract user information and request-specific data.
/// </param>
/// <example>
/// <code>
/// // Example concrete implementation
/// public class ApplicationDomainDbContext : DomainDbContext
/// {
///     public ApplicationDomainDbContext(
///         DbContextOptions&lt;DomainDbContext&gt; options,
///         TimeProvider timeProvider,
///         IHttpContextAccessor httpContextAccessor)
///         : base(options, timeProvider, httpContextAccessor)
///     {
///     }
///     
///     // Add application-specific entities
///     public DbSet&lt;CustomEntity&gt; CustomEntities { get; set; } = null!;
///     
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         base.OnModelCreating(modelBuilder);
///         
///         // Add custom configurations
///         modelBuilder.ApplyConfiguration(new CustomEntityConfiguration());
///     }
/// }
/// 
/// // Service registration
/// public void ConfigureServices(IServiceCollection services)
/// {
///     services.AddSingleton(TimeProvider.System);
///     services.AddHttpContextAccessor();
///     
///     services.AddDbContext&lt;ApplicationDomainDbContext&gt;(options =&gt;
///     {
///         options.UseSqlServer(connectionString);
///     });
///     
///     services.AddScoped&lt;IDomainDbContext&gt;(provider =&gt; 
///         provider.GetRequiredService&lt;ApplicationDomainDbContext&gt;());
/// }
/// 
/// // Usage in repositories
/// public class AggregateRepository
/// {
///     private readonly IDomainDbContext _context;
///     
///     public AggregateRepository(IDomainDbContext context)
///     {
///         _context = context;
///     }
///     
///     public async Task&lt;Result&gt; SaveAggregateAsync&lt;T&gt;(
///         T aggregate, 
///         IStreamId streamId, 
///         IAggregateId aggregateId,
///         int expectedVersion) where T : IAggregate
///     {
///         return await _context.Save(streamId, aggregateId, aggregate, expectedVersion);
///     }
/// }
/// 
/// // Example with custom audit behavior
/// public class AuditableDomainDbContext : DomainDbContext
/// {
///     private readonly ICurrentUserService _currentUserService;
///     
///     public AuditableDomainDbContext(
///         DbContextOptions&lt;DomainDbContext&gt; options,
///         TimeProvider timeProvider,
///         IHttpContextAccessor httpContextAccessor,
///         ICurrentUserService currentUserService)
///         : base(options, timeProvider, httpContextAccessor)
///     {
///         _currentUserService = currentUserService;
///     }
///     
///     protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
///     {
///         base.OnConfiguring(optionsBuilder);
///         
///         // Add custom audit interceptor
///         optionsBuilder.AddInterceptors(new CustomAuditInterceptor(_currentUserService));
///     }
/// }
/// </code>
/// </example>
public abstract class DomainDbContext(
    DbContextOptions<DomainDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DbContext(options), IDomainDbContext
{
    /// <summary>
    /// Configures the database context with event sourcing-specific interceptors and settings.
    /// This method sets up the audit functionality and can be overridden to add custom configurations.
    /// </summary>
    /// <param name="optionsBuilder">
    /// The options builder used to configure the database context. Provides methods to add interceptors,
    /// configure providers, and set various database-specific options.
    /// </param>
    /// <example>
    /// <code>
    /// // Example override in derived class
    /// protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    /// {
    ///     base.OnConfiguring(optionsBuilder);
    ///     
    ///     // Add custom interceptors
    ///     optionsBuilder.AddInterceptors(new LoggingInterceptor(_logger));
    ///     optionsBuilder.AddInterceptors(new PerformanceInterceptor(_metrics));
    ///     
    ///     // Configure retry policy for transient failures
    ///     optionsBuilder.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
    /// }
    /// </code>
    /// </example>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.AddInterceptors(new AuditInterceptor(timeProvider, httpContextAccessor));
    }

    /// <summary>
    /// Configures the database model with event sourcing entity configurations and relationships.
    /// This method applies standardized configurations for aggregates, events, and their relationships
    /// to ensure proper database schema generation and query optimization.
    /// </summary>
    /// <param name="modelBuilder">
    /// The model builder used to configure entity types, relationships, and database schema.
    /// Provides the fluent API for detailed entity configuration and constraint definition.
    /// </param>
    /// <example>
    /// <code>
    /// // Example override in derived class
    /// protected override void OnModelCreating(ModelBuilder modelBuilder)
    /// {
    ///     base.OnModelCreating(modelBuilder);
    ///     
    ///     // Add custom entity configurations
    ///     modelBuilder.ApplyConfiguration(new CustomerEntityConfiguration());
    ///     modelBuilder.ApplyConfiguration(new OrderEntityConfiguration());
    ///     
    ///     // Apply custom naming conventions
    ///     foreach (var entity in modelBuilder.Model.GetEntityTypes())
    ///     {
    ///         entity.SetTableName($"ES_{entity.GetTableName()}");
    ///     }
    ///     
    ///     // Add global query filters
    ///     modelBuilder.Entity&lt;AggregateEntity&gt;()
    ///         .HasQueryFilter(a =&gt; !a.IsDeleted);
    /// }
    /// </code>
    /// </example>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new AggregateEntityConfiguration());
        modelBuilder.ApplyConfiguration(new EventEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AggregateEventEntityConfiguration());
    }

    /// <summary>
    /// Gets or sets the database set for aggregate entities that store serialized aggregate snapshots.
    /// Provides access to aggregate persistence operations including creation, updates, and retrieval
    /// for event sourcing scenarios.
    /// </summary>
    /// <value>
    /// A <see cref="DbSet{AggregateEntity}"/> that enables CRUD operations on aggregate snapshots,
    /// supporting efficient aggregate reconstruction and state management in the event sourcing system.
    /// </value>
    public DbSet<AggregateEntity> Aggregates { get; set; } = null!;

    /// <summary>
    /// Gets or sets the database set for event entities that store individual domain events.
    /// Provides access to event persistence operations including appending, querying, and retrieval
    /// for event sourcing and event streaming scenarios.
    /// </summary>
    /// <value>
    /// A <see cref="DbSet{EventEntity}"/> that enables CRUD operations on domain events,
    /// supporting event stream management and aggregate reconstruction in the event sourcing system.
    /// </value>
    public DbSet<EventEntity> Events { get; set; } = null!;

    /// <summary>
    /// Gets or sets the database set for aggregate-event relationship entities that link aggregates to their events.
    /// Provides access to relationship management operations supporting complex querying and referential integrity
    /// between aggregates and their associated domain events.
    /// </summary>
    /// <value>
    /// A <see cref="DbSet{AggregateEventEntity}"/> that enables CRUD operations on aggregate-event relationships,
    /// facilitating efficient navigation between aggregates and events in the event sourcing system.
    /// </value>
    public DbSet<AggregateEventEntity> AggregateEvents { get; set; } = null!;

    /// <summary>
    /// Detaches the specified aggregate entity from the Entity Framework change tracker to prevent
    /// unintended state tracking and optimize memory usage in event sourcing scenarios.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the aggregate being detached. Must implement <see cref="IAggregateRoot"/> to ensure
    /// proper aggregate contract compliance and type safety.
    /// </typeparam>
    /// <param name="aggregateId">
    /// The unique identifier of the aggregate to detach. Used to locate the corresponding
    /// <see cref="AggregateEntity"/> in the change tracker for detachment operations.
    /// </param>
    /// <param name="aggregate">
    /// The aggregate instance being detached. Used to determine the aggregate type version
    /// for proper entity identification and matching.
    /// </param>
    /// <example>
    /// <code>
    /// // Usage in repository save operations
    /// public async Task&lt;Result&gt; SaveAggregateAsync&lt;T&gt;(T aggregate, IStreamId streamId, IAggregateId aggregateId) 
    ///     where T : IAggregate
    /// {
    ///     try
    ///     {
    ///         // Track and save aggregate changes
    ///         await TrackAggregateChanges(aggregate, streamId, aggregateId);
    ///         await _context.SaveChangesAsync();
    ///         
    ///         // Clean up change tracker to prevent memory leaks
    ///         _context.DetachAggregate(aggregateId, aggregate);
    ///         
    ///         return Result.Ok();
    ///     }
    ///     catch (Exception ex)
    ///     {
    ///         return Result.Fail(ex.Message);
    ///     }
    /// }
    /// 
    /// // Usage in bulk operations
    /// public async Task ProcessAggregatesAsync&lt;T&gt;(List&lt;T&gt; aggregates) where T : IAggregate
    /// {
    ///     foreach (var aggregate in aggregates)
    ///     {
    ///         // Process aggregate
    ///         await ProcessAggregate(aggregate);
    ///         
    ///         // Detach to prevent memory accumulation
    ///         var aggregateId = new AggregateId(aggregate.AggregateId);
    ///         _context.DetachAggregate(aggregateId, aggregate);
    ///     }
    /// }
    /// 
    /// // Memory optimization in long-running processes
    /// public async Task ProcessEventStreamAsync(IStreamId streamId)
    /// {
    ///     var aggregate = await _context.GetAggregateAsync&lt;OrderAggregate&gt;(streamId, aggregateId);
    ///     
    ///     // Process business logic
    ///     aggregate.ProcessBusinessRules();
    ///     
    ///     // Save changes
    ///     await _context.Save(streamId, aggregateId, aggregate, expectedVersion);
    ///     
    ///     // The Save method internally calls DetachAggregate to clean up memory
    ///     // No manual detachment needed when using context extension methods
    /// }
    /// 
    /// // Custom detachment for specific scenarios
    /// public void CleanupAggregateTracking&lt;T&gt;(T aggregate, IAggregateId aggregateId) where T : IAggregate
    /// {
    ///     // Verify aggregate is no longer needed
    ///     if (aggregate.HasUncommittedEvents)
    ///     {
    ///         throw new InvalidOperationException("Cannot detach aggregate with uncommitted events");
    ///     }
    ///     
    ///     // Detach from change tracking
    ///     _context.DetachAggregate(aggregateId, aggregate);
    ///     
    ///     // Optional: Clear aggregate references for GC
    ///     aggregate.ClearUncommittedEvents();
    /// }
    /// </code>
    /// </example>
    public void DetachAggregate<T>(IAggregateId<T> aggregateId, T aggregate) where T : IAggregateRoot
    {
        foreach (var entityEntry in ChangeTracker.Entries())
        {
            if (entityEntry.Entity is not AggregateEntity aggregateEntity)
            {
                continue;
            }

            if (aggregateEntity.Id == aggregateId.ToStoreId())
            {
                entityEntry.State = EntityState.Detached;
            }
        }
    }
}
