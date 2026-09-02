using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Events;

namespace Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Domain;

/// <summary>
/// One course: how many seats it has, and how many are taken.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart to <see cref="SubscriptionDecision"/>, and the reason both are here. A
/// subscription decision spans a course <em>and</em> a student, so the caller folds it by hand and
/// appends with <c>SaveEvents</c>. A course is identified by its own id and reads one tag, so the
/// store can build it, fold it and keep a snapshot of it — which is what <c>GetAggregate</c> and
/// <c>SaveAggregate</c> are for.
/// </para>
/// <para>
/// Its boundary is <c>course:{id}</c> alone, so every event it reads is already about this course
/// and <c>Apply</c> never has to check. <see cref="SubscriptionDecision"/> does have to, because its
/// boundary spans two entities and one subscription event means different things seen through each.
/// </para>
/// <para>
/// It reads the subscriptions as well as the capacity, and that is what makes the boundary worth
/// having: without them the capacity could be set below the seats already taken.
/// </para>
/// </remarks>
[AggregateType("Course")]
public class Course : DcbAggregateRoot
{
    public bool Exists { get; private set; }

    public int Capacity { get; private set; }

    public int SeatsTaken { get; private set; }

    public int SeatsFree => Capacity - SeatsTaken;

    public override Type[]? EventTypeFilter { get; } =
    [
        typeof(CourseDefinedEvent),
        typeof(CourseCapacityChangedEvent),
        typeof(StudentSubscribedEvent),
        typeof(StudentUnsubscribedEvent)
    ];

    /// <summary>
    /// The course this fold is about, taken from the boundary rather than from a constructor.
    /// </summary>
    /// <remarks>
    /// <see cref="DcbModel.Tags"/> is how a model learns what it was built from: the store sets it
    /// from <see cref="IDcbAggregateId.Boundary"/> before folding, so <c>Apply</c> and the decision
    /// methods can both read it. Here it holds exactly one tag, because the boundary is one course.
    /// </remarks>
    private string CourseCode => Tags.Single(tag => tag.Key == "course").Value;

    /// <summary>
    /// Changes the capacity, or explains why it cannot.
    /// </summary>
    public string? ChangeCapacityTo(int capacity)
    {
        if (!Exists) return $"Course '{CourseCode}' does not exist.";
        if (capacity < 1) return $"Capacity must be at least one seat, not {capacity}.";
        if (capacity == Capacity) return $"Course '{CourseCode}' already seats {capacity}.";

        if (capacity < SeatsTaken)
        {
            return $"Course '{CourseCode}' has {SeatsTaken} seats taken, so it cannot drop to {capacity}.";
        }

        // Staged with no tags of its own, so it inherits the aggregate's — which the store set from
        // the boundary. For a model that reads exactly one tag that is exactly the right tag.
        Add(new CourseCapacityChangedEvent(CourseCode, capacity));

        return null;
    }

    protected override bool Apply<T>(T @event)
    {
        switch (@event)
        {
            case CourseDefinedEvent defined:
                Exists = true;
                Capacity = defined.Capacity;
                return true;

            case CourseCapacityChangedEvent changed:
                Capacity = changed.Capacity;
                return true;

            case StudentSubscribedEvent:
                SeatsTaken++;
                return true;

            case StudentUnsubscribedEvent:
                SeatsTaken--;
                return true;

            default:
                return false;
        }
    }
}

/// <summary>
/// Identifies the course, and therefore its snapshot, and carries the boundary it is folded from.
/// </summary>
public class CourseId(string courseId) : IDcbAggregateId<Course>
{
    public string Id { get; } = courseId;

    /// <summary>
    /// One tag. Everything the course needs is written under it — the definition, the capacity
    /// changes, and every subscription to it.
    /// </summary>
    public TagQuery Boundary { get; } = TagQuery.AnyOf(new Tag("course", courseId));
}
