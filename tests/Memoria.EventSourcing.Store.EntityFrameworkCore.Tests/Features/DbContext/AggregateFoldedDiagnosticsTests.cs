using System.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Tests.Features.DbContext;

/// <summary>
/// <see cref="IDomainDbContextExtensions.TrackEventEntities{T}"/> folds events written for one
/// aggregate into a second aggregate sharing the stream, and writes its snapshot. It has no caller
/// inside the store — consumers call it directly for the multiple-aggregates-per-stream case — so it
/// needs its own coverage that the fold is recorded like every other snapshot write.
/// </summary>
public class AggregateFoldedDiagnosticsTests : TestBase
{
    [Fact]
    public async Task GivenEventsAreFoldedIntoASecondAggregate_ThenThatFoldIsRecordedOnTheCurrentActivity()
    {
        var id = Guid.NewGuid().ToString();
        var streamId = new TestStreamId(id);
        var testAggregate1Key = new TestAggregate1Id(id);
        var testAggregate2Key = new TestAggregate2Id(id);
        var testAggregate1 = new TestAggregate1(id, "Test Name", "Test Description");

        await using var dbContext = Shared.CreateTestDbContext();

        var trackResult = await dbContext.TrackAggregate(streamId, testAggregate1Key, testAggregate1,
            expectedEventSequence: 0);
        await dbContext.TrackEventEntities(streamId, testAggregate2Key, trackResult.Value.EventEntities!,
            expectedEventSequence: 0);
        await dbContext.Save();

        // The TrackAggregate above folded into aggregate 1; this asserts on the fold into aggregate 2.
        var folds = Activity.Current!.Events
            .Where(activityEvent => activityEvent.Name == AggregateDiagnostics.AggregateFoldedEventName)
            .ToList();

        using (new AssertionScope())
        {
            folds.Should().HaveCount(2);

            var tags = folds[^1].Tags.ToDictionary(tag => tag.Key, tag => tag.Value);
            tags["aggregateId"].Should().Be(testAggregate2Key.ToStoreId());
            tags["appliedFromSequence"].Should().Be(1);
            tags["appliedToSequence"].Should().Be(1);
            tags["appliedCount"].Should().Be(1);
            tags["versionBefore"].Should().Be(0);
            tags["versionAfter"].Should().Be(1);
        }
    }
}
