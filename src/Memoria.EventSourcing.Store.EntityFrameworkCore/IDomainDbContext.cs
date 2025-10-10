using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore;

/// <summary>
/// Defines the contract for the domain database context in the Entity Framework Core event store.
/// This interface provides access to all the essential entity sets and operations required for
/// event sourcing functionality, including aggregate snapshots, domain events, and their relationships.
/// </summary>
/// <example>
/// <code>
/// // Example implementation of IDomainDbContext
/// public class EventStoreDbContext : DbContext, IDomainDbContext
/// {
///     public EventStoreDbContext(DbContextOptions&lt;EventStoreDbContext&gt; options) : base(options)
///     {
///     }
///     
///     public DbSet&lt;AggregateEntity&gt; Aggregates { get; set; } = null!;
///     public DbSet&lt;EventEntity&gt; Events { get; set; } = null!;
///     public DbSet&lt;AggregateEventEntity&gt; AggregateEvents { get; set; } = null!;
///     
///     public void DetachAggregate&lt;T&gt;(IAggregateId aggregateId, T aggregate) 
///         where T : IAggregate
///     {
///         // Find and detach any tracked entities for this aggregate
///         var trackedEntries = ChangeTracker.Entries()
///             .Where(e =&gt; (e.Entity is AggregateEntity ae && ae.Id == aggregateId.Id) ||
///                        (e.Entity is IAggregate a && a.AggregateId == aggregateId.Id))
///             .ToList();
///         
///         foreach (var entry in trackedEntries)
///         {
///             entry.State = EntityState.Detached;
///         }
///         
///         // Clear aggregate uncommitted events
///         aggregate.ClearUncommittedEvents();
///     }
///     
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         base.OnModelCreating(modelBuilder);
///         
///         // Configure entity relationships and constraints
///         modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventStoreDbContext).Assembly);
///     }
/// }
/// 
/// // Usage in repository or service layer
/// public class OrderAggregateRepository
/// {
///     private readonly IDomainDbContext _context;
///     
///     public OrderAggregateRepository(IDomainDbContext context)
///     {
///         _context = context;
///     }
///     
///     public async Task&lt;OrderAggregate?&gt; GetByIdAsync(Guid orderId)
///     {
///         var aggregateId = new OrderAggregateId(orderId);
///         var streamId = new OrderStreamId(orderId);
///         
///         var result = await _context.GetAggregate(streamId, aggregateId);
///         return result.IsSuccess ? result.Value : null;
///     }
///     
///     public async Task SaveAsync(OrderAggregate aggregate)
///     {
///         var aggregateId = new OrderAggregateId(aggregate.Id);
///         var streamId = new OrderStreamId(aggregate.Id);
///         
///         var result = await _context.Save(streamId, aggregateId, aggregate, aggregate.Version - 1);
///         if (!result.IsSuccess)
///         {
///             throw new InvalidOperationException(result.Failure?.Description);
///         }
///     }
/// }
/// 
/// // Service registration in DI container
/// public void ConfigureServices(IServiceCollection services)
/// {
///     services.AddDbContext&lt;EventStoreDbContext&gt;(options =&gt;
///     {
///         options.UseSqlServer(connectionString);
///         options.EnableSensitiveDataLogging(isDevelopment);
///         options.EnableServiceProviderCaching();
///     });
///     
///     services.AddScoped&lt;IDomainDbContext&gt;(provider =&gt; 
///         provider.GetRequiredService&lt;EventStoreDbContext&gt;());
/// }
/// 
/// // Advanced querying example
/// public class EventAnalyticsService
/// {
///     private readonly IDomainDbContext _context;
///     
///     public EventAnalyticsService(IDomainDbContext context)
///     {
///         _context = context;
///     }
///     
///     public async Task&lt;Dictionary&lt;string, int&gt;&gt; GetEventCountsByTypeAsync(DateTime since)
///     {
///         var sinceOffset = new DateTimeOffset(since, TimeSpan.Zero);
///         
///         return await _context.Events
///             .Where(e =&gt; e.CreatedDate &gt;= sinceOffset)
///             .GroupBy(e =&gt; e.TypeName)
///             .Select(g =&gt; new { EventType = g.Key, Count = g.Count() })
///             .ToDictionaryAsync(x =&gt; x.EventType, x =&gt; x.Count);
///     }
///     
///     public async Task&lt;List&lt;string&gt;&gt; GetMostActiveStreamsAsync(int topCount = 10)
///     {
///         return await _context.Events
///             .GroupBy(e =&gt; e.StreamId)
///             .OrderByDescending(g =&gt; g.Count())
///             .Take(topCount)
///             .Select(g =&gt; g.Key)
///             .ToListAsync();
///     }
/// }
/// </code>
/// </example>
public interface IDomainDbContext : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets or sets the DbSet for aggregate entities, providing access to aggregate snapshot storage.
    /// This entity set contains serialized aggregate states along with their metadata and audit information,
    /// enabling efficient aggregate persistence and retrieval operations in the event sourcing system.
    /// </summary>
    /// <value>
    /// A <see cref="DbSet{TEntity}"/> of <see cref="AggregateEntity"/> objects representing
    /// the aggregate snapshots stored in the database. Each entity contains the complete
    /// serialized state of a domain aggregate along with versioning and audit metadata.
    /// </value>
    /// <example>
    /// <code>
    /// // Basic aggregate retrieval
    /// public async Task&lt;AggregateEntity?&gt; GetAggregateAsync(string aggregateId)
    /// {
    ///     return await _context.Aggregates
    ///         .AsNoTracking()
    ///         .FirstOrDefaultAsync(a =&gt; a.Id == aggregateId);
    /// }
    /// 
    /// // Query aggregates with filtering
    /// public async Task&lt;List&lt;AggregateEntity&gt;&gt; GetRecentAggregatesAsync(string typeName, TimeSpan age)
    /// {
    ///     var cutoffDate = DateTimeOffset.UtcNow.Subtract(age);
    ///     
    ///     return await _context.Aggregates
    ///         .Where(a =&gt; a.TypeName == typeName && a.UpdatedDate &gt;= cutoffDate)
    ///         .OrderByDescending(a =&gt; a.UpdatedDate)
    ///         .ToListAsync();
    /// }
    /// 
    /// // Update aggregate snapshot
    /// public async Task UpdateAggregateSnapshotAsync(string aggregateId, IAggregate aggregate)
    /// {
    ///     var entity = await _context.Aggregates.FindAsync(aggregateId);
    ///     if (entity != null)
    ///     {
    ///         entity.Data = JsonConvert.SerializeObject(aggregate);
    ///         entity.Version = aggregate.Version;
    ///         entity.LatestEventSequence = aggregate.LatestEventSequence;
    ///         
    ///         _context.Aggregates.Update(entity);
    ///         await _context.SaveChangesAsync();
    ///     }
    /// }
    /// 
    /// // Aggregate analytics and reporting
    /// public async Task&lt;Dictionary&lt;string, int&gt;&gt; GetAggregateCountsByTypeAsync()
    /// {
    ///     return await _context.Aggregates
    ///         .GroupBy(a =&gt; a.TypeName)
    ///         .Select(g =&gt; new { Type = g.Key, Count = g.Count() })
    ///         .ToDictionaryAsync(x =&gt; x.Type, x =&gt; x.Count);
    /// }
    /// 
    /// // Bulk operations for performance
    /// public async Task CreateMultipleSnapshotsAsync(List&lt;AggregateEntity&gt; aggregates)
    /// {
    ///     _context.Aggregates.AddRange(aggregates);
    ///     await _context.SaveChangesAsync();
    /// }
    /// </code>
    /// </example>
    DbSet<AggregateEntity> Aggregates { get; set; }

    /// <summary>
    /// Gets or sets the DbSet for event entities, providing access to the complete event store.
    /// This entity set contains all domain events in their serialized form, maintaining the
    /// immutable, append-only event log that serves as the source of truth for the event sourcing system.
    /// </summary>
    /// <value>
    /// A <see cref="DbSet{TEntity}"/> of <see cref="EventEntity"/> objects representing
    /// the complete collection of domain events stored in the database. Each entity contains
    /// the serialized event data, metadata, and audit information necessary for event sourcing operations.
    /// </value>
    /// <example>
    /// <code>
    /// // Append new events to a stream
    /// public async Task AppendEventsAsync(string streamId, IEnumerable&lt;IDomainEvent&gt; events)
    /// {
    ///     var lastSequence = await _context.Events
    ///         .Where(e =&gt; e.StreamId == streamId)
    ///         .MaxAsync(e =&gt; (int?)e.Sequence) ?? 0;
    ///     
    ///     var eventEntities = events.Select((evt, index) =&gt; 
    ///         evt.ToEventEntity(new StreamId(streamId), lastSequence + index + 1)).ToList();
    ///     
    ///     _context.Events.AddRange(eventEntities);
    ///     await _context.SaveChangesAsync();
    /// }
    /// 
    /// // Load events for aggregate reconstruction
    /// public async Task&lt;List&lt;IDomainEvent&gt;&gt; LoadStreamEventsAsync(string streamId, int fromSequence = 0)
    /// {
    ///     var eventEntities = await _context.Events
    ///         .AsNoTracking()
    ///         .Where(e =&gt; e.StreamId == streamId && e.Sequence &gt; fromSequence)
    ///         .OrderBy(e =&gt; e.Sequence)
    ///         .ToListAsync();
    ///     
    ///     return eventEntities.Select(e =&gt; e.ToDomainEvent()).ToList();
    /// }
    /// 
    /// // Query events by type for projection building
    /// public async Task&lt;List&lt;EventEntity&gt;&gt; GetEventsByTypeAsync(string typeName, int version)
    /// {
    ///     return await _context.Events
    ///         .AsNoTracking()
    ///         .Where(e =&gt; e.TypeName == typeName && e.TypeVersion == version)
    ///         .OrderBy(e =&gt; e.CreatedDate)
    ///         .ToListAsync();
    /// }
    /// 
    /// // Event streaming for real-time processing
    /// public async IAsyncEnumerable&lt;EventEntity&gt; StreamEventsFromAsync(
    ///     string streamId, 
    ///     int fromSequence,
    ///     [EnumeratorCancellation] CancellationToken cancellationToken = default)
    /// {
    ///     var events = _context.Events
    ///         .AsNoTracking()
    ///         .Where(e =&gt; e.StreamId == streamId && e.Sequence &gt;= fromSequence)
    ///         .OrderBy(e =&gt; e.Sequence)
    ///         .AsAsyncEnumerable();
    ///     
    ///     await foreach (var eventEntity in events.WithCancellation(cancellationToken))
    ///     {
    ///         yield return eventEntity;
    ///     }
    /// }
    /// 
    /// // Event analytics and monitoring
    /// public async Task&lt;EventStatistics&gt; GetEventStatisticsAsync(TimeSpan period)
    /// {
    ///     var since = DateTimeOffset.UtcNow.Subtract(period);
    ///     
    ///     var stats = await _context.Events
    ///         .Where(e =&gt; e.CreatedDate &gt;= since)
    ///         .GroupBy(e =&gt; e.TypeName)
    ///         .Select(g =&gt; new { Type = g.Key, Count = g.Count() })
    ///         .ToDictionaryAsync(x =&gt; x.Type, x =&gt; x.Count);
    ///     
    ///     return new EventStatistics 
    ///     { 
    ///         TotalEvents = stats.Values.Sum(), 
    ///         EventsByType = stats 
    ///     };
    /// }
    /// 
    /// // Batch processing for high-volume scenarios
    /// public async Task ProcessEventBatchAsync(int batchSize, Func&lt;List&lt;EventEntity&gt;, Task&gt; processor)
    /// {
    ///     var offset = 0;
    ///     List&lt;EventEntity&gt; batch;
    ///     
    ///     do
    ///     {
    ///         batch = await _context.Events
    ///             .AsNoTracking()
    ///             .OrderBy(e =&gt; e.CreatedDate)
    ///             .Skip(offset)
    ///             .Take(batchSize)
    ///             .ToListAsync();
    ///         
    ///         if (batch.Count &gt; 0)
    ///         {
    ///             await processor(batch);
    ///             offset += batch.Count;
    ///         }
    ///     }
    ///     while (batch.Count == batchSize);
    /// }
    /// </code>
    /// </example>
    DbSet<EventEntity> Events { get; set; }

    /// <summary>
    /// Gets or sets the DbSet for aggregate-event relationship entities, providing explicit many-to-many
    /// associations between aggregates and their related events. This junction table enables efficient
    /// querying and maintains referential integrity in complex event sourcing scenarios.
    /// </summary>
    /// <value>
    /// A <see cref="DbSet{TEntity}"/> of <see cref="AggregateEventEntity"/> objects representing
    /// the explicit relationships between aggregates and events. Each entity links a specific
    /// aggregate to a specific event, enabling advanced querying and relationship management.
    /// </value>
    /// <example>
    /// <code>
    /// // Load all events for a specific aggregate
    /// public async Task&lt;List&lt;EventEntity&gt;&gt; GetAggregateEventsAsync(string aggregateId)
    /// {
    ///     return await _context.AggregateEvents
    ///         .AsNoTracking()
    ///         .Where(ae =&gt; ae.AggregateId == aggregateId)
    ///         .Include(ae =&gt; ae.Event)
    ///         .Select(ae =&gt; ae.Event)
    ///         .OrderBy(e =&gt; e.Sequence)
    ///         .ToListAsync();
    /// }
    /// 
    /// // Find aggregates affected by specific events
    /// public async Task&lt;List&lt;string&gt;&gt; GetAffectedAggregatesAsync(List&lt;string&gt; eventIds)
    /// {
    ///     return await _context.AggregateEvents
    ///         .AsNoTracking()
    ///         .Where(ae =&gt; eventIds.Contains(ae.EventId))
    ///         .Select(ae =&gt; ae.AggregateId)
    ///         .Distinct()
    ///         .ToListAsync();
    /// }
    /// 
    /// // Bulk load events for multiple aggregates
    /// public async Task&lt;Dictionary&lt;string, List&lt;IDomainEvent&gt;&gt;&gt; BulkLoadAggregateEventsAsync(
    ///     List&lt;string&gt; aggregateIds)
    /// {
    ///     var aggregateEvents = await _context.AggregateEvents
    ///         .AsNoTracking()
    ///         .Where(ae =&gt; aggregateIds.Contains(ae.AggregateId))
    ///         .Include(ae =&gt; ae.Event)
    ///         .ToListAsync();
    ///     
    ///     return aggregateEvents
    ///         .GroupBy(ae =&gt; ae.AggregateId)
    ///         .ToDictionary(
    ///             g =&gt; g.Key,
    ///             g =&gt; g.Select(ae =&gt; ae.Event.ToDomainEvent())
    ///                   .OrderBy(e =&gt; ae.Event.Sequence)
    ///                   .ToList()
    ///         );
    /// }
    /// 
    /// // Create relationship between aggregate and events
    /// public async Task LinkAggregateToEventsAsync(string aggregateId, List&lt;string&gt; eventIds)
    /// {
    ///     var relationships = eventIds.Select(eventId =&gt; new AggregateEventEntity
    ///     {
    ///         AggregateId = aggregateId,
    ///         EventId = eventId
    ///     }).ToList();
    ///     
    ///     _context.AggregateEvents.AddRange(relationships);
    ///     await _context.SaveChangesAsync();
    /// }
    /// 
    /// // Advanced querying with complex filters
    /// public async Task&lt;List&lt;AggregateEventEntity&gt;&gt; GetRelationshipsAsync(
    ///     string? aggregateType = null,
    ///     string? eventType = null,
    ///     DateTime? since = null)
    /// {
    ///     var query = _context.AggregateEvents
    ///         .AsNoTracking()
    ///         .Include(ae =&gt; ae.Aggregate)
    ///         .Include(ae =&gt; ae.Event)
    ///         .AsQueryable();
    ///     
    ///     if (!string.IsNullOrEmpty(aggregateType))
    ///     {
    ///         query = query.Where(ae =&gt; ae.Aggregate.TypeName == aggregateType);
    ///     }
    ///     
    ///     if (!string.IsNullOrEmpty(eventType))
    ///     {
    ///         query = query.Where(ae =&gt; ae.Event.TypeName == eventType);
    ///     }
    ///     
    ///     if (since.HasValue)
    ///     {
    ///         var sinceOffset = new DateTimeOffset(since.Value, TimeSpan.Zero);
    ///         query = query.Where(ae =&gt; ae.Event.CreatedDate &gt;= sinceOffset);
    ///     }
    ///     
    ///     return await query.ToListAsync();
    /// }
    /// 
    /// // Analytics and reporting
    /// public async Task&lt;Dictionary&lt;string, int&gt;&gt; GetEventCountsByAggregateAsync()
    /// {
    ///     return await _context.AggregateEvents
    ///         .AsNoTracking()
    ///         .GroupBy(ae =&gt; ae.AggregateId)
    ///         .Select(g =&gt; new { AggregateId = g.Key, EventCount = g.Count() })
    ///         .ToDictionaryAsync(x =&gt; x.AggregateId, x =&gt; x.EventCount);
    /// }
    /// 
    /// // Cleanup operations for maintenance
    /// public async Task RemoveOrphanedRelationshipsAsync()
    /// {
    ///     var orphanedRelationships = await _context.AggregateEvents
    ///         .Where(ae =&gt; ae.Aggregate == null || ae.Event == null)
    ///         .ToListAsync();
    ///     
    ///     if (orphanedRelationships.Any())
    ///     {
    ///         _context.AggregateEvents.RemoveRange(orphanedRelationships);
    ///         await _context.SaveChangesAsync();
    ///     }
    /// }
    /// </code>
    /// </example>
    DbSet<AggregateEventEntity> AggregateEvents { get; set; }

    /// <summary>
    /// Asynchronously saves all changes made in the context to the database with support for cancellation.
    /// This method coordinates the persistence of all tracked entities including aggregates, events,
    /// and their relationships while maintaining data consistency and integrity constraints.
    /// </summary>
    /// <param name="cancellationToken">
    /// A <see cref="CancellationToken"/> to observe while waiting for the task to complete.
    /// This enables cooperative cancellation of long-running save operations.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous save operation. The task result contains
    /// the number of state entries written to the database during the save operation.
    /// </returns>
    /// <example>
    /// <code>
    /// // Basic save operation
    /// public async Task SaveOrderAsync(OrderAggregate order)
    /// {
    ///     var streamId = new OrderStreamId(order.Id);
    ///     var aggregateId = new OrderAggregateId(order.Id);
    ///     
    ///     // Track entities for saving
    ///     var result = await _context.Track(streamId, aggregateId, order, order.Version - 1);
    ///     if (result.IsSuccess)
    ///     {
    ///         // Persist changes to database
    ///         await _context.SaveChangesAsync();
    ///         
    ///         // Clean up tracking
    ///         _context.DetachAggregate(aggregateId, order);
    ///     }
    /// }
    /// 
    /// // Save with cancellation support
    /// public async Task SaveWithCancellationAsync(
    ///     IAggregate aggregate, 
    ///     CancellationToken cancellationToken)
    /// {
    ///     try
    ///     {
    ///         // Perform tracking operations
    ///         var trackResult = await TrackAggregateChanges(aggregate);
    ///         if (!trackResult.IsSuccess)
    ///             throw new InvalidOperationException(trackResult.Failure?.Description);
    ///         
    ///         // Save with cancellation token
    ///         var savedCount = await _context.SaveChangesAsync(cancellationToken);
    ///         _logger.LogInformation("Saved {Count} entities to database", savedCount);
    ///     }
    ///     catch (OperationCanceledException)
    ///     {
    ///         _logger.LogInformation("Save operation was cancelled");
    ///         throw;
    ///     }
    ///     catch (Exception ex)
    ///     {
    ///         _logger.LogError(ex, "Error saving aggregate {AggregateId}", aggregate.AggregateId);
    ///         throw;
    ///     }
    /// }
    /// 
    /// // Batch save operation
    /// public async Task SaveMultipleAggregatesAsync(List&lt;IAggregate&gt; aggregates)
    /// {
    ///     foreach (var aggregate in aggregates)
    ///     {
    ///         // Track each aggregate
    ///         await TrackAggregateForSave(aggregate);
    ///     }
    ///     
    ///     // Single save operation for all tracked entities
    ///     var totalSaved = await _context.SaveChangesAsync();
    ///     _logger.LogInformation("Batch saved {Count} entities", totalSaved);
    ///     
    ///     // Clean up tracking for all aggregates
    ///     foreach (var aggregate in aggregates)
    ///     {
    ///         CleanupAggregateTracking(aggregate);
    ///     }
    /// }
    /// 
    /// // Error handling patterns
    /// public async Task&lt;Result&gt; SafeSaveAsync()
    /// {
    ///     try
    ///     {
    ///         await _context.SaveChangesAsync();
    ///         return Result.Ok();
    ///     }
    ///     catch (DbUpdateConcurrencyException ex)
    ///     {
    ///         _logger.LogWarning("Concurrency conflict during save: {Message}", ex.Message);
    ///         return new Failure("Concurrency Conflict", 
    ///             "The data was modified by another user. Please refresh and try again.");
    ///     }
    ///     catch (DbUpdateException ex)
    ///     {
    ///         _logger.LogError(ex, "Database update error during save");
    ///         return new Failure("Database Error", 
    ///             "A database error occurred while saving changes.");
    ///     }
    /// }
    /// 
    /// // Transaction management
    /// public async Task SaveWithTransactionAsync(List&lt;Action&gt; operations)
    /// {
    ///     using var transaction = await _context.Database.BeginTransactionAsync();
    ///     try
    ///     {
    ///         foreach (var operation in operations)
    ///         {
    ///             operation();
    ///         }
    ///         
    ///         await _context.SaveChangesAsync();
    ///         await transaction.CommitAsync();
    ///     }
    ///     catch
    ///     {
    ///         await transaction.RollbackAsync();
    ///         throw;
    ///     }
    /// }
    /// </code>
    /// </example>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Detaches an aggregate and its associated entities from the Entity Framework change tracker,
    /// optimizing memory usage and preventing unintended modifications. This operation is essential
    /// for aggregate lifecycle management in event sourcing scenarios where aggregates should not
    /// remain in the context after their business operations are complete.
    /// </summary>
    /// <typeparam name="T">
    /// The type of aggregate to detach. Must implement <see cref="IAggregateRoot"/> to ensure
    /// proper aggregate behavior and lifecycle management.
    /// </typeparam>
    /// <param name="aggregateId">
    /// The unique identifier of the aggregate being detached. Used to locate and detach
    /// all related entities from the change tracker.
    /// </param>
    /// <param name="aggregate">
    /// The aggregate instance being detached. Used to clear uncommitted events and
    /// reset the aggregate to a clean state after persistence operations.
    /// </param>
    /// <example>
    /// <code>
    /// // Basic implementation of DetachAggregate
    /// public void DetachAggregate&lt;T&gt;(IAggregateId aggregateId, T aggregate) 
    ///     where T : IAggregate
    /// {
    ///     // Find all tracked entries related to this aggregate
    ///     var trackedEntries = ChangeTracker.Entries()
    ///         .Where(entry =&gt; IsRelatedToAggregate(entry.Entity, aggregateId))
    ///         .ToList();
    ///     
    ///     // Detach all related entities
    ///     foreach (var entry in trackedEntries)
    ///     {
    ///         entry.State = EntityState.Detached;
    ///     }
    ///     
    ///     // Clear uncommitted events from the aggregate
    ///     aggregate.ClearUncommittedEvents();
    /// }
    /// 
    /// private bool IsRelatedToAggregate(object entity, IAggregateId aggregateId)
    /// {
    ///     return entity switch
    ///     {
    ///         AggregateEntity ae =&gt; ae.Id == aggregateId.Id,
    ///         EventEntity ee =&gt; ee.StreamId == aggregateId.Id,
    ///         AggregateEventEntity aee =&gt; aee.AggregateId == aggregateId.Id,
    ///         IAggregate a =&gt; a.AggregateId == aggregateId.Id,
    ///         _ =&gt; false
    ///     };
    /// }
    /// 
    /// // Usage in repository save operation
    /// public async Task&lt;Result&gt; SaveOrderAsync(OrderAggregate order)
    /// {
    ///     try
    ///     {
    ///         var aggregateId = new OrderAggregateId(order.Id);
    ///         var streamId = new OrderStreamId(order.Id);
    ///         
    ///         // Track aggregate changes
    ///         var trackResult = await _context.Track(streamId, aggregateId, order, order.Version - 1);
    ///         if (!trackResult.IsSuccess)
    ///             return trackResult.Failure!;
    ///         
    ///         // Save to database
    ///         await _context.SaveChangesAsync();
    ///         
    ///         // Detach aggregate after successful save
    ///         _context.DetachAggregate(aggregateId, order);
    ///         
    ///         return Result.Ok();
    ///     }
    ///     catch (Exception ex)
    ///     {
    ///         // Detach even on failure to ensure clean state
    ///         _context.DetachAggregate(new OrderAggregateId(order.Id), order);
    ///         throw;
    ///     }
    /// }
    /// 
    /// // Batch detachment for multiple aggregates
    /// public void DetachAllAggregates&lt;T&gt;(List&lt;(IAggregateId Id, T Aggregate)&gt; aggregates)
    ///     where T : IAggregate
    /// {
    ///     foreach (var (aggregateId, aggregate) in aggregates)
    ///     {
    ///         DetachAggregate(aggregateId, aggregate);
    ///     }
    /// }
    /// 
    /// // Advanced detachment with logging
    /// public void DetachAggregateWithLogging&lt;T&gt;(
    ///     IAggregateId aggregateId, 
    ///     T aggregate) where T : IAggregate
    /// {
    ///     var trackedCount = ChangeTracker.Entries().Count();
    ///     _logger.LogDebug("Detaching aggregate {AggregateId}, tracking {Count} entities", 
    ///         aggregateId.Id, trackedCount);
    ///     
    ///     DetachAggregate(aggregateId, aggregate);
    ///     
    ///     var remainingCount = ChangeTracker.Entries().Count();
    ///     _logger.LogDebug("Detachment complete, {Count} entities remaining", remainingCount);
    /// }
    /// 
    /// // Context cleanup with aggregate detachment
    /// public void CleanupContext()
    /// {
    ///     // Find all tracked aggregates
    ///     var trackedAggregates = ChangeTracker.Entries()
    ///         .Where(e =&gt; e.Entity is IAggregate)
    ///         .Select(e =&gt; e.Entity as IAggregate)
    ///         .Where(a =&gt; a != null)
    ///         .ToList();
    ///     
    ///     // Detach each aggregate
    ///     foreach (var aggregate in trackedAggregates!)
    ///     {
    ///         var aggregateId = CreateAggregateId(aggregate);
    ///         DetachAggregate(aggregateId, aggregate);
    ///     }
    /// }
    /// 
    /// // Error-safe detachment operation
    /// public void SafeDetachAggregate&lt;T&gt;(
    ///     IAggregateId aggregateId, 
    ///     T aggregate) where T : IAggregate
    /// {
    ///     try
    ///     {
    ///         DetachAggregate(aggregateId, aggregate);
    ///     }
    ///     catch (Exception ex)
    ///     {
    ///         _logger.LogWarning(ex, "Error during aggregate detachment for {AggregateId}", 
    ///             aggregateId.Id);
    ///         
    ///         // Fallback: clear change tracker entirely if individual detachment fails
    ///         ChangeTracker.Clear();
    ///         aggregate.ClearUncommittedEvents();
    ///     }
    /// }
    /// </code>
    /// </example>
    void DetachAggregate<T>(IAggregateId<T> aggregateId, T aggregate) where T : IAggregateRoot;
}
