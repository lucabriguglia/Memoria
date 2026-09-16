using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Domain;
using Memoria.Results;
using Memoria.Web.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The one write the tool makes is the one change to the store it can see coming. A refreshed
/// snapshot may be a new row on the models pages, so every remembered total goes with it rather
/// than being served stale for the rest of its lifetime.
/// </summary>
public class RefreshForgetsTotalsTests
{
    private const string Updaters = "memoria-updaters";

    [Fact]
    public async Task Forgets_every_remembered_total_when_a_snapshot_is_refreshed()
    {
        var store = Substitute.For<IDomainService>();
        store.UpdateAggregate<SampleAggregate>(
                Arg.Any<IStreamId>(), Arg.Any<IAggregateId<SampleAggregate>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((Result<SampleAggregate?>)new SampleAggregate()));

        using var web = MemoriaWeb.SignedInAs("Ada Lovelace", ("roles", Updaters))
            .With("Authorization:Roles:Updater", Updaters)
            .WithSampleTypes()
            .WithDomainService(store);
        var client = web.Client;
        var totals = web.Totals();

        var before = await totals.Total("events", () => Task.FromResult(1));
        await client.PostAsync("/samples/streamed/aggregates/update", await Form(client,
            ("type", typeof(SampleAggregate).FullName!),
            ("stream", "sample:1"),
            ("id", "sample-1:1"),
            ("returnUrl", "/samples/streamed/aggregates")));
        var after = await totals.Total("events", () => Task.FromResult(2));

        before.Should().Be(1);
        after.Should().Be(2, "the refresh wrote a row, so what was counted before it is no longer the count");
    }

    private static async Task<FormUrlEncodedContent> Form(HttpClient client, params (string Name, string Value)[] fields)
    {
        var page = await client.GetStringAsync("/");

        return new FormUrlEncodedContent(
            fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))
                .Prepend(new KeyValuePair<string, string>(Forms.AntiforgeryField, Forms.AntiforgeryToken(page))));
    }
}
