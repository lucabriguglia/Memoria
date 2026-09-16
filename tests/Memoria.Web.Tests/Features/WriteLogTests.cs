using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Every line the tool logs about a write names the operator who asked for it. An assembly on the
/// host that nobody remembers uploading is answered by the log the host already keeps, not by
/// reconstruction from access logs.
/// </summary>
public class WriteLogTests
{
    private const string Admins = "memoria-admins";

    private const string Ada = "Ada Lovelace (ada lovelace)";

    [Fact]
    public async Task Names_the_operator_who_uploaded()
    {
        using var web = Administrator();
        var client = web.Client;

        await client.PostAsync("/settings/upload", await Upload(client));

        web.Logged.Should().Contain(entry =>
            entry.Category == "Memoria.Web.Settings" &&
            entry.Message.Contains("Installed orders.zip") && entry.Message.Contains(Ada));
    }

    /// <summary>
    /// Removing what is not there is not a failure to the store, so the failed-removal line is
    /// written the same way and left to the store's own tests to reach.
    /// </summary>
    [Fact]
    public async Task Names_the_operator_who_removed()
    {
        using var web = Administrator();
        var client = web.Client;
        await client.PostAsync("/settings/upload", await Upload(client));

        await client.PostAsync("/settings/delete", await Form(client, ("name", "orders.zip")));

        web.Logged.Should().Contain(entry =>
            entry.Message.Contains("Removed orders.zip") && entry.Message.Contains(Ada));
    }

    /// <summary>
    /// Rereading changes what every operator resolves, so it is a write in every sense but the
    /// store's — and today it logs the catalogue but never that somebody asked.
    /// </summary>
    [Fact]
    public async Task Names_the_operator_who_reread_the_extensions()
    {
        using var web = Administrator();
        var client = web.Client;

        await client.PostAsync("/settings/refresh", await Form(client));

        web.Logged.Should().Contain(entry =>
            entry.Category == "Memoria.Web.Settings" &&
            entry.Message.Contains("Reread") && entry.Message.Contains(Ada));
    }

    /// <summary>
    /// Whatever the store then says — refreshed, or could not — the line about the model carries
    /// the operator. Proved on the streamed store; the DCB handler answers through the same
    /// method, and its sample identifiers all take a parameter called <c>id</c>, which is also the
    /// form field that names the identifier type, so a post of them cannot reach the store here.
    /// </summary>
    [Fact]
    public async Task Names_the_operator_who_refreshed_a_snapshot()
    {
        using var web = Administrator().WithSampleTypes();
        var client = web.Client;

        await client.PostAsync("/samples/streamed/aggregates/update", await Form(client,
            ("type", typeof(SampleAggregate).FullName!),
            ("stream", "sample:1"),
            ("id", "sample-1:1"),
            ("returnUrl", "/samples/streamed/aggregates")));

        var about = web.Logged
            .Where(entry => entry.Category == "Memoria.Web.Streamed" && entry.Message.Contains(nameof(SampleAggregate)))
            .ToList();
        about.Should().NotBeEmpty();
        about.Should().AllSatisfy(entry => entry.Message.Should().Contain(Ada));
    }

    /// <summary>
    /// A line is found in a tool like Application Insights by the name it is filed under, not by
    /// its wording — so each write has one, and each of a write's outcomes has its own.
    /// </summary>
    [Fact]
    public async Task Files_each_write_under_its_own_event_name()
    {
        using var web = Administrator();
        var client = web.Client;

        await client.PostAsync("/settings/upload", await Upload(client));
        await client.PostAsync("/settings/delete", await Form(client, ("name", "orders.zip")));
        await client.PostAsync("/settings/refresh", await Form(client));

        web.Logged.Select(entry => entry.Event).Should().ContainInOrder(
            "ExtensionInstalled", "ExtensionRemoved", "ExtensionsReread");
    }

    [Fact]
    public async Task Says_which_snapshot_was_refreshed()
    {
        using var web = Administrator().WithSampleTypes().WithDomainService(Answering(new SampleAggregate()));
        var client = web.Client;

        await client.PostAsync("/samples/streamed/aggregates/update", await Update(client));

        web.Logged.Should().ContainSingle(entry => entry.Event == "SnapshotRefreshed")
            .Which.Should().Match<MemoriaWeb.LogEntry>(entry =>
                entry.Level == LogLevel.Information &&
                entry.Message.Contains(nameof(SampleAggregate)) &&
                entry.Message.Contains("sample:1") &&
                entry.Message.Contains("sample-1:1") &&
                entry.Message.Contains(Ada));
    }

    /// <summary>
    /// A store with nothing to bring up to date wrote nothing, and the log must not say it did:
    /// a snapshot that was never written is the first thing somebody reading it back would look for.
    /// </summary>
    [Fact]
    public async Task Says_when_there_was_nothing_to_refresh()
    {
        using var web = Administrator().WithSampleTypes().WithDomainService(Answering((SampleAggregate?)null));
        var client = web.Client;

        await client.PostAsync("/samples/streamed/aggregates/update", await Update(client));

        web.Logged.Should().NotContain(entry => entry.Event == "SnapshotRefreshed");
        web.Logged.Should().ContainSingle(entry => entry.Event == "SnapshotUpToDate")
            .Which.Message.Should().ContainAll(nameof(SampleAggregate), "sample:1", "sample-1:1", Ada);
    }

    [Fact]
    public async Task Says_why_a_snapshot_could_not_be_refreshed()
    {
        using var web = Administrator().WithSampleTypes()
            .WithDomainService(Answering(new Failure(Title: "The store is read-only.")));
        var client = web.Client;

        await client.PostAsync("/samples/streamed/aggregates/update", await Update(client));

        web.Logged.Should().NotContain(entry => entry.Event == "SnapshotRefreshed");
        web.Logged.Should().ContainSingle(entry => entry.Event == "SnapshotNotRefreshed")
            .Which.Should().Match<MemoriaWeb.LogEntry>(entry =>
                entry.Level == LogLevel.Warning &&
                entry.Message.Contains("sample-1:1") &&
                entry.Message.Contains("The store is read-only.") &&
                entry.Message.Contains(Ada));
    }

    /// <summary>
    /// A DCB model has no stream to name it by; it is addressed by the values its identifier was
    /// built from, so those are what the line says. Only those: the token and the return address
    /// arrive on the same form and belong in no log.
    /// </summary>
    [Fact]
    public async Task Says_how_a_dcb_snapshot_was_addressed()
    {
        using var web = Administrator().WithSampleTypes();
        var client = web.Client;

        await client.PostAsync("/samples/dcb/aggregates/update", await Form(client,
            ("type", typeof(SampleCarryingDcbAggregate).FullName!),
            ("id", typeof(SampleCarryingId).FullName!),
            ("sampleId", "sample-7"),
            ("returnUrl", "/samples/dcb/aggregates")));

        web.Logged.Should().ContainSingle(entry => entry.Event != null && entry.Event.StartsWith("Snapshot"))
            .Which.Should().Match<MemoriaWeb.LogEntry>(entry =>
                entry.Category == "Memoria.Web.Dcb" &&
                entry.Message.Contains(nameof(SampleCarryingDcbAggregate)) &&
                entry.Message.Contains("SampleCarryingId(sampleId=sample-7)") &&
                !entry.Message.Contains(Forms.AntiforgeryField) &&
                !entry.Message.Contains("returnUrl") &&
                entry.Message.Contains(Ada));
    }

    /// <summary>
    /// The operator is one value in the wording and two beside it: the name a person recognises
    /// and the subject the provider keys them by, each a column of its own, so a tool like
    /// Application Insights can be asked for everything one subject did without matching text.
    /// </summary>
    [Fact]
    public async Task Says_the_operator_as_a_name_column_and_a_subject_column()
    {
        using var web = Administrator().WithSampleTypes().WithDomainService(Answering(new SampleAggregate()));
        var client = web.Client;

        await client.PostAsync("/settings/upload", await Upload(client));
        await client.PostAsync("/samples/streamed/aggregates/update", await Update(client));

        var lines = web.Logged
            .Where(entry => entry.Event is "ExtensionInstalled" or "SnapshotRefreshed")
            .ToList();
        lines.Should().HaveCount(2);
        lines.Should().AllSatisfy(entry =>
        {
            entry.Columns.Should().Contain("Operator", Ada);
            entry.Columns.Should().Contain("OperatorName", "Ada Lovelace");
            entry.Columns.Should().Contain("OperatorSubject", "ada lovelace");
        });
    }

    [Fact]
    public async Task Says_nobody_when_running_open()
    {
        using var web = MemoriaWeb.Open();
        var client = web.Client;

        await client.PostAsync("/settings/upload", await Upload(client, tokenPage: "/settings"));

        web.Logged.Should().Contain(entry =>
            entry.Message.Contains("Installed orders.zip") && entry.Message.Contains("nobody (running open)"));
    }

    /// <summary>A streamed store answering every update with what it is given: a model, nothing, or a failure.</summary>
    private static IDomainService Answering(Result<SampleAggregate?> answer)
    {
        var store = Substitute.For<IDomainService>();
        store.UpdateAggregate<SampleAggregate>(
                Arg.Any<IStreamId>(), Arg.Any<IAggregateId<SampleAggregate>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(answer));
        return store;
    }

    private static async Task<FormUrlEncodedContent> Update(HttpClient client) =>
        await Form(client,
            ("type", typeof(SampleAggregate).FullName!),
            ("stream", "sample:1"),
            ("id", "sample-1:1"),
            ("returnUrl", "/samples/streamed/aggregates"));

    private static MemoriaWeb Administrator() =>
        MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Admins))
            .With("Authorization:Roles:Administrator", Admins);

    private static async Task<MultipartFormDataContent> Upload(HttpClient client, string tokenPage = "/")
    {
        var page = await client.GetStringAsync(tokenPage);

        return new MultipartFormDataContent
        {
            { new StringContent(Forms.AntiforgeryToken(page)), Forms.AntiforgeryField },
            { new ByteArrayContent(Forms.Zip("Contoso.Orders.dll")), "files", "orders.zip" }
        };
    }

    private static async Task<FormUrlEncodedContent> Form(HttpClient client, params (string Name, string Value)[] fields)
    {
        var page = await client.GetStringAsync("/");

        return new FormUrlEncodedContent(
            fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))
                .Prepend(new KeyValuePair<string, string>(Forms.AntiforgeryField, Forms.AntiforgeryToken(page))));
    }
}
