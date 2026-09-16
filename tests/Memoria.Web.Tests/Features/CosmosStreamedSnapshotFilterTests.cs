using Memoria.EventSourcing.Domain;
using System;
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
/// Narrowing the stored models to the one a reader came for, against a Cosmos store.
/// </summary>
/// <remarks>
/// The four the two pages offer. Two are the log's own — a stream pattern and typed text — and two
/// are not: the model type lives in a different property for each kind, and the identifier pattern
/// is held against the whole stored key rather than the id inside it.
/// <para>
/// One thing here differs from the log deliberately, and the relational read says why: the typed
/// text is not matched against the payload. Neither page shows a stored model's state, so a row
/// matching on something invisible would come back with nothing on it carrying what was typed. The
/// payloads below carry a word found nowhere else, so a read that looked in them would be caught.
/// </para>
/// </remarks>
[Trait("Category", "Emulator")]
[Collection(CosmosCollection.Name)]
public class CosmosStreamedSnapshotFilterTests : IAsyncLifetime
{
    private const string Endpoint = "https://localhost:8081";

    private const string Key =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private readonly string _databaseName = $"memoria_web_tests_{Guid.NewGuid():N}";

    private const string ContainerName = "Domain";
    private const string Account = "CustomerAccount:1";
    private const string Stock = "WarehouseStock:1";

    /// <summary>A word carried only by the payloads, which nothing should match on.</summary>
    private const string OnlyInPayloads = "unsearchable";

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
        var properties = new ContainerProperties(ContainerName, "/streamId");

        properties.IndexingPolicy.CompositeIndexes.Add(
        [
            new CompositePath { Path = "/updatedDate", Order = CompositePathSortOrder.Descending },
            new CompositePath { Path = "/streamId", Order = CompositePathSortOrder.Ascending },
            new CompositePath { Path = "/id", Order = CompositePathSortOrder.Ascending }
        ]);

        await database.Database.CreateContainerIfNotExistsAsync(properties);

        var container = database.Database.GetContainer(ContainerName);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var hour = 0;

        // Three aggregates over two kinds of stream and two model types, so each narrowing has
        // something to exclude. The ids are prefixed rather than named after their stream, because
        // an aggregate id that renders what an event's does replaces that event.
        (string Stream, string Id, string Type)[] aggregates =
        [
            ("customer:c-0001", "account-c-0001:1", Account),
            ("customer:c-0002", "account-c-0002:1", Account),
            ("warehouse:w-0001", "stock-w-0001:1", Stock)
        ];

        foreach (var (stream, id, type) in aggregates)
        {
            await container.UpsertItemAsync(new AggregateDocument
            {
                Id = id,
                StreamId = stream,
                AggregateType = type,
                Version = 1,
                LatestEventSequence = 1,
                Data = $"{{\"note\":\"{OnlyInPayloads}\"}}",
                CreatedDate = start.AddHours(hour),
                UpdatedDate = start.AddHours(hour++)
            }, new PartitionKey(stream));
        }

        // One projection, so a narrowing that matched everything would still have to keep the kinds
        // apart to answer these.
        await container.UpsertItemAsync(new ProjectionDocument
        {
            Id = "history-c-0001:1",
            StreamId = "customer:c-0001",
            ProjectionType = "CustomerOrderHistory:1",
            Version = 1,
            LatestEventSequence = 1,
            Data = $"{{\"note\":\"{OnlyInPayloads}\"}}",
            CreatedDate = start,
            UpdatedDate = start
        }, new PartitionKey("customer:c-0001"));

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

    private static StreamedSnapshotFilter Filter(
        string? streamPattern = null,
        string? modelType = null,
        string? identifierPattern = null,
        string? text = null,
        StreamedModelKind kind = StreamedModelKind.Aggregate) =>
        new(kind, streamPattern, modelType, identifierPattern, text,
            InstanceSort.Updated, Descending: true, Page: 1, Size: 50);

    private async Task<(string[] Ids, int Total, string? Error)> Read(StreamedSnapshotFilter filter)
    {
        var page = await _reads.Snapshots(filter);

        return (page.Snapshots.Select(snapshot => snapshot.StoreId).Order().ToArray(),
            page.Total, page.Error);
    }

    [Fact]
    public async Task GivenAStreamPattern_WhenThePageIsRead_ThenOnlyModelsOfThoseStreamsComeBack()
    {
        var (ids, total, error) = await Read(Filter(streamPattern: "customer:%"));

        using var scope = new AssertionScope();

        error.Should().BeNull();
        ids.Should().Equal("account-c-0001:1", "account-c-0002:1");
        total.Should().Be(2, "the warehouse aggregate is outside the pattern");
    }

    [Fact]
    public async Task GivenAModelType_WhenThePageIsRead_ThenOnlyThatTypeComesBack()
    {
        var (ids, total, error) = await Read(Filter(modelType: Stock));

        using var scope = new AssertionScope();

        error.Should().BeNull();
        ids.Should().Equal("stock-w-0001:1");
        total.Should().Be(1);
    }

    /// <summary>
    /// The store joins the id and the type's version with a colon, so the pattern is held against
    /// that whole key. An identifier whose id ends in something fixed would otherwise match nothing.
    /// </summary>
    [Fact]
    public async Task GivenAnIdentifierPattern_WhenThePageIsRead_ThenItMatchesTheWholeStoredKey()
    {
        var (ids, total, error) = await Read(Filter(identifierPattern: "account-c-0001"));

        using var scope = new AssertionScope();

        error.Should().BeNull();
        ids.Should().Equal("account-c-0001:1");
        total.Should().Be(1);
    }

    /// <summary>
    /// The version the store appends is matched by the pattern's own trailing colon, so an
    /// identifier that is a prefix of another one does not drag it in.
    /// </summary>
    [Fact]
    public async Task GivenAnIdentifierPatternThatIsAPrefix_WhenThePageIsRead_ThenItMatchesNothing()
    {
        var (_, total, error) = await Read(Filter(identifierPattern: "account-c-000"));

        using var scope = new AssertionScope();

        error.Should().BeNull();
        total.Should().Be(0, "the key carries a whole id before its colon, not a prefix of one");
    }

    [Fact]
    public async Task GivenTypedTextNamingAStream_WhenThePageIsRead_ThenThatStreamsModelsComeBack()
    {
        var (ids, total, _) = await Read(Filter(text: "warehouse"));

        using var scope = new AssertionScope();

        ids.Should().Equal("stock-w-0001:1");
        total.Should().Be(1);
    }

    [Fact]
    public async Task GivenTypedTextNamingAStoredKey_WhenThePageIsRead_ThenThatModelComesBack()
    {
        var (ids, total, _) = await Read(Filter(text: "stock-w"));

        using var scope = new AssertionScope();

        ids.Should().Equal("stock-w-0001:1");
        total.Should().Be(1);
    }

    [Fact]
    public async Task GivenTypedTextInAnotherCase_WhenThePageIsRead_ThenItStillMatches()
    {
        var (ids, _, _) = await Read(Filter(text: "WAREHOUSE"));

        ids.Should().Equal("stock-w-0001:1");
    }

    /// <summary>
    /// Neither page shows a stored model's state, so a row matching on it would come back with
    /// nothing on it carrying what was typed.
    /// </summary>
    [Fact]
    public async Task GivenTypedTextFoundOnlyInThePayload_WhenThePageIsRead_ThenNothingMatches()
    {
        var (_, total, error) = await Read(Filter(text: OnlyInPayloads));

        using var scope = new AssertionScope();

        error.Should().BeNull();
        total.Should().Be(0, "the payload is not looked in, because neither page shows it");
    }

    [Fact]
    public async Task GivenSeveralNarrowings_WhenThePageIsRead_ThenEachOneAsksForLess()
    {
        var (ids, total, _) = await Read(
            Filter(streamPattern: "customer:%", modelType: Account, text: "c-0002"));

        using var scope = new AssertionScope();

        ids.Should().Equal("account-c-0002:1");
        total.Should().Be(1);
    }

    /// <summary>
    /// The kind narrows whatever else was asked for: one container holds both, and a projection
    /// answering an aggregate's filter would be a row on the wrong page.
    /// </summary>
    [Fact]
    public async Task GivenAFilterMatchingBothKinds_WhenProjectionsAreRead_ThenOnlyTheyComeBack()
    {
        var (ids, total, _) = await Read(
            Filter(streamPattern: "customer:%", kind: StreamedModelKind.Projection));

        using var scope = new AssertionScope();

        ids.Should().Equal("history-c-0001:1");
        total.Should().Be(1, "the two customer aggregates are not projections");
    }
}
