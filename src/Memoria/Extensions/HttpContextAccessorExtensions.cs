using Microsoft.AspNetCore.Http;

namespace Memoria.Extensions;

/// <summary>
/// Extension methods for <see cref="IHttpContextAccessor"/> to provide convenient access to current user information.
/// Simplifies extracting authenticated user data from the HTTP context in ASP.NET Core applications,
/// particularly useful in CQRS handlers and services that need user context information.
/// </summary>
public static class HttpContextAccessorExtensions
{
    /// <summary>
    /// Retrieves the unique identifier (subject) for the currently authenticated user.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to access the current request context.</param>
    /// <returns>
    /// The unique user identifier from the authenticated user's claims if the user is authenticated; 
    /// otherwise, <c>null</c>. This typically represents the user ID from the identity provider.
    /// </returns>
    public static string? GetCurrentUserNameIdentifier(this IHttpContextAccessor httpContextAccessor) =>
        httpContextAccessor.UserIsAuthenticated()
            ? httpContextAccessor.HttpContext?.User.GetNameIdentifier()
            : null;

    /// <summary>
    /// Retrieves the email address for the currently authenticated user.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to access the current request context.</param>
    /// <returns>
    /// The email address from the authenticated user's claims if the user is authenticated; 
    /// otherwise, <c>null</c>. The email may not always be available depending on the authentication provider.
    /// </returns>
    public static string? GetCurrentUserEmail(this IHttpContextAccessor httpContextAccessor) =>
        httpContextAccessor.UserIsAuthenticated()
            ? httpContextAccessor.HttpContext?.User.GetEmail()
            : null;

    /// <summary>
    /// Determines whether the current user is authenticated.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor used to access the current request context.</param>
    /// <returns>
    /// <c>true</c> if the current user has a valid authenticated identity; otherwise, <c>false</c>.
    /// Also returns <c>false</c> if there is no current HTTP context or user principal.
    /// </returns>
    public static bool UserIsAuthenticated(this IHttpContextAccessor httpContextAccessor)
    {
        var claimsPrincipal = httpContextAccessor.HttpContext?.User;
        return claimsPrincipal?.Identity?.IsAuthenticated is true;
    }
}
