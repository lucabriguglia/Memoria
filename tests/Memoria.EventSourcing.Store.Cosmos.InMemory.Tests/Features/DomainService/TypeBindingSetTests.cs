using System.Security.Claims;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Tests.Models;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Projections;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.InMemory.Tests.Features.DomainService;

/// <summary>
/// An in-memory Cosmos domain service given its own <see cref="TypeBindingSet"/> resolves every
/// stored key through that set and never through the process-wide one — proved with the twins,
/// the way the Entity Framework Core stores prove it.
/// </summary>
public class TypeBindingSetTests
{
    private readonly InMemoryCosmosStorage _storage = new();
    private readonly FakeTimeProvider _timeProvider = new();

    // Set and never restored, like every sibling class: a map captured at construction may be
    // the empty one a class built first would see, and putting it back while another class reads
    // the shared set in parallel takes that class's keys away.
    public TypeBindingSetTests()
    {
        // The process-wide set knows the shared models and nothing of the twins — the same maps
        // every other test class in this process sets, to the entry, because the classes run in
        // parallel over one static set and a smaller map here would be a missing key there.
        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "TestAggregateCreated:1", typeof(TestAggregateCreatedEvent) },
            { "TestAggregateUpdated:1", typeof(TestAggregateUpdatedEvent) },
            { "SomethingHappened:1", typeof(SomethingHappenedEvent) },
            { "SomethingHappened:2", typeof(SomethingHappenedEvent2) }
        };
        TypeBindings.AggregateTypeBindings = new Dictionary<string, Type>
        {
            { "TestAggregate1:1", typeof(TestAggregate1) },
            { "TestAggregate2:1", typeof(TestAggregate2) },
            { "TestAggregateWithNoTypeFilter:1", typeof(TestAggregateWithNoTypeFilter) }
        };
        TypeBindings.ProjectionTypeBindings = new Dictionary<string, Type>
        {
            { "TestProjection:1", typeof(TestProjection) }
        };
    }

    private IDomainService OnProcessWideBindings() =>
        new InMemoryCosmosDomainService(_storage, _timeProvider, HttpContextAccessor());

    private IDomainService OnTwinBindings() =>
        new InMemoryCosmosDomainService(_storage, _timeProvider, HttpContextAccessor(), TwinBindings.Create());

    private static IHttpContextAccessor HttpContextAccessor()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "TestUser")], "TestAuth"))
        });
        return accessor;
    }

    [Fact]
    public async Task Reads_an_event_as_the_type_its_own_set_binds_the_key_to()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregate = new TestAggregate1(id, "Name", "Description");
        aggregate.Update("Renamed", "Described");
        await OnProcessWideBindings().SaveAggregate(streamId, new TestAggregate1Id(id), aggregate, expectedEventSequence: 0);

        var events = await OnTwinBindings().GetEvents(streamId);

        using (new AssertionScope())
        {
            events.Value.Should().HaveCount(2);
            events.Value![0].Should().BeOfType<TwinCreatedEvent>().Which.Name.Should().Be("Name");
            events.Value![1].Should().BeOfType<TwinUpdatedEvent>().Which.Name.Should().Be("Renamed");
        }
    }

    [Fact]
    public async Task Reads_an_event_through_the_process_wide_set_when_given_none()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        await OnProcessWideBindings().SaveAggregate(
            streamId, new TestAggregate1Id(id), new TestAggregate1(id, "Name", "Description"), expectedEventSequence: 0);

        var events = await OnProcessWideBindings().GetEvents(streamId);

        events.Value.Should().ContainSingle().Which.Should().BeOfType<TestAggregateCreatedEvent>();
    }

    [Fact]
    public async Task Reads_an_aggregate_snapshot_as_the_type_its_own_set_binds_the_key_to()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        await OnProcessWideBindings().SaveAggregate(
            streamId, new TestAggregate1Id(id), new TestAggregate1(id, "Name", "Description"), expectedEventSequence: 0);

        var aggregate = await OnTwinBindings().GetAggregate(streamId, new TwinAggregateId(id));

        aggregate.Value.Should().BeOfType<TwinAggregate>().Which.Name.Should().Be("Name");
    }

    [Fact]
    public async Task Reads_a_projection_snapshot_as_the_type_its_own_set_binds_the_key_to()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        await OnProcessWideBindings().SaveProjection(
            streamId, new TestProjectionId(id), new TestProjection { Name = "Name", Description = "Description" });

        var projection = await OnTwinBindings().GetProjection(streamId, new TwinProjectionId(id));

        projection.Value.Should().BeOfType<TwinProjection>().Which.Name.Should().Be("Name");
    }

    [Fact]
    public async Task Filters_events_by_the_types_its_own_set_knows()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var aggregate = new TestAggregate1(id, "Name", "Description");
        aggregate.Update("Renamed", "Described");
        await OnProcessWideBindings().SaveAggregate(streamId, new TestAggregate1Id(id), aggregate, expectedEventSequence: 0);

        var throughTwins = await OnTwinBindings().GetEvents(streamId, [typeof(TwinCreatedEvent)]);
        var throughProcess = await OnProcessWideBindings().GetEvents(streamId, [typeof(TwinCreatedEvent)]);

        using (new AssertionScope())
        {
            throughTwins.Value.Should().ContainSingle().Which.Should().BeOfType<TwinCreatedEvent>();
            throughProcess.Value.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Folds_a_snapshot_from_the_events_its_own_set_knows()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var twin = new TwinAggregate(id, "Name", "Description");

        await OnTwinBindings().SaveAggregate(streamId, new TwinAggregateId(id), twin, expectedEventSequence: 0);
        var snapshot = await OnTwinBindings().GetAggregate(streamId, new TwinAggregateId(id));

        snapshot.Value.Should().BeOfType<TwinAggregate>().Which.Version.Should().Be(1);
    }
}
