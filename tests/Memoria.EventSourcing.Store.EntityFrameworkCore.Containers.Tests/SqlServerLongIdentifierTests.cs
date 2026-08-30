using FluentAssertions;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;
using Memoria.EventSourcing.Store.Tests.Models.Aggregates;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// SQL Server used to reject a save whose aggregate store id and event id together exceeded 900
/// bytes, because <c>PK_DomainAggregateEvents</c> spanned both. That table is gone, and with it the
/// limit: ids that stay inside their own column widths now save whatever their combined length.
/// </summary>
/// <remarks>
/// These are the exact ids that produced
/// <c>... exceeds the maximum length of 900 bytes ... PK_DomainAggregateEvents</c>, kept so the
/// removed limitation cannot quietly return.
/// </remarks>
[Trait("Category", "Container")]
[Collection(SqlServerCollection.Name)]
public class SqlServerLongIdentifierTests(SqlServerFixture fixture)
{
    [RequiresDockerFact]
    public async Task GivenIdsThatOnceExceededTheCompositeKeyLimit_ThenTheAggregateSaves()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);
        TestTypeBindings.Configure();

        await using var dbContext = StoreSchema.OnSqlServer(fixture.ConnectionStringForFreshDatabase());

        try
        {
            await dbContext.Database.EnsureCreatedAsync();

            var streamId = new TestStreamId(new string('s', 240));
            var aggregateId = new TestAggregate1Id(new string('a', 230));
            var aggregate = new TestAggregate1(Guid.NewGuid().ToString(), "Name", "Description");

            // Each id is inside its own column width; only their sum was ever the problem.
            streamId.Id.Length.Should().BeLessThanOrEqualTo(255, "StreamId is nvarchar(255)");
            aggregateId.ToStoreId().Length.Should().BeLessThanOrEqualTo(255, "AggregateId is nvarchar(255)");
            (aggregateId.ToStoreId().Length + $"{streamId.Id}:1".Length).Should().BeGreaterThan(450,
                "these are the ids that used to break the 900-byte key");

            var domainService = new EntityFrameworkCoreDomainService(dbContext);

            var result = await domainService.SaveAggregate(streamId, aggregateId, aggregate,
                expectedEventSequence: 0);

            result.IsSuccess.Should().BeTrue(
                result.Failure is null ? "the save should succeed" : result.Failure.Description);
        }
        finally
        {
            try
            {
                await dbContext.Database.EnsureDeletedAsync();
            }
            catch
            {
                // The container is discarded after the run; a failed cleanup must not mask the result.
            }
        }
    }
}
