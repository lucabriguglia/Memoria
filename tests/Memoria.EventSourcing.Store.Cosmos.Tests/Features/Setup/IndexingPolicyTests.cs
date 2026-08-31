using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Memoria.EventSourcing.Store.Cosmos.Configuration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Memoria.EventSourcing.Store.Cosmos.Tests.Features.Setup;

/// <summary>
/// The indexing policy <see cref="CosmosSetup"/> gives a container it creates.
/// </summary>
/// <remarks>
/// The same policy ships as JSON under <c>scripts/install</c> for consumers who provision their
/// containers elsewhere. Two copies of the same thing drift, so one test here compares them
/// directly — it needs no emulator and is the reason the JSON can be trusted.
/// </remarks>
public class IndexingPolicyTests
{
    [Fact]
    public void GivenTheRecommendedPolicy_ThenItMatchesTheShippedJson()
    {
        var shipped = ShippedPolicyJson();
        var policy = CosmosIndexingPolicy.CreateRecommended();

        using (new AssertionScope())
        {
            policy.Automatic.Should().Be(shipped.GetProperty("automatic").GetBoolean());
            policy.IndexingMode.ToString().ToLowerInvariant()
                .Should().Be(shipped.GetProperty("indexingMode").GetString());

            policy.IncludedPaths.Select(path => path.Path).Should().BeEquivalentTo(
                shipped.GetProperty("includedPaths").EnumerateArray()
                    .Select(entry => entry.GetProperty("path").GetString()));

            policy.ExcludedPaths.Select(path => path.Path).Should().BeEquivalentTo(
                shipped.GetProperty("excludedPaths").EnumerateArray()
                    .Select(entry => entry.GetProperty("path").GetString()));

            policy.CompositeIndexes.Count
                .Should().Be(shipped.GetProperty("compositeIndexes").GetArrayLength());
        }
    }

    [Fact]
    public void GivenTheRecommendedPolicy_ThenItDoesNotIndexTheSystemIdPath()
    {
        // Cosmos DB rejects a policy that lists /id: it always indexes it and refuses to have that
        // overridden. Including it made the whole policy unusable, so it is worth pinning.
        CosmosIndexingPolicy.CreateRecommended().IncludedPaths
            .Select(path => path.Path).Should().NotContain(path => path!.StartsWith("/id"));
    }

    [Fact]
    public void GivenTheRecommendedPolicy_ThenItIndexesThePartitionKeyPath()
    {
        // Excluding /streamId looks free because every query passes a partition key, but it more
        // than doubles the MAX(sequence) charge that guards every save.
        CosmosIndexingPolicy.CreateRecommended().IncludedPaths
            .Select(path => path.Path).Should().Contain("/streamId/?");
    }

    [Fact]
    public void GivenTheRecommendedPolicy_WhenAskedTwice_ThenTheCopiesAreIndependent()
    {
        // IndexingPolicy is mutable and callers pass it to the SDK, so handing out a shared instance
        // would let one caller's edit reach another's container.
        var first = CosmosIndexingPolicy.CreateRecommended();
        var second = CosmosIndexingPolicy.CreateRecommended();

        first.IncludedPaths.Clear();

        second.IncludedPaths.Should().NotBeEmpty();
    }

    [Trait("Category", "Emulator")]
    [Fact]
    public async Task GivenNoContainer_WhenCreated_ThenItCarriesTheRecommendedPolicy()
    {
        var setup = SetupFor("MemoriaSetupProbe", $"created-{Guid.NewGuid():N}");

        var container = await setup.CreateDatabaseAndContainerIfNotExist();

        var properties = await container.ReadContainerAsync();
        using (new AssertionScope())
        {
            properties.Resource.IndexingPolicy.IncludedPaths.Select(path => path.Path)
                .Should().BeEquivalentTo(
                    CosmosIndexingPolicy.CreateRecommended().IncludedPaths.Select(path => path.Path));
            properties.Resource.IndexingPolicy.ExcludedPaths.Select(path => path.Path)
                .Should().Contain("/*");
        }

        await container.DeleteContainerAsync();
    }

    [Trait("Category", "Emulator")]
    [Fact]
    public async Task GivenAnExplicitPolicy_WhenTheContainerIsCreated_ThenThatPolicyIsUsed()
    {
        var setup = SetupFor("MemoriaSetupProbe", $"explicit-{Guid.NewGuid():N}");

        // The Cosmos DB default, for a consumer who wants to keep indexing everything.
        var container = await setup.CreateDatabaseAndContainerIfNotExist(new IndexingPolicy());

        var properties = await container.ReadContainerAsync();
        properties.Resource.IndexingPolicy.IncludedPaths.Select(path => path.Path)
            .Should().Contain("/*");

        await container.DeleteContainerAsync();
    }

    [Trait("Category", "Emulator")]
    [Fact]
    public async Task GivenAnExistingContainer_WhenThePolicyIsReplaced_ThenTheContainerCarriesIt()
    {
        var setup = SetupFor("MemoriaSetupProbe", $"replaced-{Guid.NewGuid():N}");
        var container = await setup.CreateDatabaseAndContainerIfNotExist(new IndexingPolicy());

        await setup.ReplaceIndexingPolicy(CosmosIndexingPolicy.CreateRecommended());

        var properties = await container.ReadContainerAsync();
        using (new AssertionScope())
        {
            properties.Resource.IndexingPolicy.IncludedPaths.Select(path => path.Path)
                .Should().NotContain("/*");
            properties.Resource.IndexingPolicy.ExcludedPaths.Select(path => path.Path)
                .Should().Contain("/*");
        }

        await container.DeleteContainerAsync();
    }

    private static CosmosSetup SetupFor(string database, string container)
    {
        var options = Substitute.For<IOptions<CosmosOptions>>();
        options.Value.Returns(new CosmosOptions
        {
            Endpoint = "https://localhost:8081",
            AuthKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            DatabaseName = database,
            ContainerName = container
        });

        return new CosmosSetup(options, new CosmosClientProvider(options));
    }

    private static JsonElement ShippedPolicyJson()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Memoria.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the repository root should be found by walking up from the test binaries");

        var path = Path.Combine(directory!.FullName, "scripts", "install", "1.7.0-cosmos-indexing-policy.json");
        File.Exists(path).Should().BeTrue($"the shipped policy should exist at {path}");

        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }
}
