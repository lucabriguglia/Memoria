using System.Security.Claims;
using FluentAssertions;
using Memoria.Web.Security;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// How an operator is written into a log line: the name a person recognises, then the subject
/// the provider keys them by, which survives a rename. Nothing else the sign-in carried.
/// </summary>
public class OperatorTests
{
    [Fact]
    public void Writes_the_name_then_the_subject()
    {
        Operator.Of(SignedIn(("name", "Ada Lovelace"), ("sub", "ada"))).ToString()
            .Should().Be("Ada Lovelace (ada)");
    }

    [Fact]
    public void Writes_the_name_alone_when_the_provider_sent_no_subject()
    {
        Operator.Of(SignedIn(("name", "Ada Lovelace"))).ToString().Should().Be("Ada Lovelace");
    }

    [Fact]
    public void Writes_the_subject_alone_when_the_provider_sent_no_name()
    {
        Operator.Of(SignedIn(("sub", "ada"))).ToString().Should().Be("ada");
    }

    /// <summary>Open, nobody is signed in, and the line says so rather than leaving a blank.</summary>
    [Fact]
    public void Writes_nobody_when_nobody_is_signed_in()
    {
        Operator.Of(new ClaimsPrincipal(new ClaimsIdentity())).ToString()
            .Should().Be("nobody (running open)");
    }

    private static ClaimsPrincipal SignedIn(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value)),
            authenticationType: "test", nameType: "name", roleType: "roles"));
}
