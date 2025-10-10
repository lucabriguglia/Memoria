using Memoria.Results;

namespace Memoria.Commands;

/// <summary>
/// Represents the response from a send-and-publish operation that includes results
/// for both the initial command execution and subsequent notifications.
/// </summary>
public record SendAndPublishResponse(Result<CommandResponse> CommandResult, IEnumerable<Result> NotificationResults, IEnumerable<Result> MessageResults);
