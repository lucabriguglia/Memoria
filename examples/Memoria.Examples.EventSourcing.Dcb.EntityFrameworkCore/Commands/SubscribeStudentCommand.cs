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
/// The three steps are the whole idea: choose a boundary, read where it stands and fold it, then
/// append on condition that it has not moved. There is no one-call form — <c>UpdateAggregate</c>
/// only refreshes a snapshot and appends nothing, exactly as it does in the streamed store.
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
        // 1. The identifier carries the boundary, so the decision and the events it may read cannot
        //    disagree: everything it reads and nothing else, which is why a subscription to a
        //    different course by a different student never contends with this one.
        var decisionId = new SubscriptionDecisionId(command.CourseId, command.StudentId);
        var boundary = decisionId.Boundary;

        var decision = new SubscriptionDecision().About(command.CourseId, command.StudentId);

        // 2. Read where the boundary stands, then fold it. The order matters: the position is a claim
        //    about what this decision saw, so reading it after the fold would let an event slip in
        //    between and be counted as seen when it was not — the append would then be accepted on a
        //    decision that never read it. Reading first means such an event makes the append fail
        //    instead, and the command is retried.
        var positionResult = await dcb.GetLatestPosition(boundary, cancellationToken: cancellationToken);
        if (positionResult.IsNotSuccess)
        {
            return positionResult.Failure!;
        }

        //    The position cannot come from the fold: it stops at the last event this model's type
        //    filter accepted, which can be behind the boundary's head with nothing else running.
        var eventsResult = await dcb.GetEvents(boundary, decision.EventTypeFilter, cancellationToken);
        if (eventsResult.IsNotSuccess)
        {
            return eventsResult.Failure!;
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
