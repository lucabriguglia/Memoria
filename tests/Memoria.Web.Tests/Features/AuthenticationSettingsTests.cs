using System;
using System.Collections.Generic;
using FluentAssertions;
using Memoria.Web.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// How the tool decides, before it listens for anything, whether operators are signed in through
/// an OpenID Connect provider or nobody is. Left unsaid is not an answer: an upload form that runs
/// code does not get to be open by omission, so a tool told neither refuses to start and says
/// which settings would have told it.
/// </summary>
public class AuthenticationSettingsTests
{
    [Fact]
    public void Reads_the_provider_off_the_three_settings_it_needs()
    {
        var settings = AuthenticationSettings.Of(Configured(
            ("Authentication:Oidc:Authority", "https://login.example.com/realms/memoria"),
            ("Authentication:Oidc:ClientId", "memoria-web"),
            ("Authentication:Oidc:ClientSecret", "s3cret")));

        settings.Should().Be(new AuthenticationSettings.OpenIdConnect(
            Authority: "https://login.example.com/realms/memoria",
            ClientId: "memoria-web",
            ClientSecret: "s3cret",
            Scopes: "openid profile email"));
    }

    [Fact]
    public void Takes_the_scopes_it_is_given_over_the_default()
    {
        var settings = AuthenticationSettings.Of(Configured(
            ("Authentication:Oidc:Authority", "https://login.example.com/realms/memoria"),
            ("Authentication:Oidc:ClientId", "memoria-web"),
            ("Authentication:Oidc:ClientSecret", "s3cret"),
            ("Authentication:Oidc:Scopes", "openid groups")));

        settings.Should().BeOfType<AuthenticationSettings.OpenIdConnect>()
            .Which.Scopes.Should().Be("openid groups");
    }

    [Fact]
    public void Runs_open_only_when_told_to_in_so_many_words()
    {
        AuthenticationSettings.Of(Configured(("Authentication:Disabled", "true")))
            .Should().Be(new AuthenticationSettings.Disabled());
    }

    [Fact]
    public void Refuses_to_start_when_nothing_is_said()
    {
        var reading = () => AuthenticationSettings.Of(Configured());

        reading.Should().Throw<InvalidOperationException>()
            .Which.Message.Should()
            .Contain("Authentication:Oidc:Authority").And
            .Contain("Authentication:Oidc:ClientId").And
            .Contain("Authentication:Oidc:ClientSecret").And
            .Contain("Authentication:Disabled");
    }

    /// <summary>
    /// An environment variable exported with nothing after the equals sign is how a setting most
    /// often ends up empty rather than absent. It means the same thing.
    /// </summary>
    [Fact]
    public void Treats_an_empty_setting_as_one_that_was_never_set()
    {
        var reading = () => AuthenticationSettings.Of(Configured(
            ("Authentication:Disabled", ""),
            ("Authentication:Oidc:Authority", "")));

        reading.Should().Throw<InvalidOperationException>()
            .Which.Message.Should()
            .Contain("Authentication:Oidc:Authority").And
            .Contain("Authentication:Disabled");
    }

    [Fact]
    public void Refuses_to_start_when_disabled_is_not_a_yes_or_a_no()
    {
        var reading = () => AuthenticationSettings.Of(Configured(("Authentication:Disabled", "yes")));

        reading.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Authentication:Disabled").And.Contain("yes");
    }

    /// <summary>
    /// Saying "not disabled" is not the same as saying how to sign in; it is the default put into
    /// words, and gets the same refusal as silence.
    /// </summary>
    [Fact]
    public void Refuses_to_start_when_told_only_that_it_is_not_disabled()
    {
        var reading = () => AuthenticationSettings.Of(Configured(("Authentication:Disabled", "false")));

        reading.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Authentication:Oidc:Authority");
    }

    [Theory]
    [InlineData("Authentication:Oidc:Authority")]
    [InlineData("Authentication:Oidc:ClientId")]
    [InlineData("Authentication:Oidc:ClientSecret")]
    public void Refuses_to_start_when_one_of_the_three_is_missing_and_names_it(string missing)
    {
        var all = new Dictionary<string, string?>
        {
            ["Authentication:Oidc:Authority"] = "https://login.example.com/realms/memoria",
            ["Authentication:Oidc:ClientId"] = "memoria-web",
            ["Authentication:Oidc:ClientSecret"] = "s3cret"
        };
        all.Remove(missing);

        var reading = () => AuthenticationSettings.Of(
            new ConfigurationBuilder().AddInMemoryCollection(all).Build());

        reading.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(missing);
    }

    /// <summary>
    /// Told both to sign operators in and to run open, the tool does neither. Picking the flag
    /// would fail open on what is most likely a leftover, and picking the provider would make the
    /// flag mean nothing.
    /// </summary>
    [Fact]
    public void Refuses_to_start_when_told_both_to_sign_in_and_to_run_open()
    {
        var reading = () => AuthenticationSettings.Of(Configured(
            ("Authentication:Disabled", "true"),
            ("Authentication:Oidc:Authority", "https://login.example.com/realms/memoria"),
            ("Authentication:Oidc:ClientId", "memoria-web"),
            ("Authentication:Oidc:ClientSecret", "s3cret")));

        reading.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Authentication:Disabled").And.Contain("Authentication:Oidc");
    }

    private static IConfiguration Configured(params (string Key, string Value)[] settings)
    {
        var values = new Dictionary<string, string?>();

        foreach (var (key, value) in settings)
        {
            values[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
