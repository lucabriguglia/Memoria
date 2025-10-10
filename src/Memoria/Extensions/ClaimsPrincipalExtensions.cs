using System.Security.Claims;

namespace OpenCqrs.Extensions;

/// <summary>
/// Extension methods for <see cref="ClaimsPrincipal"/> to provide convenient access to common claim values.
/// Simplifies extracting standard identity information from authenticated users in ASP.NET Core applications.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Retrieves the unique identifier (subject) for the authenticated user from the claims principal.
    /// </summary>
    /// <param name="claimsPrincipal">The claims principal representing the authenticated user.</param>
    /// <returns>
    /// The value of the <see cref="ClaimTypes.NameIdentifier"/> claim if present; otherwise, <c>null</c>.
    /// This typically represents the unique user ID from the identity provider.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the claims principal has no identities.</exception>
    public static string? GetNameIdentifier(this ClaimsPrincipal claimsPrincipal) =>
        claimsPrincipal.Identities.First().Claims
            .FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value;

    /// <summary>
    /// Retrieves the email address for the authenticated user from the claims principal.
    /// </summary>
    /// <param name="claimsPrincipal">The claims principal representing the authenticated user.</param>
    /// <returns>
    /// The value of the <see cref="ClaimTypes.Email"/> claim if present; otherwise, <c>null</c>.
    /// This represents the user's email address as provided by the identity system.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the claims principal has no identities.</exception>
    public static string? GetEmail(this ClaimsPrincipal claimsPrincipal) =>
        claimsPrincipal.Identities.First().Claims
            .FirstOrDefault(claim => claim.Type == ClaimTypes.Email)?.Value;
}
