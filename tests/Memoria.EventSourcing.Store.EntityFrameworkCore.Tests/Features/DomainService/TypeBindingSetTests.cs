using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Data;
using Memoria.EventSourcing.Store.Tests.Models;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Projections;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DomainService;

/// <summary>
/// A context given its own <see cref="TypeBindingSet"/> resolves every stored key through that set
/// and never through the process-wide one, so two stores read in one process can bind the same
/// key to two different CLR types.
/// </summary>
/// <remarks>
/// Proved with the twins: types that carry the same attribute name and version as the shared test
/// models, and so write the same keys, but that only the instance set knows. A row written by the
/// shared model through a context on the process-wide set is read back as the twin through a
/// context on the instance set — and as the shared model through a context given no set at all.
/// Each test opens its own in-memory database, since the two contexts in a test must share one and
/// no other test may.
/// </remarks>
public class TypeBindingSetTests
{
    private readonly string _database = $"TypeBindingSet-{Guid.NewGuid()}";
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
        new EntityFrameworkCoreDomainService(
            new TestDbContext(Options(), _timeProvider, Shared.CreateHttpContextAccessor()));

    private IDomainService OnTwinBindings() =>
        new EntityFrameworkCoreDomainService(
            new TestDbContext(Options(), _timeProvider, Shared.CreateHttpContextAccessor())
            {
                TypeBindings = TwinBindings.Create()
            });

    private DbContextOptions<DomainDbContext> Options() =>
        new DbContextOptionsBuilder<DomainDbContext>().UseInMemoryDatabase(_database).Options;

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
            // The instance set turns the twin into the stored key; the process-wide set has never
            // heard of it, so the same filter there matches nothing rather than everything.
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

        // Saving folds the new events into the snapshot, deciding which ones the aggregate handles
        // by resolving each stored key to a type: through its own set that type is the twin event,
        // which the twin aggregate applies. Through the process-wide set it would be the shared
        // event, which it does not — and no snapshot would be written.
        await OnTwinBindings().SaveAggregate(streamId, new TwinAggregateId(id), twin, expectedEventSequence: 0);
        var snapshot = await OnTwinBindings().GetAggregate(streamId, new TwinAggregateId(id));

        snapshot.Value.Should().BeOfType<TwinAggregate>().Which.Version.Should().Be(1);
    }
}
