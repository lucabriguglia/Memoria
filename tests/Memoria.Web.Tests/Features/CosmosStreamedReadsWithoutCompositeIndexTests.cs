using Memoria.EventSourcing.Domain;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing.Store.Cosmos;
using Memoria.EventSourcing.Store.Cosmos.Documents;
using Memoria.Web.Extensibility;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The same page, read from a container carrying the indexing policy the Cosmos store actually
/// ships — the one that defines no composite indexes.
/// </summary>
/// <remarks>
/// <para>
/// The store leaves them out on purpose and says why: three were drafted and cost about 7% on every
/// write while returning nothing to the store's own reads. So the container this tool is pointed at
/// will usually not have them, and asking Cosmos for the three-key order there is not a slow query
/// but a refused one — <c>BadRequest</c>, no rows at all.
/// </para>
/// <para>
/// Rather than require every operator to change the indexing policy of a store the tool only reads,
/// the read falls back to ordering on the date alone, which the store's own policy already indexes.
/// The page then says so, because the coarser order is a real difference: events sharing a
/// timestamp can move between pages.
/// </para>
/// </remarks>
[Trait("Category", "Emulator")]
[Collection(CosmosCollection.Name)]
public class CosmosStreamedReadsWithoutCompositeIndexTests : IAsyncLifetime
{
    private const string Endpoint = "https://localhost:8081";

    private const string Key =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private readonly string _databaseName = $"memoria_web_tests_{Guid.NewGuid():N}";

    private const string ContainerName = "Domain";
    private const int EventCount = 30;

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

        // The store's own policy, unedited. Whatever it indexes is what this tool has to work with.
        await database.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(ContainerName, "/streamId")
            {
                IndexingPolicy = CosmosIndexingPolicy.CreateRecommended()
            });

        _start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var container = database.Database.GetContainer(ContainerName);

        for (var index = 0; index < EventCount; index++)
        {
            var streamId = $"c-{index % 5:0000}";

            await container.UpsertItemAsync(new EventDocument
            {
                Id = $"{streamId}:{index / 5}",
                StreamId = streamId,
                EventType = "OrderPlacedEvent:1",
                Sequence = index / 5,
                Data = "{}",
                CreatedDate = _start.AddHours(index)
            }, new PartitionKey(streamId));
        }

        // A couple of stored models too, because the snapshot pages ask for a three-key order of
        // their own and meet the same refusal.
        //
        // Their ids are prefixed rather than named after the stream: an event is written under
        // "{streamId}:{sequence}" and an aggregate under "{aggregateId}:{typeVersion}", so an
        // aggregate named after its own stream lands on the id of one of that stream's events and
        // replaces it. That is the store's own collision, covered by DocumentIdCollisionTests; it is
        // avoided here rather than reproduced, so these tests measure what they mean to.
        for (var index = 0; index < 2; index++)
        {
            var stream = $"c-000{index}";

            await container.UpsertItemAsync(new AggregateDocument
            {
                Id = $"account-c-000{index}:1",
                StreamId = stream,
                AggregateType = "CustomerAccount:1",
                Version = index + 1,
                LatestEventSequence = index,
                Data = "{}",
                CreatedDate = _start.AddHours(index),
                UpdatedDate = _start.AddHours(index + 1)
            }, new PartitionKey(stream));
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

    private static StreamedEventFilter Filter(bool descending = true) =>
        new(StreamPattern: null, EventType: null, Text: null, descending, Page: 1, Size: 10);

    [Fact]
    public async Task GivenNoCompositeIndex_WhenAPageIsRead_ThenItStillReadsTheNewestFirst()
    {
        var page = await _reads.Events(Filter());

        using var scope = new AssertionScope();

        page.Error.Should().BeNull("a container without the index is not a broken container");
        page.Total.Should().Be(EventCount);
        page.Events.Should().HaveCount(10);

        page.Events[0].Event.Written.Should().Be(_start.AddHours(EventCount - 1));
        page.Events.Select(appended => appended.Event.Written).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GivenNoCompositeIndex_WhenAPageIsRead_ThenItSaysTheOrderIsCoarser()
    {
        var page = await _reads.Events(Filter());

        page.OrderingNotice.Should().NotBeNullOrWhiteSpace(
            "a reader paging through events deserves to know rows can shift between pages");
    }

    /// <summary>
    /// The stored models fall back the same way the log does, and say so the same way.
    /// </summary>
    [Fact]
    public async Task GivenNoCompositeIndex_WhenTheStoredModelsAreRead_ThenTheyListAndSaySo()
    {
        var page = await _reads.Snapshots(new StreamedSnapshotFilter(
            StreamedModelKind.Aggregate, StreamPattern: null, ModelType: null,
            IdentifierPattern: null, Text: null, InstanceSort.Updated, Descending: true,
            Page: 1, Size: 10));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull("a container without the index is not a broken container");
        page.Total.Should().Be(2);
        page.Snapshots.Should().HaveCount(2);
        page.OrderingNotice.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GivenNoCompositeIndex_WhenAscendingIsAsked_ThenTheOldestStillComeFirst()
    {
        var page = await _reads.Events(Filter(descending: false));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Events[0].Event.Written.Should().Be(_start);
        page.Events.Select(appended => appended.Event.Written).Should().BeInAscendingOrder();
    }
}
