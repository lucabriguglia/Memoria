using System.Collections.Generic;
using System.Net.Http;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Domain;
using Memoria.EventSourcing.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Store.EntityFrameworkCore.Extensions;
using Memoria.Web.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Each service reads the store its connection string names: two services over two files show two
/// different sets of rows, a service naming a string the configuration lacks is listed as
/// unreachable and its pages say so, start-up logs which string each service opened, and two
/// services that both declare an event under one name each open their own rows through their own
/// bindings — the clash the tool could not resolve while there was one process-wide map.
/// </summary>
public class ServiceStoresTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"memoria_web_stores_{Guid.NewGuid():N}");

    private string FileA => Path.Combine(_directory, "a.db");

    private string FileB => Path.Combine(_directory, "b.db");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes one event to a SQLite file through the framework's own store, creating the tables
    /// first, since the tool creates nothing.
    /// </summary>
    private static async Task Seed(string file, IEvent @event, string stream)
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>().UseSqlite($"Data Source={file}").Options;
        await using var context = new StreamedStoreDbContext(options, TimeProvider.System, Substitute.For<IHttpContextAccessor>());
        await context.Database.EnsureCreatedAsync();

        var saved = await new EntityFrameworkCoreDomainService(context)
            .SaveEvents(new SamplePrefixedStreamId(stream), [@event], expectedEventSequence: 0);

        saved.IsSuccess.Should().BeTrue(saved.Failure?.Description);
    }

    [Fact]
    public async Task Reads_each_service_through_the_store_its_connection_string_names()
    {
        await Seed(FileA, new SampleHappenedEvent("in-a"), "only-in-a");
        using var web = MemoriaWeb.Open().WithSampleTypes()
            .WithService("Orders", connectionString: "Orders")
            .WithService("Billing", connectionString: "Billing")
            .With("ConnectionStrings:Orders", $"Data Source={FileA}")
            .With("ConnectionStrings:Billing", $"Data Source={FileB}");
        await Seed(FileB, new SampleHappenedEvent("in-b"), "only-in-b");

        var orders = Markup.Plain(await web.Client.GetStringAsync("/orders/streamed/events/data"));
        var billing = Markup.Plain(await web.Client.GetStringAsync("/billing/streamed/events/data"));

        using (new AssertionScope())
        {
            orders.Should().Contain("sample:only-in-a").And.NotContain("sample:only-in-b");
            billing.Should().Contain("sample:only-in-b").And.NotContain("sample:only-in-a");
        }
    }

    /// <summary>
    /// The one write goes through the same store the reads do. A snapshot seeded in one file is
    /// found by Update under the service over that file — the store answers that it is up to
    /// date — and not under the service over the other, whose store holds no such row.
    /// </summary>
    [Fact]
    public async Task Updates_a_row_through_the_store_of_the_service_it_is_posted_under()
    {
        await Seed(FileA, new SampleHappenedEvent("in-a"), "only-in-a");
        await Seed(FileB, new SampleHappenedEvent("in-b"), "only-in-b");
        await SeedSnapshot(FileB, stream: "only-in-b", id: "sample-b", version: 1);
        using var web = MemoriaWeb.Open().WithSampleTypes()
            .WithService("Orders", connectionString: "Orders")
            .WithService("Billing", connectionString: "Billing")
            .With("ConnectionStrings:Orders", $"Data Source={FileA}")
            .With("ConnectionStrings:Billing", $"Data Source={FileB}");
        var client = web.Client;

        var billing = await client.PostAsync("/billing/streamed/aggregates/update", await Update(client, "/billing"));
        var orders = await client.PostAsync("/orders/streamed/aggregates/update", await Update(client, "/orders"));

        using (new AssertionScope())
        {
            billing.Headers.Location?.ToString().Should().Contain("message=Snapshot");
            orders.Headers.Location?.ToString().Should().Contain("message=Nothing");
        }
    }

    /// <summary>
    /// A snapshot row written the way the store writes one, so Update finds an aggregate at that
    /// version with nothing newer to fold — which it answers as refreshed rather than as nothing.
    /// </summary>
    private static async Task SeedSnapshot(string file, string stream, string id, int version)
    {
        var options = new DbContextOptionsBuilder<DomainDbContext>().UseSqlite($"Data Source={file}").Options;
        await using var context = new StreamedStoreDbContext(options, TimeProvider.System, Substitute.For<IHttpContextAccessor>());
        var aggregate = new SampleAggregate { Version = version };
        context.Aggregates.Add(aggregate.ToAggregateEntity(
            new SamplePrefixedStreamId(stream), new SampleAggregateId(id), newLatestEventSequence: version));
        await context.SaveChangesAsync();
    }

    /// <summary>The Update form for the snapshot <see cref="SeedSnapshot"/> writes, under a service.</summary>
    private static async Task<FormUrlEncodedContent> Update(HttpClient client, string service)
    {
        var page = await client.GetStringAsync("/");
        var fields = new Dictionary<string, string>
        {
            [Forms.AntiforgeryField] = Forms.AntiforgeryToken(page),
            ["type"] = typeof(SampleAggregate).FullName!,
            ["stream"] = "sample:only-in-b",
            ["id"] = "sample-b:1",
            ["returnUrl"] = $"{service}/streamed/aggregates"
        };

        return new FormUrlEncodedContent(fields);
    }

    [Fact]
    public async Task Lists_a_service_naming_an_unconfigured_string_as_unreachable_and_its_pages_say_so()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes().WithService("Billing", connectionString: "Billing");

        var home = Markup.Plain(await web.Client.GetStringAsync("/"));
        var page = await web.Client.GetAsync("/billing/streamed/events/data");

        using (new AssertionScope())
        {
            home.Should().Contain("Billing").And.Contain("not configured");
            page.StatusCode.Should().Be(HttpStatusCode.OK);
            (await page.Content.ReadAsStringAsync()).Should().Contain("Billing").And.Contain("not configured");
        }
    }

    [Fact]
    public async Task Logs_one_line_per_service_saying_which_string_it_opened()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes()
            .WithService("Orders", connectionString: "Orders")
            .WithService("Billing", connectionString: "Billing")
            .With("ConnectionStrings:Orders", $"Data Source={FileA}");

        await web.Client.GetAsync("/");

        using (new AssertionScope())
        {
            web.Logged.Should().Contain(entry => entry.Level == LogLevel.Information &&
                entry.Message.Contains("Orders") && entry.Message.Contains("SQLite"));
            web.Logged.Should().Contain(entry => entry.Level == LogLevel.Warning &&
                entry.Message.Contains("Billing") && entry.Message.Contains("not configured"));
        }
    }

    /// <summary>
    /// The reason the bindings became a set a store carries: two services that both declare
    /// <c>ProductCreated:1</c> — in two assemblies, over two files — each open their own rows
    /// through their own type, where one process-wide map could only bind the key to one of them.
    /// </summary>
    [Fact]
    public async Task Opens_the_same_event_name_in_two_services_through_each_one_s_own_bindings()
    {
        var first = OneSidedAssembly.DeclaringEvent("FirstShop", "ProductCreated");
        var second = OneSidedAssembly.DeclaringEvent("SecondShop", "ProductCreated");
        await Seed(FileA, Event(first), "shop-a");
        await Seed(FileB, Event(second), "shop-b");
        using var web = MemoriaWeb.Open()
            .WithService("First Shop", connectionString: "First", assembly: first)
            .WithService("Second Shop", connectionString: "Second", assembly: second)
            .With("ConnectionStrings:First", $"Data Source={FileA}")
            .With("ConnectionStrings:Second", $"Data Source={FileB}");

        var one = Markup.Plain(await web.Client.GetStringAsync("/first-shop/streamed/events/data"));
        var two = Markup.Plain(await web.Client.GetStringAsync("/second-shop/streamed/events/data"));

        using (new AssertionScope())
        {
            one.Should().Contain("sample:shop-a").And.NotContain("No uploaded type is registered");
            two.Should().Contain("sample:shop-b").And.NotContain("No uploaded type is registered");
        }
    }

    private static IEvent Event(System.Reflection.Assembly assembly) =>
        (IEvent)Activator.CreateInstance(assembly.GetTypes().Single())!;
}
