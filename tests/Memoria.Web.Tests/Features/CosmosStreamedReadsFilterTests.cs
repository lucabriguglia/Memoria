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
/// Narrowing the log to what a reader came for, against a Cosmos store.
/// </summary>
/// <remarks>
/// The three filters the page offers, each proved against the engine rather than against a fake,
/// because each one is a different Cosmos SQL construct and none of them is obvious: an equality, a
/// <c>LIKE</c> over the pattern <see cref="IdShape"/> produces, and a case-insensitive
/// <c>CONTAINS</c> over three properties.
/// <para>
/// The data is shaped to tell them apart. Two order streams and one customer stream, so a stream
/// pattern that matches the orders proves it excluded the customer rather than merely returned rows;
/// one event type per stream, so a type filter cannot be mistaken for a stream filter; and a payload
/// carrying a literal <c>%</c>, because the typed text is matched as characters and a reader looking
/// for one should find it rather than match everything.
/// </para>
/// </remarks>
[Trait("Category", "Emulator")]
[Collection(CosmosCollection.Name)]
public class CosmosStreamedReadsFilterTests : IAsyncLifetime
{
    private const string Endpoint = "https://localhost:8081";

    private const string Key =
        "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private readonly string _databaseName = $"memoria_web_tests_{Guid.NewGuid():N}";

    private const string ContainerName = "Domain";

    /// <summary>The three streams, their event type, and how many events each holds.</summary>
    private static readonly (string Stream, string EventType, int Count)[] Streams =
    [
        ("order-0001", "OrderPlacedEvent:1", 4),
        ("order-0002", "OrderShippedEvent:1", 3),
        ("customer-0001", "CustomerRegisteredEvent:1", 2)
    ];

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

        var container = new ContainerProperties(ContainerName, "/streamId");

        container.IndexingPolicy.CompositeIndexes.Add(
        [
            new CompositePath { Path = "/createdDate", Order = CompositePathSortOrder.Descending },
            new CompositePath { Path = "/streamId", Order = CompositePathSortOrder.Ascending },
            new CompositePath { Path = "/sequence", Order = CompositePathSortOrder.Descending }
        ]);

        await database.Database.CreateContainerIfNotExistsAsync(container);

        var written = database.Database.GetContainer(ContainerName);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var hour = 0;

        foreach (var (stream, eventType, count) in Streams)
        {
            for (var sequence = 0; sequence < count; sequence++)
            {
                await written.UpsertItemAsync(new EventDocument
                {
                    Id = $"{stream}:{sequence}",
                    StreamId = stream,
                    EventType = eventType,
                    Sequence = sequence,
                    // The first event of each stream carries a percent sign, so text matching can be
                    // shown to read it as a character. The rest carry the stream's own name so a
                    // payload match can be told apart from a stream match by which rows come back.
                    Data = sequence == 0
                        ? $"{{\"note\":\"100% {stream}\"}}"
                        : $"{{\"note\":\"about {stream}\"}}",
                    CreatedDate = start.AddHours(hour++)
                }, new PartitionKey(stream));
            }
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

    private static StreamedEventFilter Filter(
        string? streamPattern = null, string? eventType = null, string? text = null) =>
        new(streamPattern, eventType, text, Descending: true, Page: 1, Size: 50);

    // The two questions the compare tab asks that a page answers wastefully: how many events a
    // model has, which is a count with no rows wanted, and which event sits at a place in its
    // history, which is one row with no count wanted. Each is one query rather than two.

    [Fact]
    public async Task GivenAFilter_WhenTheEventsAreCounted_ThenOnlyTheTotalComesBack()
    {
        var counted = await _reads.Count(Filter(streamPattern: "order-0001"));

        counted.Error.Should().BeNull();
        counted.Total.Should().Be(4);
    }

    [Fact]
    public async Task GivenAPlace_WhenTheEventThereIsRead_ThenThatOneRowComesBackInTheOrderAsked()
    {
        var stream = Filter(streamPattern: "order-0001") with { Descending = false };

        var third = await _reads.At(stream, index: 2);
        var newest = await _reads.At(stream with { Descending = true }, index: 0);
        var beyond = await _reads.At(stream, index: 40);

        using var scope = new AssertionScope();

        third.Error.Should().BeNull();
        third.Event!.Event.Position.Should().Be(2);
        newest.Event!.Event.Position.Should().Be(3);
        beyond.Event.Should().BeNull();
        beyond.Error.Should().BeNull();
    }

    /// <summary>
    /// The event before a given one in a stream is the newest of those below its sequence. What the
    /// compare column asks for, one row at a time, to say which of a model's own events a row
    /// follows on a stream it shares.
    /// </summary>
    [Fact]
    public async Task GivenASequenceBound_WhenThePageIsRead_ThenOnlyTheEventsBelowItComeBack()
    {
        var page = await _reads.Events(Filter(streamPattern: "order-0001") with
        {
            Size = 1,
            BeforeSequence = 3
        });

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(3, "the stream holds sequences 0 to 3 and the bound excludes 3 itself");
        page.Events.Should().ContainSingle().Which.Event.Position.Should().Be(2);
    }

    private static async Task<(string[] Streams, int Total, string? Error)> Read(
        CosmosStreamedReads reads, StreamedEventFilter filter)
    {
        var page = await reads.Events(filter);

        return (page.Events.Select(appended => appended.StreamId).Distinct().Order().ToArray(),
            page.Total, page.Error);
    }

    [Fact]
    public async Task GivenAnEventType_WhenThePageIsRead_ThenOnlyThatTypeComesBack()
    {
        var page = await _reads.Events(Filter(eventType: "OrderShippedEvent:1"));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(3, "only order-0002 carries that type");
        page.Events.Should().OnlyContain(appended => appended.Event.Type == "OrderShippedEvent:1");
    }

    /// <summary>
    /// The pattern <see cref="IdShape"/> writes: the stream's own id with a wildcard where each
    /// value it was built from stood.
    /// </summary>
    [Fact]
    public async Task GivenAStreamPattern_WhenThePageIsRead_ThenOnlyMatchingStreamsComeBack()
    {
        var (streams, total, error) = await Read(_reads, Filter(streamPattern: "order-%"));

        using var scope = new AssertionScope();

        error.Should().BeNull();
        streams.Should().Equal("order-0001", "order-0002");
        total.Should().Be(7, "the customer stream is outside the pattern");
    }

    [Fact]
    public async Task GivenAStreamPatternMatchingOne_WhenThePageIsRead_ThenOnlyItComesBack()
    {
        var (streams, total, _) = await Read(_reads, Filter(streamPattern: "customer-%"));

        using var scope = new AssertionScope();

        streams.Should().Equal("customer-0001");
        total.Should().Be(2);
    }

    [Fact]
    public async Task GivenTypedText_WhenItNamesAStream_ThenThatStreamComesBack()
    {
        var (streams, total, error) = await Read(_reads, Filter(text: "customer-0001"));

        using var scope = new AssertionScope();

        error.Should().BeNull();
        streams.Should().Equal("customer-0001");
        total.Should().Be(2, "its stream, its keys and its payloads all carry the text");
    }

    /// <summary>
    /// Matched however it was typed. The relational read lowers both sides for exactly this, and a
    /// reader should not have to know which case the store wrote.
    /// </summary>
    [Fact]
    public async Task GivenTypedTextInAnotherCase_WhenThePageIsRead_ThenItStillMatches()
    {
        var (streams, total, _) = await Read(_reads, Filter(text: "CUSTOMER-0001"));

        using var scope = new AssertionScope();

        streams.Should().Equal("customer-0001");
        total.Should().Be(2);
    }

    /// <summary>
    /// Typed text is matched as characters, not as a pattern. A reader looking for a percent sign
    /// wants the rows carrying one, not every row.
    /// </summary>
    [Fact]
    public async Task GivenTypedTextHoldingAPercent_WhenThePageIsRead_ThenItIsACharacter()
    {
        var page = await _reads.Events(Filter(text: "100%"));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(3, "one event of each stream carries it, and a wildcard would take all nine");
        page.Events.Should().OnlyContain(appended => appended.Event.Position == 0);
    }

    [Fact]
    public async Task GivenTypedTextInThePayloadOnly_WhenThePageIsRead_ThenTheRowComesBack()
    {
        var page = await _reads.Events(Filter(text: "about order-0002"));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(2, "two of that stream's three events say 'about'");
    }

    /// <summary>
    /// Narrowings combine rather than replace one another: each one the reader adds asks for less.
    /// </summary>
    [Fact]
    public async Task GivenAStreamPatternAndAnEventType_WhenThePageIsRead_ThenBothNarrow()
    {
        var (streams, total, _) = await Read(_reads,
            Filter(streamPattern: "order-%", eventType: "OrderPlacedEvent:1"));

        using var scope = new AssertionScope();

        streams.Should().Equal("order-0001");
        total.Should().Be(4);
    }

    [Fact]
    public async Task GivenAFilterNothingMatches_WhenThePageIsRead_ThenTheLogIsEmptyNotBroken()
    {
        var page = await _reads.Events(Filter(text: "nothing carries this"));

        using var scope = new AssertionScope();

        page.Error.Should().BeNull();
        page.Total.Should().Be(0);
        page.Events.Should().BeEmpty();
        page.TotalPages.Should().Be(1, "an empty log is one empty page, not none");
    }
}
