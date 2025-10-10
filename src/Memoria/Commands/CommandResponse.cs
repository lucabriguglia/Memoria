using OpenCqrs.Messaging;
using OpenCqrs.Notifications;

namespace OpenCqrs.Commands;

/// <summary>
/// Represents the response from a command execution, containing notifications, messages, and an optional result.
/// </summary>
public class CommandResponse
{
    /// <summary>
    /// Gets or sets the collection of notifications generated during command execution.
    /// </summary>
    public IEnumerable<INotification> Notifications { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of messages generated during command execution.
    /// </summary>
    public IEnumerable<IMessage> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the result of the command execution.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponse"/> class.
    /// </summary>
    public CommandResponse()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponse"/> class with a result.
    /// </summary>
    /// <param name="result">The result of the command execution.</param>
    public CommandResponse(object? result)
    {
        Result = result;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponse"/> class with a notification and optional result.
    /// </summary>
    /// <param name="notification">The notification to include in the response.</param>
    /// <param name="result">The result of the command execution.</param>
    public CommandResponse(INotification notification, object? result = null)
    {
        Notifications = new List<INotification>
        {
            notification
        };

        Result = result;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponse"/> class with notifications and optional result.
    /// </summary>
    /// <param name="notifications">The notifications to include in the response.</param>
    /// <param name="result">The result of the command execution.</param>
    public CommandResponse(IEnumerable<INotification> notifications, object? result = null)
    {
        Notifications = notifications;
        Result = result;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponse"/> class with a message and optional result.
    /// </summary>
    /// <param name="message">The message to include in the response.</param>
    /// <param name="result">The result of the command execution.</param>
    public CommandResponse(IMessage message, object? result = null)
    {
        Messages = new List<IMessage>
        {
            message
        };

        Result = result;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponse"/> class with messages and optional result.
    /// </summary>
    /// <param name="messages">The messages to include in the response.</param>
    /// <param name="result">The result of the command execution.</param>
    public CommandResponse(IEnumerable<IMessage> messages, object? result = null)
    {
        Messages = messages;
        Result = result;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponse"/> class with a notification, message, and optional result.
    /// </summary>
    /// <param name="notification">The notification to include in the response.</param>
    /// <param name="message">The message to include in the response.</param>
    /// <param name="result">The result of the command execution.</param>
    public CommandResponse(INotification notification, IMessage message, object? result = null)
    {
        Notifications = new List<INotification>
        {
            notification
        };

        Messages = new List<IMessage>
        {
            message
        };

        Result = result;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponse"/> class with notifications, messages, and optional result.
    /// </summary>
    /// <param name="notifications">The notifications to include in the response.</param>
    /// <param name="messages">The messages to include in the response.</param>
    /// <param name="result">The result of the command execution.</param>
    public CommandResponse(IEnumerable<INotification> notifications, IEnumerable<IMessage> messages, object? result = null)
    {
        Notifications = notifications;
        Messages = messages;
        Result = result;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponse"/> class with notifications, a message, and optional result.
    /// </summary>
    /// <param name="notifications">The notifications to include in the response.</param>
    /// <param name="message">The message to include in the response.</param>
    /// <param name="result">The result of the command execution.</param>
    public CommandResponse(IEnumerable<INotification> notifications, IMessage message, object? result = null)
    {
        Notifications = notifications;

        Messages = new List<IMessage>
        {
            message
        };

        Result = result;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandResponse"/> class with a notification, messages, and optional result.
    /// </summary>
    /// <param name="notification">The notification to include in the response.</param>
    /// <param name="messages">The messages to include in the response.</param>
    /// <param name="result">The result of the command execution.</param>
    public CommandResponse(INotification notification, IEnumerable<IMessage> messages, object? result = null)
    {
        Notifications = new List<INotification>
        {
            notification
        };

        Messages = messages;

        Result = result;
    }
}
