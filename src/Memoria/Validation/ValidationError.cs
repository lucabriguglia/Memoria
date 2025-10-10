namespace OpenCqrs.Validation;

/// <summary>
/// Represents a validation error that contains details about a specific property and its related error message.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Gets or sets the name of the property that failed validation.
    /// </summary>
    public required string PropertyName { get; set; }

    /// <summary>
    /// Gets or sets the error message describing the validation failure.
    /// </summary>
    public required string ErrorMessage { get; set; }
}
