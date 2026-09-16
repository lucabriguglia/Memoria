using Memoria.EventSourcing.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Memoria.Web.Extensibility;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// One page of the log, read from the container the Cosmos store writes into.
/// </summary>
/// <remarks>
/// Against the emulator rather than a fake, because what is being proved is what Cosmos does: a
/// cross-partition query, an <c>ORDER BY</c> that needs a composite index to be served at all, and
/// <c>OFFSET</c>/<c>LIMIT</c> paging. A stand-in that answered from a dictionary would pass without
/// any of that being true. Needs the emulator on https://localhost:8081 — see CLAUDE.md.
/// <para>
/// The documents are the store's own <see cref="EventDocument"/>, written with the same SDK, so the
/// JSON under test is the JSON the store produces rather than a second description of it.
/// </para>
/// </remarks>
[Trait("Category", "Emulator")]
[Collection(CosmosCollection.Name)]
public class CosmosStreamedReadsTests : IAsyncLifetime
{
    private const string Endpoint = "https://localhost:8081";

    private const string Key =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    /// <summary>A database of its own per run, so a failed run cannot leave rows for the next.</summary>
    private readonly string _databaseName = $"memoria_web_tests_{Guid.NewGuid():N}";

    private const string ContainerName = "Domain";

    /// <summary>Sixty events over ten streams, an hour apart, oldest first.</summary>
    private const int EventCount = 60;

    private CosmosClient _client = null!;
    private CosmosStreamedReads _reads = null!;
    private DateTimeOffset _start;

    public async Task InitializeAsync()
    {
        _client = new CosmosClient(Endpoint, Key, new CosmosClientOptions
        {
            HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            }),
            ConnectionMode = ConnectionMode.Gateway
        });

        var database = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);

        // The store's own partition key, plus the composite index the page's three-key order needs.
        // Cosmos refuses such an ORDER BY outright without one, so the container a real deployment
        // reads from needs these too — see scripts/install for where they ship.
        var container = new ContainerProperties(ContainerName, "/streamId");

        container.IndexingPolicy.CompositeIndexes.Add(
        [
            new CompositePath { Path = "/createdDate", Order = CompositePathSortOrder.Descending },
            new CompositePath { Path = "/streamId", Order = CompositePathSortOrder.Ascending },
            new CompositePath { Path = "/sequence", Order = CompositePathSortOrder.Descending }
        ]);

        container.IndexingPolicy.CompositeIndexes.Add(
        [
            new CompositePath { Path = "/createdDate", Order = CompositePathSortOrder.Ascending },
            new CompositePath { Path = "/streamId", Order = CompositePathSortOrder.Ascending },
            new CompositePath { Path = "/sequence", Order = CompositePathSortOrder.Ascending }
        ]);

        await database.Database.CreateContainerIfNotExistsAsync(container);

        _start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var written = database.Database.GetContainer(ContainerName);

        for (var index = 0; index < EventCount; index++)
        {
            var streamId = $"c-{index % 10:0000}";

            await written.UpsertItemAsync(new EventDocument
            {
                Id = $"{streamId}:{index / 10}",
                StreamId = streamId,
                EventType = index % 2 == 0 ? "OrderPlacedEvent:1" : "OrderShippedEvent:1",
                Sequence = index / 10,
                Data = $"{{\"orderReference\":\"ORD-{index}\"}}",
                CreatedDate = _start.AddHours(index),
                CreatedBy = "seeder"
            }, new PartitionKey(streamId));
        }

        _reads = new CosmosStreamedReads(_client, _databaseName, ContainerName, TypeBindingSet.Default);
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _client.GetDatabase(_databaseName).DeleteAsync();
        }
        catch (CosmosException)
        {
            // A run that never created it has nothing to clean up.
        }

        _client.Dispose();
    }

    private static StreamedEventFilter Filter(int page = 1, bool descending = true, int size = 25) =>
        new(StreamPattern: null, EventType: null, Text: null, descending, page, size);

    [Fact]
    public async Task GivenAContainerOfEvents_WhenTheFirstPageIsRead_ThenItIsTheNewestInOrder()
    {
        var page = await _reads.Events(Filter());

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(EventCount, "every event in the container is counted, not just this page");
        page.TotalPages.Should().Be(3);
        page.Page.Should().Be(1);
        page.Events.Should().HaveCount(25);

        page.Events.Select(appended => appended.Event.Written)
            .Should().BeInDescendingOrder("the newest come first");

        page.Events[0].Event.Written.Should().Be(_start.AddHours(EventCount - 1),
            "the newest event in the container leads the first page");

        page.OrderingNotice.Should().BeNull(
            "this container carries the composite index, so the full order was served");
    }

    [Fact]
    public async Task GivenThreePagesOfEvents_WhenTheLastIsRead_ThenItHoldsWhatIsLeft()
    {
        var page = await _reads.Events(Filter(page: 3));

        page.Error.Should().BeNull();
        page.Page.Should().Be(3);
        page.Events.Should().HaveCount(10, "sixty events at twenty-five to a page leave ten on the third");

        page.Events[^1].Event.Written.Should().Be(_start,
            "the oldest event in the container ends the last page");
    }

    [Fact]
    public async Task GivenAscendingOrderIsAsked_WhenTheFirstPageIsRead_ThenTheOldestComeFirst()
    {
        var page = await _reads.Events(Filter(descending: false));

        page.Error.Should().BeNull();
        page.Events.Should().HaveCount(25);
        page.Events[0].Event.Written.Should().Be(_start);

        page.Events.Select(appended => appended.Event.Written).Should().BeInAscendingOrder();
    }

    /// <summary>
    /// A row carries the stream it belongs to and the key it was written under, which is what the
    /// page renders beside the payload.
    /// </summary>
    [Fact]
    public async Task GivenAnEvent_WhenItIsRead_ThenItCarriesItsStreamAndKey()
    {
        var page = await _reads.Events(Filter());

        var newest = page.Events[0];

        using var scope = new AssertionScope();

        newest.StreamId.Should().Be("c-0009", "event fifty-nine was written to the tenth stream");
        newest.Id.Should().Be("c-0009:5");
        newest.Event.Type.Should().Be("OrderShippedEvent:1");
        newest.Event.WrittenBy.Should().Be("seeder", "a row opened over the table says who appended it");
    }

    /// <summary>
    /// The row at one place in a stream is read the way a page of them is, who appended it
    /// included.
    /// </summary>
    [Fact]
    public async Task GivenAPlace_WhenTheEventThereIsRead_ThenItSaysWhoAppendedIt()
    {
        var placed = await _reads.At(Filter(descending: false) with { StreamPattern = "c-0003" }, index: 1);

        using var scope = new AssertionScope();

        placed.Error.Should().BeNull();
        placed.Event!.Id.Should().Be("c-0003:1");
        placed.Event.Event.WrittenBy.Should().Be("seeder");
    }

    /// <summary>
    /// The one read that names its partition: an address carries the stream, which is what the
    /// container is partitioned by, so this reaches one document without crossing partitions.
    /// </summary>
    [Fact]
    public async Task GivenAStoredEvent_WhenItIsReadByItsKey_ThenTheWholeRowComesBack()
    {
        var read = await _reads.Event(new StreamedEventAddress("c-0003", "c-0003:2"));

        using var scope = new AssertionScope();

        read.Error.Should().BeNull();
        read.Event.Should().NotBeNull();
        read.Event!.StreamId.Should().Be("c-0003");
        read.Event.Id.Should().Be("c-0003:2");
        read.Event.Event.Position.Should().Be(2);
        read.Event.Event.Type.Should().Be("OrderShippedEvent:1", "event twenty-three is odd-numbered, so it was shipped");
        read.Event.Event.Data.Should().Be("{\"orderReference\":\"ORD-23\"}");
        read.Event.Event.Written.Should().Be(_start.AddHours(23));
        read.Event.Event.WrittenBy.Should().Be("seeder", "the page about one event says who appended it");
    }

    [Fact]
    public async Task GivenNoEventUnderAKey_WhenItIsRead_ThenThereIsNoRowAndNoError()
    {
        var read = await _reads.Event(new StreamedEventAddress("c-0003", "c-0003:40"));

        using var scope = new AssertionScope();

        read.Error.Should().BeNull();
        read.Event.Should().BeNull();
    }

    /// <summary>
    /// The detail page's version column, narrowed: the model's whole history read once, as the
    /// sequences alone in order, so each row on the page is placed by where it falls.
    /// </summary>
    [Fact]
    public async Task GivenAFilter_WhenTheHistoryIsRead_ThenEveryMatchingSequenceComesBackInOrder()
    {
        var history = await _reads.History(
            Filter() with { StreamPattern = "c-0002", EventTypes = ["OrderPlacedEvent:1"] });

        using var scope = new AssertionScope();

        history.Error.Should().BeNull();
        // Every event of stream two is even-numbered, so every one of them was placed.
        history.Positions.Should().Equal(0L, 1L, 2L, 3L, 4L, 5L);
    }

    [Fact]
    public async Task GivenTheTypesAModelApplies_WhenTheHistoryIsRead_ThenOnlyThoseAreInIt()
    {
        var history = await _reads.History(
            Filter() with { StreamPattern = "c-0002", EventTypes = ["OrderShippedEvent:1"] });

        history.Positions.Should().BeEmpty("no event of stream two was shipped");
    }
}
