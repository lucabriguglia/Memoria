using Memoria.Commands;
using Memoria.EventSourcing.Dcb;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Domain;
using Memoria.Results;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Commands;

public record SubscribeStudentCommand(string StudentId, string CourseId) : ICommand;

/// <summary>
/// The read-decide-append cycle, written out step by step.
/// </summary>
/// <remarks>
/// <para>
/// The three steps are the whole idea: choose a boundary, fold it and remember where it stood, then
/// append on condition that it has not moved.
/// </para>
/// <para>
/// The fold is done by hand rather than through <c>GetInMemoryAggregate</c>, because this decision
/// model has to know which course and which student it is about before it applies anything, and
/// <c>GetInMemoryAggregate</c> constructs the model itself. That method suits a model identified
/// solely by its own id; a decision model spanning two entities reads the events and folds them.
/// </para>
/// </remarks>
public class SubscribeStudentCommandHandler(IDcbDomainService dcb) : ICommandHandler<SubscribeStudentCommand>
{
    public async Task<Result> Handle(SubscribeStudentCommand command, CancellationToken cancellationToken = default)
    {
        // 1. The boundary: everything the decision reads and nothing else, so a subscription to a
        //    different course by a different student never contends with this one.
        var boundary = TagQuery.AnyOf(
            new Tag("course", command.CourseId),
            new Tag("student", command.StudentId));

        var decision = new SubscriptionDecision().About(command.CourseId, command.StudentId);

        // 2. Fold it, and read where the boundary stands. The position is read in its own right
        //    rather than taken from the last folded event: an event this model's type filter ignores
        //    still moves the boundary, and conditioning on the fold would then always fail.
        var eventsResult = await dcb.GetEvents(boundary, decision.EventTypeFilter, cancellationToken);
        if (eventsResult.IsNotSuccess)
        {
            return eventsResult.Failure!;
        }

        var positionResult = await dcb.GetLatestPosition(boundary, cancellationToken: cancellationToken);
        if (positionResult.IsNotSuccess)
        {
            return positionResult.Failure!;
        }

        decision.Apply(eventsResult.Value!);

        var refusal = decision.Subscribe();
        if (refusal is not null)
        {
            return new Failure(ErrorCode.BadRequest, "Cannot subscribe", refusal);
        }

        // 3. Append, on condition that nothing matching the boundary arrived in between. If it did,
        //    the decision rests on stale facts and is refused rather than committed.
        return await dcb.SaveEvents([..decision.UncommittedEvents],
            new AppendCondition(boundary, positionResult.Value), cancellationToken);
    }
}
