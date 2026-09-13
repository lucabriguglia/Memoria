using System.Security.Claims;

namespace Memoria.Web.Security;

/// <summary>
/// Who asked for a write, the way a log line says it.
/// </summary>
/// <param name="Name">The name the provider showed, or null when it sent none.</param>
/// <param name="Subject">The provider's own identifier for them, or null when it sent none.</param>
/// <remarks>
/// The name is what a person recognises; the subject is what survives a rename and what the
/// provider's own logs are keyed by, so both are written when both are there. Nothing else the
/// sign-in carried is: a log is not the place for a claim. Open, there is nobody, and the line
/// says so rather than leaving a blank that could be read as a missing name.
/// </remarks>
public sealed record Operator(string? Name, string? Subject)
{
    /// <summary>What the line says when the tool is running open.</summary>
    public const string Nobody = "nobody (running open)";

    /// <summary>The operator a request came from, or nobody when nobody is signed in.</summary>
    public static Operator Of(ClaimsPrincipal user) =>
        user.Identity is { IsAuthenticated: true } identity
            ? new Operator(
                Name: identity.Name is { Length: > 0 } name ? name : null,
                Subject: user.FindFirst("sub")?.Value is { Length: > 0 } subject ? subject : null)
            : new Operator(null, null);

    public override string ToString() => (Name, Subject) switch
    {
        (null, null) => Nobody,
        (var name, null) => name,
        (null, var subject) => subject,
        var (name, subject) => $"{name} ({subject})"
    };
}
