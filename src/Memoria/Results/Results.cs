using OneOf;

namespace Memoria.Results;

/// <summary>
/// Represents the result of an operation that can either succeed or fail.
/// </summary>
public sealed class Result : OneOfBase<Success, Failure>
{
    /// <summary>
    /// Initializes a new instance of the Result class.
    /// </summary>
    /// <param name="input">The OneOf union.</param>
    private Result(OneOf<Success, Failure> input) : base(input) { }

    /// <summary>
    /// Implicitly converts a Success to a Result.
    /// </summary>
    /// <param name="success">The success instance.</param>
    /// <returns>A Result.</returns>
    public static implicit operator Result(Success success) => new(success);

    /// <summary>
    /// Implicitly converts a Failure to a Result.
    /// </summary>
    /// <param name="failure">The failure instance.</param>
    /// <returns>A Result.</returns>
    public static implicit operator Result(Failure failure) => new(failure);

    /// <summary>
    /// Gets whether the result is successful.
    /// </summary>
    public bool IsSuccess => IsT0;

    /// <summary>
    /// Gets whether the result is a failure.
    /// </summary>
    public bool IsFailure => IsT1;

    /// <summary>
    /// Gets whether the result is not a failure.
    /// </summary>
    public bool IsNotFailure => IsT0;

    /// <summary>
    /// Gets whether the result is not successful.
    /// </summary>
    public bool IsNotSuccess => IsT1;

    /// <summary>
    /// Gets the Success instance if successful.
    /// </summary>
    public Success? Success => IsT0 ? AsT0 : null;

    /// <summary>
    /// Gets the Failure instance if failed.
    /// </summary>
    public Failure? Failure => IsT1 ? AsT1 : null;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A Result.</returns>
    public static Result Ok() => new(new Success());

    /// <summary>
    /// Creates a successful result with the specified Success.
    /// </summary>
    /// <param name="success">The Success instance.</param>
    /// <returns>A Result.</returns>
    public static Result Ok(Success success) => new(success);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="title">The title.</param>
    /// <param name="description">The description.</param>
    /// <param name="type">The type.</param>
    /// <param name="tags">The tags.</param>
    /// <returns>A Result.</returns>
    public static Result Fail(ErrorCode errorCode = ErrorCode.Error, string? title = null, string? description = null, string? type = null, IDictionary<string, string>? tags = null) => new(new Failure(errorCode, title, description, type, tags));

    /// <summary>
    /// Creates a failed result with the specified Failure.
    /// </summary>
    /// <param name="failure">The Failure instance.</param>
    /// <returns>A Result.</returns>
    public static Result Fail(Failure failure) => new(failure);

    /// <summary>
    /// Attempts to extract the Success and Failure.
    /// </summary>
    /// <param name="success">The Success instance.</param>
    /// <param name="failure">The Failure instance.</param>
    /// <returns>True if successful.</returns>
    public bool TryPickSuccess(out Success success, out Failure failure) => TryPickT0(out success, out failure);

    /// <summary>
    /// Attempts to extract the Failure and Success.
    /// </summary>
    /// <param name="failure">The Failure instance.</param>
    /// <param name="success">The Success instance.</param>
    /// <returns>True if failed.</returns>
    public bool TryPickFailure(out Failure failure, out Success success) => TryPickT1(out failure, out success);
}

/// <summary>
/// Represents the result of an operation with a value.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public sealed class Result<TValue> : OneOfBase<Success<TValue>, Failure>
{
    /// <summary>
    /// Initializes a new instance of the Result class.
    /// </summary>
    /// <param name="input">The OneOf union.</param>
    private Result(OneOf<Success<TValue>, Failure> input) : base(input) { }

    /// <summary>
    /// Implicitly converts a Success to a Result.
    /// </summary>
    /// <param name="success">The success instance.</param>
    /// <returns>A Result.</returns>
    public static implicit operator Result<TValue>(Success<TValue> success) => new(success);

    /// <summary>
    /// Implicitly converts a Failure to a Result.
    /// </summary>
    /// <param name="failure">The failure instance.</param>
    /// <returns>A Result.</returns>
    public static implicit operator Result<TValue>(Failure failure) => new(failure);

    /// <summary>
    /// Implicitly converts a value to a Result.
    /// </summary>
    /// <param name="result">The value.</param>
    /// <returns>A Result.</returns>
    public static implicit operator Result<TValue>(TValue result) => new(new Success<TValue>(result));

    /// <summary>
    /// Gets whether the result is successful.
    /// </summary>
    public bool IsSuccess => IsT0;

    /// <summary>
    /// Gets whether the result is a failure.
    /// </summary>
    public bool IsFailure => IsT1;

    /// <summary>
    /// Gets whether the result is not a failure.
    /// </summary>
    public bool IsNotFailure => IsT0;

    /// <summary>
    /// Gets whether the result is not successful.
    /// </summary>
    public bool IsNotSuccess => IsT1;

    /// <summary>
    /// Gets the Success instance if successful.
    /// </summary>
    public Success<TValue>? Success => IsT0 ? AsT0 : null;

    /// <summary>
    /// Gets the Failure instance if failed.
    /// </summary>
    public Failure? Failure => IsT1 ? AsT1 : null;

    /// <summary>
    /// Gets the value if successful.
    /// </summary>
    public new TValue? Value => IsT0 ? AsT0.Result : default;

    /// <summary>
    /// Creates a successful result with the value.
    /// </summary>
    /// <param name="result">The value.</param>
    /// <returns>A Result.</returns>
    public static Result<TValue> Ok(TValue result) => new(new Success<TValue>(result));

    /// <summary>
    /// Creates a successful result with the Success.
    /// </summary>
    /// <param name="success">The Success instance.</param>
    /// <returns>A Result.</returns>
    public static Result<TValue> Ok(Success<TValue> success) => new(success);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="title">The title.</param>
    /// <param name="description">The description.</param>
    /// <param name="type">The type.</param>
    /// <param name="tags">The tags.</param>
    /// <returns>A Result.</returns>
    public static Result<TValue> Fail(ErrorCode errorCode = ErrorCode.Error, string? title = null, string? description = null, string? type = null, IDictionary<string, string>? tags = null) => new(new Failure(errorCode, title, description, type, tags));

    /// <summary>
    /// Creates a failed result with the Failure.
    /// </summary>
    /// <param name="failure">The Failure instance.</param>
    /// <returns>A Result.</returns>
    public static Result<TValue> Fail(Failure failure) => new(failure);

    /// <summary>
    /// Attempts to extract the Success and Failure.
    /// </summary>
    /// <param name="success">The Success instance.</param>
    /// <param name="failure">The Failure instance.</param>
    /// <returns>True if successful.</returns>
    public bool TryPickSuccess(out Success<TValue> success, out Failure failure) => TryPickT0(out success, out failure);

    /// <summary>
    /// Attempts to extract the Failure and Success.
    /// </summary>
    /// <param name="failure">The Failure instance.</param>
    /// <param name="success">The Success instance.</param>
    /// <returns>True if failed.</returns>
    public bool TryPickFailure(out Failure failure, out Success<TValue> success) => TryPickT1(out failure, out success);
}
