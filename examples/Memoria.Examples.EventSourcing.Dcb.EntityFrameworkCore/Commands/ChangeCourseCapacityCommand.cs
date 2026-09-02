using Memoria.Commands;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Domain;
using Memoria.Results;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Commands;

public record ChangeCourseCapacityCommand(string CourseId, int Capacity) : ICommand;

/// <summary>
/// The same read-decide-append cycle as <see cref="SubscribeStudentCommandHandler"/>, with the store
/// doing the folding and keeping a snapshot.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SubscribeStudentCommandHandler"/> reads the events and folds them itself. This one
/// hands both to the store, because <see cref="Course"/> is identified by its own id: give
/// <c>GetAggregate</c> the identifier and it builds the model, sets its tags from the boundary,
/// folds every event inside it, and writes a snapshot so the next read starts from there instead of
/// from the beginning of the log.
/// </para>
/// <para>
/// The append is <c>SaveAggregate</c> rather than <c>SaveEvents</c>: it writes the staged events and
/// the updated snapshot in one transaction, so a snapshot can never be left behind the events it
/// claims to summarise.
/// </para>
/// </remarks>
public class ChangeCourseCapacityCommandHandler(IDcbDomainService dcb)
    : ICommandHandler<ChangeCourseCapacityCommand>
{
    public async Task<Result> Handle(ChangeCourseCapacityCommand command,
        CancellationToken cancellationToken = default)
    {
        var courseId = new CourseId(command.CourseId);

        // 1. Read where the boundary stands, before folding anything. Same reasoning as the
        //    subscription handler: the position is a claim about what this decision saw, so an event
        //    arriving after it must invalidate the append rather than be counted as seen.
        var positionResult = await dcb.GetLatestPosition(courseId.Boundary, cancellationToken: cancellationToken);
        if (positionResult.IsNotSuccess)
        {
            return positionResult.Failure!;
        }

        // 2. Fold. SnapshotWithNewEventsOrCreate reads the last snapshot and applies whatever
        //    arrived inside the boundary since, falling back to a fold from the beginning the first
        //    time, when there is no snapshot yet.
        var courseResult = await dcb.GetAggregate(courseId, ReadMode.SnapshotWithNewEventsOrCreate,
            cancellationToken);
        if (courseResult.IsNotSuccess)
        {
            return courseResult.Failure!;
        }

        if (courseResult.Value is not { } course)
        {
            return new Failure(ErrorCode.NotFound, "Cannot change capacity",
                $"Course '{command.CourseId}' does not exist.");
        }

        var refusal = course.ChangeCapacityTo(command.Capacity);
        if (refusal is not null)
        {
            return new Failure(ErrorCode.BadRequest, "Cannot change capacity", refusal);
        }

        // 3. Append the staged event and rewrite the snapshot, on condition that nothing matching the
        //    boundary arrived in between. A subscription to this course would move it — the two
        //    decisions read the same tag, so they contend even though they are different decisions.
        return await dcb.SaveAggregate(courseId, course,
            new AppendCondition(courseId.Boundary, positionResult.Value), cancellationToken);
    }
}
