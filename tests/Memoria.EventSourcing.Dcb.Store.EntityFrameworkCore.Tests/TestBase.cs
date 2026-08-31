using System.Security.Claims;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Models;
using Memoria.EventSourcing.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests;

/// <summary>
/// A DCB context over the in-memory provider, seeded directly.
/// </summary>
/// <remarks>
/// Slice 3 covers reads only, so rows are seeded through the context rather than appended through
/// the store — there is nothing to append with yet. The concurrency behaviour of the append path is
/// covered against a relational provider in the next slice, because the in-memory provider models
/// neither transactions nor concurrency tokens.
/// </remarks>
public abstract class TestBase : IDisposable
{
    protected readonly TestDbContext Context;
    protected readonly FakeTimeProvider TimeProvider;

    private readonly Dictionary<string, Type> _originalEventTypeBindings = TypeBindings.EventTypeBindings;

    protected TestBase()
    {
        // Well before any date a test seeds: FakeTimeProvider refuses to move backwards, and Seed
        // moves it forward to the date being seeded.
        TimeProvider = new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Context = new TestDbContext(CreateOptions(), TimeProvider, CreateHttpContextAccessor());

        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "SeatReserved:1", typeof(SeatReservedEvent) },
            { "SeatReleased:1", typeof(SeatReleasedEvent) },
            { "CourseRenamed:1", typeof(CourseRenamedEvent) }
        };
    }

    private static DbContextOptions<DcbDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<DcbDbContext>()
            // A fresh database per test: these tests assert on absolute positions.
            .UseInMemoryDatabase($"Dcb-{Guid.NewGuid()}")
            .Options;

    private static IHttpContextAccessor CreateHttpContextAccessor()
    {
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "TestUser")], "TestAuth"))
        };

        httpContextAccessor.HttpContext.Returns(context);
        return httpContextAccessor;
    }

    /// <summary>
    /// Seeds one event at an explicit position, under the given tags.
    /// </summary>
    protected async Task Seed(long position, IEvent @event, DateTimeOffset createdDate, params string[] tags)
    {
        // The audit interceptor owns CreatedDate, so setting it on the entity would be overwritten.
        // Moving the clock is both the honest way to seed a date and a check that the interceptor
        // really is the thing stamping it.
        TimeProvider.SetUtcNow(createdDate);

        Context.DcbEvents.Add(new DcbEventEntity
        {
            Position = position,
            EventType = TypeBindings.GetEventBindingKey(@event.GetType()),
            Data = DomainSerializer.Current.Serialize(@event),
            Tags = tags.Select(tag => new DcbEventTagEntity { Position = position, Tag = tag }).ToList()
        });

        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    /// <summary>
    /// Seeds one event at an explicit position with the default timestamp.
    /// </summary>
    protected Task Seed(long position, IEvent @event, params string[] tags) =>
        Seed(position, @event, TimeProvider.GetUtcNow(), tags);

    public void Dispose()
    {
        // Restored because a test may deliberately clear the bindings, and they are process-wide.
        TypeBindings.EventTypeBindings = _originalEventTypeBindings;
        Context.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class TestDbContext(
    DbContextOptions<DcbDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DcbDbContext(options, timeProvider, httpContextAccessor);
