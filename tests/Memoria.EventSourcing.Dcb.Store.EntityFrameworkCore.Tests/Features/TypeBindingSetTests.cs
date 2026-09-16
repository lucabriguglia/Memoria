using System.Security.Claims;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Models;
using Memoria.EventSourcing.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Tests.Features;

/// <summary>
/// A DCB context given its own <see cref="TypeBindingSet"/> resolves every stored key through that
/// set and never through the process-wide one — the same contract the streamed store keeps, proved
/// the same way: twins that write the shared models' keys but that only the instance set knows.
/// </summary>
/// <remarks>
/// Rows are seeded through the context, as every test in this project seeds them, since the
/// in-memory provider models no transactions for the append path. Each test opens its own database
/// so the two contexts in it share one and no other test does.
/// </remarks>
public class TypeBindingSetTests
{
    private readonly string _database = $"Dcb-TypeBindingSet-{Guid.NewGuid()}";
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));

    // Set and never restored: a map captured at construction may be the empty one a class built
    // first would see, and putting it back while another class reads the shared set in parallel
    // takes that class's keys away.
    public TypeBindingSetTests()
    {
        // The process-wide set knows the shared models and nothing of the twins — the same event
        // map every other test class in this process sets, to the entry, because the classes run
        // in parallel over one static set and a smaller map here would be a missing key there.
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "SeatReserved:1", typeof(SeatReservedEvent) },
            { "SeatReleased:1", typeof(SeatReleasedEvent) },
            { "CourseRenamed:1", typeof(CourseRenamedEvent) }
        };
        DcbTypeBindings.AggregateTypeBindings = new Dictionary<string, Type>
        {
            { "Seat:1", typeof(SeatAggregate) }
        };
        DcbTypeBindings.ProjectionTypeBindings = new Dictionary<string, Type>
        {
            { "SeatSummary:1", typeof(SeatSummaryProjection) }
        };
    }

    private static TypeBindingSet TwinBindings() => new()
    {
        EventTypeBindings = new Dictionary<string, Type>
        {
            { "SeatReserved:1", typeof(TwinReservedEvent) },
            { "SeatReleased:1", typeof(TwinReleasedEvent) }
        },
        DcbAggregateTypeBindings = new Dictionary<string, Type>
        {
            { "Seat:1", typeof(TwinSeatAggregate) }
        },
        DcbProjectionTypeBindings = new Dictionary<string, Type>
        {
            { "SeatSummary:1", typeof(TwinSummaryProjection) }
        }
    };

    private TestDbContext OnProcessWideBindings() =>
        new(Options(), _timeProvider, HttpContextAccessor());

    private TestDbContext OnTwinBindings() =>
        new(Options(), _timeProvider, HttpContextAccessor()) { TypeBindings = TwinBindings() };

    private DbContextOptions<DcbDbContext> Options() =>
        new DbContextOptionsBuilder<DcbDbContext>().UseInMemoryDatabase(_database).Options;

    private static IHttpContextAccessor HttpContextAccessor()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "TestUser")], "TestAuth"))
        });
        return accessor;
    }

    private async Task Seed(IEvent @event, params string[] tags)
    {
        await using var context = OnProcessWideBindings();
        var position = await context.DcbEvents.CountAsync() + 1;

        context.DcbEvents.Add(new DcbEventEntity
        {
            Position = position,
            EventType = TypeBindings.GetEventBindingKey(@event.GetType()),
            Data = DomainSerializer.Current.Serialize(@event),
            Tags = tags.Select(tag => new DcbEventTagEntity { Position = position, Tag = tag }).ToList()
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Reads_an_event_as_the_type_its_own_set_binds_the_key_to()
    {
        await Seed(new SeatReservedEvent("A1", "s-1"), "seat:A1");
        await Seed(new SeatReleasedEvent("A1"), "seat:A1");

        var events = await new EntityFrameworkCoreDcbDomainService(OnTwinBindings())
            .GetEvents(TagQuery.AnyOf(new Tag("seat", "A1")));

        using (new AssertionScope())
        {
            events.Value.Should().HaveCount(2);
            events.Value![0].Should().BeOfType<TwinReservedEvent>().Which.StudentId.Should().Be("s-1");
            events.Value![1].Should().BeOfType<TwinReleasedEvent>();
        }
    }

    [Fact]
    public async Task Reads_an_event_through_the_process_wide_set_when_given_none()
    {
        await Seed(new SeatReservedEvent("A1", "s-1"), "seat:A1");

        var events = await new EntityFrameworkCoreDcbDomainService(OnProcessWideBindings())
            .GetEvents(TagQuery.AnyOf(new Tag("seat", "A1")));

        events.Value.Should().ContainSingle().Which.Should().BeOfType<SeatReservedEvent>();
    }

    [Fact]
    public async Task Reads_an_aggregate_snapshot_as_the_type_its_own_set_binds_the_key_to()
    {
        var seat = new SeatAggregate();
        seat.Apply([new SeatReservedEvent("A1", "s-1")]);
        await using (var writer = OnProcessWideBindings())
        {
            writer.DcbSnapshots.Add(seat.ToSnapshotEntity(new SeatId("A1")));
            await writer.SaveChangesAsync();
        }

        var aggregate = await new EntityFrameworkCoreDcbDomainService(OnTwinBindings())
            .GetAggregate(new TwinSeatId("A1"));

        aggregate.Value.Should().BeOfType<TwinSeatAggregate>().Which.ReservedBy.Should().Be("s-1");
    }

    [Fact]
    public async Task Reads_a_projection_snapshot_as_the_type_its_own_set_binds_the_key_to()
    {
        var summary = new SeatSummaryProjection();
        summary.Apply([new SeatReservedEvent("A1", "s-1")]);
        await using (var writer = OnProcessWideBindings())
        {
            writer.DcbSnapshots.Add(summary.ToSnapshotEntity(new SeatSummaryId("A1")));
            await writer.SaveChangesAsync();
        }

        var projection = await new EntityFrameworkCoreDcbDomainService(OnTwinBindings())
            .GetProjection(new TwinSummaryId("A1"));

        projection.Value.Should().BeOfType<TwinSummaryProjection>().Which.Reservations.Should().Be(1);
    }

    [Fact]
    public async Task Filters_events_by_the_types_its_own_set_knows()
    {
        await Seed(new SeatReservedEvent("A1", "s-1"), "seat:A1");
        await Seed(new SeatReleasedEvent("A1"), "seat:A1");
        var boundary = TagQuery.AnyOf(new Tag("seat", "A1"));

        var throughTwins = await new EntityFrameworkCoreDcbDomainService(OnTwinBindings())
            .GetEvents(boundary, [typeof(TwinReservedEvent)]);
        var throughProcess = await new EntityFrameworkCoreDcbDomainService(OnProcessWideBindings())
            .GetEvents(boundary, [typeof(TwinReservedEvent)]);

        using (new AssertionScope())
        {
            throughTwins.Value.Should().ContainSingle().Which.Should().BeOfType<TwinReservedEvent>();
            throughProcess.Value.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Folds_an_in_memory_aggregate_from_the_events_its_own_set_knows()
    {
        await Seed(new SeatReservedEvent("A1", "s-1"), "seat:A1");

        // The fold narrows the boundary to the event types the aggregate applies, turning each into
        // its key through the set — so the twin aggregate, which applies only twin events, folds one
        // through its own set and nothing through the process-wide one.
        var throughTwins = await new EntityFrameworkCoreDcbDomainService(OnTwinBindings())
            .GetInMemoryAggregate(new TwinSeatId("A1"));

        throughTwins.Value.Should().BeOfType<TwinSeatAggregate>().Which.Version.Should().Be(1);
    }
}

// The twins: the shared models' attribute names and versions on different CLR types.

[EventType("SeatReserved")]
public record TwinReservedEvent(string SeatId, string StudentId) : IEvent;

[EventType("SeatReleased")]
public record TwinReleasedEvent(string SeatId) : IEvent;

[AggregateType("Seat")]
public class TwinSeatAggregate : DcbAggregateRoot
{
    public string? ReservedBy { get; private set; }

    public override Type[]? EventTypeFilter { get; } = [typeof(TwinReservedEvent), typeof(TwinReleasedEvent)];

    protected override bool Apply<T>(T @event)
    {
        switch (@event)
        {
            case TwinReservedEvent reserved:
                ReservedBy = reserved.StudentId;
                return true;
            case TwinReleasedEvent:
                ReservedBy = null;
                return true;
            default:
                return false;
        }
    }
}

public class TwinSeatId(string seatId) : IDcbAggregateId<TwinSeatAggregate>
{
    public string Id { get; } = seatId;

    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("seat", seatId));
}

[ProjectionType("SeatSummary")]
public class TwinSummaryProjection : DcbProjection
{
    public int Reservations { get; private set; }

    public override Type[]? EventTypeFilter { get; } = [typeof(TwinReservedEvent)];

    protected override bool Apply<T>(T @event)
    {
        if (@event is not TwinReservedEvent)
        {
            return false;
        }

        Reservations++;
        return true;
    }
}

public class TwinSummaryId(string seatId) : IDcbProjectionId<TwinSummaryProjection>
{
    public string Id { get; } = seatId;

    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("seat", seatId));
}
