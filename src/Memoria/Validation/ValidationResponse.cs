namespace Memoria.Validation;

/// <summary>
/// Represents the response of a validation operation.
/// </summary>
/// <remarks>
/// This class contains details about the outcome of a validation process, including whether the validation
/// was successful and any validation errors that occurred.
/// </remarks>
public class ValidationResponse
{
    /// <summary>
    /// Gets or sets the list of validation errors that occurred during the validation process.
    /// </summary>
    /// <remarks>
    /// Each item in the list represents a specific validation error, containing details such as the property name
    /// and the corresponding error message. If there are no errors, the list will be empty.
    /// </remarks>
    public IList<ValidationError> Errors { get; set; } = new List<ValidationError>();

    /// <summary>
    /// Gets a value indicating whether the validation process completed successfully without any errors.
    /// </summary>
    /// <remarks>
    /// If the validation process did not identify any errors, this property will return true.
    /// Otherwise, it will return false to indicate the presence of one or more validation errors.
    /// </remarks>
    public bool IsValid => Errors.Count == 0;
}
