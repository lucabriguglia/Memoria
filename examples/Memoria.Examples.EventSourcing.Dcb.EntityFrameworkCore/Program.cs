using Memoria;
using Memoria.EventSourcing;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Dcb.Extensions;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore;
using Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore.Extensions;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Commands;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Data;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Domain;
using Memoria.Examples.EventSourcing.Dcb.EntityFrameworkCore.Events;
using Memoria.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// A school where a student may take at most ten courses and a course has a fixed capacity.
//
// Subscribing checks both at once, which is the thing a stream cannot do: a stream per course cannot
// see the student's other subscriptions, a stream per student cannot see how full the course is, and
// one stream for the school serialises every subscription in it. Under DCB the boundary is the query
// "course:c1 OR student:s7", so two subscriptions contend only when they share a course or a student.

var serviceProvider = ConfigureServices();
await CreateDatabase(serviceProvider);

var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
var dcb = serviceProvider.GetRequiredService<IDcbDomainService>();

await Seed(dcb);

Console.WriteLine("--- a subscription that should succeed ---");
await Subscribe("alice", "maths");

Console.WriteLine();
Console.WriteLine("--- the same student, twice ---");
await Subscribe("alice", "maths");

Console.WriteLine();
Console.WriteLine("--- a course with one seat, taken ---");
await Subscribe("bob", "latin");
await Subscribe("carol", "latin");

Console.WriteLine();
Console.WriteLine("--- two decisions that do not contend ---");
Console.WriteLine("dave/maths and erin/greek share neither a course nor a student, so both commit.");
await Subscribe("dave", "maths");
await Subscribe("erin", "greek");

Console.WriteLine();
Console.WriteLine("--- a stale decision ---");
await ShowStaleDecisionRefused(dcb);

Console.WriteLine();
Console.WriteLine("--- an aggregate the store folds and snapshots ---");
Console.WriteLine("latin filled at one seat. Raising its capacity reads course:latin and nothing else,");
Console.WriteLine("so the store can build the model, fold it and keep a snapshot of it.");
await ChangeCapacity("latin", 3);
await ShowSnapshot(dcb, "latin");
await Subscribe("carol", "latin");

Console.WriteLine();
Console.WriteLine("--- two different decisions contending on one tag ---");
await ShowCapacityChangeRefusedBySubscription(dcb);

return;

async Task Subscribe(string studentId, string courseId)
{
    var result = await dispatcher.Send(new SubscribeStudentCommand(studentId, courseId));

    Console.WriteLine(result.IsSuccess
        ? $"  {studentId} -> {courseId}: subscribed"
        : $"  {studentId} -> {courseId}: refused — {result.Failure!.Description}");
}

// Reads the boundary, lets something else move it, then appends: the condition catches it.
async Task ShowStaleDecisionRefused(IDcbDomainService service)
{
    var boundary = TagQuery.AnyOf(new Tag("course", "greek"), new Tag("student", "frank"));

    var positionBefore = (await service.GetLatestPosition(boundary)).Value;
    Console.WriteLine($"  frank/greek read the boundary at position {positionBefore}.");

    // Something else subscribes to greek in the meantime, moving the boundary.
    await Subscribe("gina", "greek");

    var result = await service.SaveEvents(
        [new TaggedEvent(new StudentSubscribedEvent("frank", "greek"),
            [new Tag("course", "greek"), new Tag("student", "frank")])],
        new AppendCondition(boundary, positionBefore));

    Console.WriteLine(result.IsSuccess
        ? "  frank/greek: subscribed"
        : $"  frank/greek: refused — {result.Failure!.Type} (boundary now at " +
          $"{result.Failure.Tags!["latestPosition"]}, decision read {result.Failure.Tags["expectedPosition"]})");
}

async Task ChangeCapacity(string courseId, int capacity)
{
    var result = await dispatcher.Send(new ChangeCourseCapacityCommand(courseId, capacity));

    Console.WriteLine(result.IsSuccess
        ? $"  {courseId} capacity -> {capacity}: changed"
        : $"  {courseId} capacity -> {capacity}: refused — {result.Failure!.Description}");
}

// SnapshotOnly reads the snapshot and no events at all, so it returns something only because
// SaveAggregate wrote one alongside the event it appended.
async Task ShowSnapshot(IDcbDomainService service, string courseId)
{
    var course = (await service.GetAggregate(new CourseId(courseId), ReadMode.SnapshotOnly)).Value;

    Console.WriteLine(course is null
        ? $"  no snapshot for {courseId}"
        : $"  snapshot: {course.SeatsTaken} of {course.Capacity} seats taken, folded to position " +
          $"{course.LatestPosition}");
}

// A capacity change and a subscription are different decisions about different things, but both are
// written under course:latin — so they contend, and the one that read first is the one refused.
async Task ShowCapacityChangeRefusedBySubscription(IDcbDomainService service)
{
    var courseId = new CourseId("latin");

    var positionBefore = (await service.GetLatestPosition(courseId.Boundary)).Value;
    Console.WriteLine($"  a capacity change read course:latin at position {positionBefore}.");

    var course = (await service.GetAggregate(courseId, ReadMode.SnapshotWithNewEventsOrCreate)).Value!;
    course.ChangeCapacityTo(10);

    await Subscribe("henry", "latin");

    var result = await service.SaveAggregate(courseId, course,
        new AppendCondition(courseId.Boundary, positionBefore));

    Console.WriteLine(result.IsSuccess
        ? "  latin capacity -> 10: changed"
        : $"  latin capacity -> 10: refused — {result.Failure!.Type} (boundary now at " +
          $"{result.Failure.Tags!["latestPosition"]}, decision read {result.Failure.Tags["expectedPosition"]})");
}

async Task Seed(IDcbDomainService service)
{
    // Courses and students are appended unconditionally: nothing was read to make them, so there is
    // nothing for a condition to protect.
    TaggedEvent Course(string id, int capacity) =>
        new(new CourseDefinedEvent(id, capacity), [new Tag("course", id)]);

    TaggedEvent Student(string id, string name) =>
        new(new StudentRegisteredEvent(id, name), [new Tag("student", id)]);

    await service.SaveEvents(
    [
        Course("maths", 30), Course("latin", 1), Course("greek", 30),
        Student("alice", "Alice"), Student("bob", "Bob"), Student("carol", "Carol"),
        Student("dave", "Dave"), Student("erin", "Erin"), Student("frank", "Frank"),
        Student("gina", "Gina"), Student("henry", "Henry")
    ], condition: null);
}

IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();

    var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "School.db");
    var connectionString = $"Data Source={dbPath}";

    services.AddScoped(serviceProvider => new DbContextOptionsBuilder<DcbDbContext>()
        .UseSqlite(connectionString)
        .UseApplicationServiceProvider(serviceProvider)
        .Options);

    services.AddDbContext<SchoolDbContext>(options => options.UseSqlite(connectionString));

    services.AddMemoria(typeof(Program));
    services.AddMemoriaDcb(typeof(Program));
    services.AddMemoriaDcbEntityFrameworkCore<SchoolDbContext>();

    return services.BuildServiceProvider();
}

async Task CreateDatabase(IServiceProvider provider)
{
    using var scope = provider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();

    await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();
}
