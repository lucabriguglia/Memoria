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
/// The two reads one stored streamed model is shown through, against a Cosmos store.
/// </summary>
/// <remarks>
/// The relational counterpart of <see cref="SqliteStreamedModelTests"/>, proving the same two
/// questions against the engine rather than against a fake — because neither is obvious in Cosmos
/// SQL: the set of types a model applies is an <c>ARRAY_CONTAINS</c> over a parameter, the
/// properties its identifier claims are <c>CONTAINS</c> over the payload as text, and the model
/// itself is the one read here that names its partition.
/// <para>
/// The data is shaped to tell the narrowings apart: two orders in one customer's stream, an event
/// no order model applies beside them, and another customer carrying the same order reference.
/// </para>
/// </remarks>
[Trait("Category", "Emulator")]
[Collection(CosmosCollection.Name)]
public class CosmosStreamedModelTests : IAsyncLifetime
{
    private const string Endpoint = "https://localhost:8081";

    private const string Key =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private readonly string _databaseName = $"memoria_web_tests_{Guid.NewGuid():N}";

    private const string ContainerName = "Domain";

    private const string Stream = "customer:alice";

    private const string Placed = "OrderPlaced:1";

    private const string Paid = "OrderPaid:1";

    private const string Noted = "NoteAdded:1";

    /// <summary>What the order these tests are about was addressed by, as the store wrote it.</summary>
    private const string StoreId = "ORD-1:1";

    /// <summary>
    /// The projection's own key, which has to differ from the aggregate's: Cosmos keeps an id unique
    /// within a partition, so two models of one stream rendering the same id are one document. See
    /// <see cref="CosmosStreamedSnapshotsTests"/>, which records that as the store's own limitation.
    /// </summary>
    private const string ProjectionStoreId = "SUM-1:1";

    private CosmosClient _client = null!;
    private CosmosStreamedReads _reads = null!;

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

        await database.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(ContainerName, "/streamId"));

        var container = database.Database.GetContainer(ContainerName);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var appended = new[]
        {
            (Sequence: 0, Type: Placed, Order: "ORD-1"),
            (Sequence: 1, Type: Paid, Order: "ORD-1"),
            (Sequence: 2, Type: Placed, Order: "ORD-2"),
            (Sequence: 3, Type: Noted, Order: "ORD-1")
        };

        foreach (var (sequence, type, order) in appended)
        {
            await container.UpsertItemAsync(new EventDocument
            {
                Id = $"{Stream}:{sequence}",
                StreamId = Stream,
                EventType = type,
                Sequence = sequence,
                Data = $"{{\"OrderId\":\"{order}\",\"Reference\":\"R-{sequence}\"}}",
                CreatedDate = start.AddHours(sequence)
            }, new PartitionKey(Stream));
        }

        await container.UpsertItemAsync(new EventDocument
        {
            Id = "customer:bob:0",
            StreamId = "customer:bob",
            EventType = Placed,
            Sequence = 0,
            Data = "{\"OrderId\":\"ORD-1\",\"Reference\":\"R-9\"}",
            CreatedDate = start
        }, new PartitionKey("customer:bob"));

        await container.UpsertItemAsync(new AggregateDocument
        {
            Id = StoreId,
            StreamId = Stream,
            AggregateType = "Order:1",
            Version = 2,
            LatestEventSequence = 1,
            Data = "{\"OrderId\":\"ORD-1\",\"Paid\":42}",
            CreatedDate = start,
            CreatedBy = "importer",
            UpdatedDate = start.AddHours(1),
            UpdatedBy = "refresher"
        }, new PartitionKey(Stream));

        await container.UpsertItemAsync(new ProjectionDocument
        {
            Id = ProjectionStoreId,
            StreamId = Stream,
            ProjectionType = "OrderSummary:1",
            Version = 2,
            LatestEventSequence = 1,
            Data = "{\"OrderId\":\"ORD-1\",\"Lines\":1}",
            CreatedDate = start,
            UpdatedDate = start
        }, new PartitionKey(Stream));

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

    private static StreamedEventFilter EventsOf(
        IReadOnlyList<string>? types = null,
        IReadOnlyDictionary<string, string>? properties = null) =>
        new(StreamPattern: Stream, EventType: null, Text: null, Descending: false, Page: 1, Size: 20)
        {
            EventTypes = types,
            Properties = properties
        };

    [Fact]
    public async Task GivenAStream_WhenTheTypesAModelAppliesAreAsked_ThenOnlyThoseComeBack()
    {
        var page = await _reads.Events(EventsOf([Placed, Paid]));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(3, "the stream holds two placed and one paid, and one note besides");
        page.Events.Select(appended => appended.Event.Type).Should().NotContain(Noted);
    }

    [Fact]
    public async Task GivenAStream_WhenAnIdentifiersPropertyIsAsked_ThenOnlyItsOwnEventsComeBack()
    {
        var page = await _reads.Events(
            EventsOf([Placed, Paid], new Dictionary<string, string> { ["OrderId"] = "ORD-1" }));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(2, "the other order's placing is in the same stream and is not this one's");
        page.Events.Select(appended => appended.Event.Position).Should().Equal(0, 1);
    }

    [Fact]
    public async Task GivenAnotherStream_WhenItHoldsTheSameProperty_ThenItStaysOut()
    {
        var page = await _reads.Events(
            EventsOf(properties: new Dictionary<string, string> { ["OrderId"] = "ORD-1" }));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(3, "three of this stream's four events carry it, and bob's is not one");
        page.Events.Select(appended => appended.StreamId).Should().AllBe(Stream);
    }

    /// <summary>
    /// Payload and audit properties included, which is what the lists deliberately leave unread and
    /// the whole of what a page about one model shows.
    /// </summary>
    [Fact]
    public async Task GivenAStoredAggregate_WhenItIsReadByItsAddress_ThenTheWholeDocumentComesBack()
    {
        var read = await _reads.Model(
            new StreamedModelAddress(StreamedModelKind.Aggregate, Stream, StoreId));

        using var scope = new AssertionScope();

        read.Error.Should().BeNull();
        read.Snapshot.Should().NotBeNull();
        read.Snapshot!.Type.Should().Be("Order:1");
        read.Snapshot.Version.Should().Be(2);
        read.Snapshot.Sequence.Should().Be(1);
        read.Snapshot.Data.Should().Be("{\"OrderId\":\"ORD-1\",\"Paid\":42}");
        read.Snapshot.CreatedBy.Should().Be("importer");
        read.Snapshot.UpdatedBy.Should().Be("refresher");
    }

    /// <summary>
    /// One container holds both kinds, so only the discriminator says which of them an address
    /// reaches — and asking for a projection must never come back with an aggregate.
    /// </summary>
    [Fact]
    public async Task GivenAStoredProjection_WhenItIsReadByItsAddress_ThenItIsNotTheAggregate()
    {
        var read = await _reads.Model(
            new StreamedModelAddress(StreamedModelKind.Projection, Stream, ProjectionStoreId));

        using var scope = new AssertionScope();

        read.Error.Should().BeNull();
        read.Snapshot.Should().NotBeNull();
        read.Snapshot!.Type.Should().Be("OrderSummary:1");
        read.Snapshot.Data.Should().Contain("Lines");
    }

    [Fact]
    public async Task GivenNothingStored_WhenItIsReadByItsAddress_ThenThereIsNoRowAndNoError()
    {
        var read = await _reads.Model(
            new StreamedModelAddress(StreamedModelKind.Aggregate, Stream, "ORD-404:1"));

        using var scope = new AssertionScope();

        read.Snapshot.Should().BeNull();
        read.Error.Should().BeNull();
    }
}
