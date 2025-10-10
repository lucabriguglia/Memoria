using Memoria.Results;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore;

/// <summary>
/// Provides error handling utilities for the Entity Framework Core event store.
/// </summary>
public static class ErrorHandling
{
    /// <summary>
    /// Gets the default failure result used when an error occurs during request processing.
    /// </summary>
    public static Failure DefaultFailure => new(
        Title: "Error",
        Description: "There was an error when processing the request"
    );
}
