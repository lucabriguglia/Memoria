namespace Memoria.Results;

/// <summary>
/// Represents a successful operation result.
/// </summary>
public record Success;

/// <summary>
/// Represents a successful operation result with data.
/// </summary>
/// <typeparam name="TResult">The type of the result data.</typeparam>
public record Success<TResult>
{
    /// <summary>
    /// Gets the result data.
    /// </summary>
    public TResult? Result { get; }

    /// <summary>
    /// Initializes a new instance with no data.
    /// </summary>
    public Success()
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified data.
    /// </summary>
    /// <param name="result">The result data.</param>
    public Success(TResult result)
    {
        Result = result;
    }
}
