using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.Cosmos.Configuration;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Memoria.EventSourcing.Store.Tests.Models.Events;
using Memoria.EventSourcing.Store.Tests.Models.Streams;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.DataStore;

/// <summary>
/// Fetching specific event documents by identifier.
/// </summary>
/// <remarks>
/// Ids this store writes are <c>{streamId}:{sequence}</c>, so the query matches on the numeric
/// sequence instead of the string id — measurably cheaper on the same result set. An id that does
/// not carry a sequence has to keep working, so these cover both routes.
/// </remarks>
[Trait("Category", "Emulator")]
public class GetEventDocumentsByIdTests : TestBase
{
    [Fact]
    public async Task GivenEventsWrittenByTheStore_WhenFetchedById_ThenTheyComeBackInSequenceOrder()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());
        await DomainService.SaveEvents(streamId,
        [
            new SomethingHappenedEvent("one"),
            new SomethingHappenedEvent("two"),
            new SomethingHappenedEvent("three")
        ], expectedEventSequence: 0);

        // Deliberately out of order, and skipping the middle one.
        var result = await DataStore.GetEventDocuments(streamId,
            [$"{streamId.Id}:3", $"{streamId.Id}:1"]);

        using (new AssertionScope())
        {
            result.Value!.Count.Should().Be(2);
            result.Value[0].Sequence.Should().Be(1);
            result.Value[1].Sequence.Should().Be(3);
        }
    }

    [Fact]
    public async Task GivenNoIds_WhenFetched_ThenNothingComesBack()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());
        await DomainService.SaveEvents(streamId, [new SomethingHappenedEvent("one")], expectedEventSequence: 0);

        var result = await DataStore.GetEventDocuments(streamId, Array.Empty<string>());

        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue();
            result.Value!.Count.Should().Be(0);
        }
    }

    [Fact]
    public async Task GivenAnEventWhoseIdCarriesNoSequence_WhenFetchedById_ThenItIsStillFound()
    {
        var streamId = new TestStreamId(Guid.NewGuid().ToString());
        await DomainService.SaveEvents(streamId, [new SomethingHappenedEvent("written by the store")],
            expectedEventSequence: 0);

        // Written straight into the container, as another tool might, with an id this store would
        // never produce. Matching on sequence cannot find it, so the query has to fall back to
        // matching the id.
        const string foreignId = "externally-written-event";
        await Container().UpsertItemAsync(new EventDocument
        {
            Id = foreignId,
            StreamId = streamId.Id,
            Sequence = 2,
            EventType = TypeBindings.GetEventBindingKey(typeof(SomethingHappenedEvent)),
            Data = DomainSerializer.Current.Serialize(new SomethingHappenedEvent("written by hand")),
            CreatedDate = DateTimeOffset.UtcNow
        }, new PartitionKey(streamId.Id));

        var result = await DataStore.GetEventDocuments(streamId, [$"{streamId.Id}:1", foreignId]);

        using (new AssertionScope())
        {
            result.Value!.Count.Should().Be(2);
            result.Value.Select(document => document.Id).Should().Contain(foreignId);
        }
    }

    private static Container Container()
    {
        var options = Substitute.For<IOptions<CosmosOptions>>();
        options.Value.Returns(new CosmosOptions
        {
            Endpoint = "https://localhost:8081",
            AuthKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
        });

        return new CosmosClientProvider(options).Container;
    }
}
