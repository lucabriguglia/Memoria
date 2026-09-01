using FluentAssertions;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Entities;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions.DbContextExtensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Relational.Tests.Models;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests;

/// <summary>
/// A context that stores event and snapshot payloads as <c>jsonb</c>, which is what the PostgreSQL
/// guide tells a consumer to write.
/// </summary>
/// <remarks>
/// There is no <c>Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Npgsql</c> package. The
/// streamed store's Npgsql sibling exists solely to replace the event-property filter, whose
/// substring match breaks against <c>jsonb</c>; the DCB store has no property filter, because tags
/// do that job. What is left is this override, which belongs in the consumer's own context.
/// </remarks>
public class JsonbDcbDbContext(
    DbContextOptions<DcbDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DcbDbContext(options, timeProvider, httpContextAccessor)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DcbEventEntity>().Property(@event => @event.Data).HasColumnType("jsonb");
        modelBuilder.Entity<DcbSnapshotEntity>().Property(snapshot => snapshot.Data).HasColumnType("jsonb");
    }
}

/// <summary>
/// Proves the documented <c>jsonb</c> override works, rather than only recommending it.
/// </summary>
[Trait("Category", "Container")]
[Collection(PostgreSqlCollection.Name)]
public class PostgreSqlDcbJsonbTests(PostgreSqlFixture fixture)
{
    private static readonly Tag SeatA1 = new("seat", "a1");

    private JsonbDcbDbContext Connect(string connectionString) =>
        new(new DbContextOptionsBuilder<DcbDbContext>().UseNpgsql(connectionString).Options,
            TimeProvider.System, new StubHttpContextAccessor());

    [RequiresDockerFact]
    public async Task EventsAndSnapshotsRoundTripThroughJsonbColumns()
    {
        Assert.True(fixture.IsAvailable, fixture.UnavailableReason);

        TypeBindings.EventTypeBindings = new Dictionary<string, Type>
        {
            { "SeatReserved:1", typeof(SeatReservedEvent) }
        };
        DcbTypeBindings.AggregateTypeBindings = new Dictionary<string, Type>
        {
            { "Seat:1", typeof(SeatAggregate) }
        };

        await using var dbContext = Connect(fixture.ConnectionStringForFreshDatabase());

        try
        {
            await dbContext.Database.EnsureCreatedAsync();

            var columns = await ColumnMetadata.ReadAsync(dbContext, "DcbEvents");
            columns["Data"].DataType.Should().Be("jsonb", "the override is what is under test");

            var boundary = TagQuery.AnyOf(SeatA1);

            var appendResult = await dbContext.SaveEvents(
                [new TaggedEvent(new SeatReservedEvent("a1", "s7"), [SeatA1])],
                AppendCondition.NothingAppendedFor(boundary));

            appendResult.IsSuccess.Should().BeTrue(
                appendResult.Failure is null ? "the append should succeed" : appendResult.Failure.Description);

            // jsonb normalises what it stores — key order is not preserved and whitespace is
            // rewritten — so this proves the payload still deserialises, not that the bytes match.
            var events = await dbContext.GetEvents(boundary);
            events.Should().ContainSingle()
                .Which.Should().BeOfType<SeatReservedEvent>()
                .Which.StudentId.Should().Be("s7");

            var aggregate = await dbContext.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOrCreate);
            aggregate.Value!.ReservedBy.Should().Be("s7");

            var fromSnapshot = await dbContext.GetAggregate(new SeatId("a1"), ReadMode.SnapshotOnly);
            fromSnapshot.Value!.ReservedBy.Should().Be("s7", "the snapshot round-tripped through jsonb too");
        }
        finally
        {
            try
            {
                await dbContext.Database.EnsureDeletedAsync();
            }
            catch
            {
                // The container is discarded after the run; cleanup must not mask the result.
            }
        }
    }
}
