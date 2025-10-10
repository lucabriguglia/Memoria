using Memoria.Commands;

namespace OpenCqrs.Tests.Models.Commands;

public class TestCommandSequence : CommandSequence<string>
{
    public TestCommandSequence()
    {
        AddCommand(new FirstCommandInSequence(Name: "Test Name"));
        AddCommand(new SecondCommandInSequence());
        AddCommand(new ThirdCommandInSequence());
    }
}
