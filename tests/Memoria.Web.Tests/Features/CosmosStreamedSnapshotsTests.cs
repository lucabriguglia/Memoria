using Memoria.EventSourcing.Domain;
using System;
using System.Linq;
using System.Net;
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
/// The stored models, read from the container the Cosmos store writes them into.
/// </summary>
/// <remarks>
/// Aggregates and projections are one table relationally and one container here, told apart by
/// <c>documentType</c> — and they do not even name their type in the same property, so the read
/// differs by more than a discriminator.
/// <para>
/// The dates are deliberately out of step: each model's <c>createdDate</c> rises with its stream
/// while its <c>updatedDate</c> falls. Ordering by one therefore reverses the other, so a test
/// asking for the created order cannot pass by accident on a read that sorted by updated.
/// </para>
/// </remarks>
[Trait("Category", "Emulator")]
[Collection(CosmosCollection.Name)]
public class CosmosStreamedSnapshotsTests : IAsyncLifetime
{
    private const string Endpoint = "https://localhost:8081";

    private const string Key =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private readonly string _databaseName = $"memoria_web_tests_{Guid.NewGuid():N}";

    private const string ContainerName = "Domain";
    private const string AggregateType = "CustomerAccount:1";
    private const string ProjectionType = "CustomerOrderHistory:1";
    private const int Models = 3;

    private CosmosClient _client = null!;
    private Container _container = null!;
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
        var properties = new ContainerProperties(ContainerName, "/streamId");

        // One per sort the pages offer. Cosmos matches a composite index exactly or fully reversed,
        // and these mix directions, so neither pair collapses into the other.
        foreach (var date in new[] { "/createdDate", "/updatedDate" })
        {
            foreach (var order in new[] { CompositePathSortOrder.Ascending, CompositePathSortOrder.Descending })
            {
                properties.IndexingPolicy.CompositeIndexes.Add(
                [
                    new CompositePath { Path = date, Order = order },
                    new CompositePath { Path = "/streamId", Order = CompositePathSortOrder.Ascending },
                    new CompositePath { Path = "/id", Order = CompositePathSortOrder.Ascending }
                ]);
            }
        }

        await database.Database.CreateContainerIfNotExistsAsync(properties);

        _container = database.Database.GetContainer(ContainerName);
        _start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (var index = 0; index < Models; index++)
        {
            var stream = $"customer:c-000{index}";

            await _container.UpsertItemAsync(new AggregateDocument
            {
                Id = $"c-000{index}:1",
                StreamId = stream,
                AggregateType = AggregateType,
                Version = index + 1,
                LatestEventSequence = index + 10,
                Data = "{}",
                CreatedDate = _start.AddHours(index),
                UpdatedDate = _start.AddHours(Models - index)
            }, new PartitionKey(stream));

            // A different id from the aggregate's, so both can exist. That they cannot share one is
            // its own test below.
            await _container.UpsertItemAsync(new ProjectionDocument
            {
                Id = $"history-c-000{index}:1",
                StreamId = stream,
                ProjectionType = ProjectionType,
                Version = index + 100,
                LatestEventSequence = index + 20,
                Data = "{}",
                CreatedDate = _start.AddHours(index),
                UpdatedDate = _start.AddHours(Models - index)
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

    private static StreamedSnapshotFilter Filter(
        StreamedModelKind kind,
        InstanceSort sort = InstanceSort.Updated,
        bool descending = true,
        int page = 1,
        int size = 25) =>
        new(kind, StreamPattern: null, ModelType: null, IdentifierPattern: null, Text: null,
            sort, descending, page, size);

    [Fact]
    public async Task GivenStoredAggregates_WhenThePageIsRead_ThenEachRowCarriesWhatTheStoreSays()
    {
        var page = await _reads.Snapshots(Filter(StreamedModelKind.Aggregate));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(Models, "the projections in the same container are not aggregates");

        var newest = page.Snapshots.Single(snapshot => snapshot.StreamId == "customer:c-0000");

        newest.StoreId.Should().Be("c-0000:1");
        newest.Type.Should().Be(AggregateType);
        newest.Version.Should().Be(1);
        newest.Sequence.Should().Be(10, "the sequence it had read up to");
        newest.Created.Should().Be(_start);
        newest.Updated.Should().Be(_start.AddHours(Models));
    }

    [Fact]
    public async Task GivenStoredProjections_WhenThePageIsRead_ThenTheyComeBackNotTheAggregates()
    {
        var page = await _reads.Snapshots(Filter(StreamedModelKind.Projection));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(Models);
        page.Snapshots.Should().OnlyContain(snapshot => snapshot.Type == ProjectionType,
            "a projection names its type in a property of its own");
        page.Snapshots.Should().OnlyContain(snapshot => snapshot.Version >= 100);
    }

    /// <summary>
    /// Each of the four the pages offer. The two dates run opposite ways through the data, so a read
    /// that ordered by the wrong one comes back in the wrong order rather than the same one.
    /// </summary>
    [Theory]
    [InlineData(InstanceSort.Created, false, "customer:c-0000")]
    [InlineData(InstanceSort.Created, true, "customer:c-0002")]
    [InlineData(InstanceSort.Updated, false, "customer:c-0002")]
    [InlineData(InstanceSort.Updated, true, "customer:c-0000")]
    public async Task GivenASort_WhenThePageIsRead_ThenTheRightModelLeadsIt(
        InstanceSort sort, bool descending, string expected)
    {
        var page = await _reads.Snapshots(
            Filter(StreamedModelKind.Aggregate, sort, descending));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.OrderingNotice.Should().BeNull("this container carries every composite index the sorts need");
        page.Snapshots[0].StreamId.Should().Be(expected);
    }

    [Fact]
    public async Task GivenMoreModelsThanFitAPage_WhenTheSecondIsRead_ThenItHoldsWhatIsLeft()
    {
        var page = await _reads.Snapshots(Filter(StreamedModelKind.Aggregate, size: 2, page: 2));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(Models);
        page.TotalPages.Should().Be(2);
        page.Snapshots.Should().ContainSingle("three models at two to a page leave one on the second");
    }

    /// <summary>
    /// The store writes an aggregate and a projection under <c>{id}:{typeVersion}</c> with no type
    /// name in it, into one container partitioned by stream. Cosmos keeps <c>id</c> unique within a
    /// logical partition, so two models of the same stream that render the same id are one document.
    /// </summary>
    /// <remarks>
    /// Recorded rather than fixed: the id scheme belongs to the store, and changing it would break
    /// every container already written. What matters to this tool is that it reads whatever is
    /// there, and cannot mistake one kind for another — every query names a <c>documentType</c>.
    /// </remarks>
    [Fact]
    public async Task GivenAnAggregateAndAProjectionRenderingOneId_WhenBothAreWritten_ThenTheStoreKeepsOne()
    {
        const string stream = "customer:c-clash";
        const string shared = "c-clash:1";

        await _container.CreateItemAsync(new AggregateDocument
        {
            Id = shared, StreamId = stream, AggregateType = AggregateType, Version = 1,
            LatestEventSequence = 1, Data = "{}", CreatedDate = _start, UpdatedDate = _start
        }, new PartitionKey(stream));

        var second = async () => await _container.CreateItemAsync(new ProjectionDocument
        {
            Id = shared, StreamId = stream, ProjectionType = ProjectionType, Version = 1,
            LatestEventSequence = 1, Data = "{}", CreatedDate = _start, UpdatedDate = _start
        }, new PartitionKey(stream));

        (await second.Should().ThrowAsync<CosmosException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Conflict,
                "one id per logical partition, whatever kind of model wants it");
    }
}
