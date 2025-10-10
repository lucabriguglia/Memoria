using Memoria.Results;

namespace Memoria.EventSourcing.Store.Cosmos;

/// <summary>
/// Provides error handling utilities for Cosmos DB event sourcing operations.
/// </summary>
public static class ErrorHandling
{
    /// <summary>
    /// Gets a default failure result for general error scenarios.
    /// </summary>
    public static Failure DefaultFailure => new(
        Title: "Error",
        Description: "There was an error when processing the request"
    );
}
