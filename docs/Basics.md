# Basics

The **IDispatcher** interface contains all the methods needed to send commands, get results from queries, and publish notifications.

There are three kinds of requests that can be sent through the dispatcher:
- [Commands](Commands) (single handler)
- [Queries](Queries) (single handler)
- [Notifications](Notifications) (multiple handlers)

Memoria uses the result pattern to return the result of commands, queries, and notifications. The result contains information about the success or failure of the operation, any errors that occurred, and the data returned by the operation.
