namespace OpenCqrs.Results;

/// <summary>
/// Represents a failed operation result.
/// </summary>
/// <param name="ErrorCode">The error code.</param>
/// <param name="Title">The error title.</param>
/// <param name="Description">The error description.</param>
/// <param name="Type">The error type.</param>
/// <param name="Tags">Additional metadata tags.</param>
public record Failure(ErrorCode ErrorCode = ErrorCode.Error, string? Title = null, string? Description = null, string? Type = null, IDictionary<string, string>? Tags = null);

/// <summary>
/// Extension methods for Failure.
/// </summary>
public static class FailureExtensions
{
    /// <summary>
    /// Sets the title for the failure.
    /// </summary>
    /// <param name="failure">The failure instance.</param>
    /// <param name="title">The title to set.</param>
    /// <returns>A new Failure instance with the updated title.</returns>
    public static Failure WithTitle(this Failure failure, string title) =>
        failure with { Title = title };

    /// <summary>
    /// Sets the description for the failure.
    /// </summary>
    /// <param name="failure">The failure instance.</param>
    /// <param name="description">The description to set.</param>
    /// <returns>A new Failure instance with the updated description.</returns>
    public static Failure WithDescription(this Failure failure, string description) =>
        failure with { Description = description };

    /// <summary>
    /// Sets the description for the failure with a list of items.
    /// </summary>
    /// <param name="failure">The failure instance.</param>
    /// <param name="description">The base description.</param>
    /// <param name="items">The items to append.</param>
    /// <returns>A new Failure instance with the combined description.</returns>
    public static Failure WithDescription(this Failure failure, string description, IEnumerable<string> items) =>
        failure with { Description = $"{description}: {string.Join(", ", items)}" };

    /// <summary>
    /// Sets the type for the failure.
    /// </summary>
    /// <param name="failure">The failure instance.</param>
    /// <param name="type">The type to set.</param>
    /// <returns>A new Failure instance with the updated type.</returns>
    public static Failure WithType(this Failure failure, string type) =>
        failure with { Type = type };

    /// <summary>
    /// Sets the tags for the failure.
    /// </summary>
    /// <param name="failure">The failure instance.</param>
    /// <param name="tags">The tags to set.</param>
    /// <returns>A new Failure instance with the updated tags.</returns>
    public static Failure WithTags(this Failure failure, IDictionary<string, string> tags) =>
        failure with { Tags = tags };
}

/// <summary>
/// Defines error codes.
/// </summary>
public enum ErrorCode
{
    /// <summary>
    /// Generic error.
    /// </summary>
    Error,

    /// <summary>
    /// Resource not found.
    /// </summary>
    NotFound,

    /// <summary>
    /// Unauthorized access.
    /// </summary>
    Unauthorized,

    /// <summary>
    /// Unprocessable entity.
    /// </summary>
    UnprocessableEntity,

    /// <summary>
    /// Bad request.
    /// </summary>
    BadRequest
}
