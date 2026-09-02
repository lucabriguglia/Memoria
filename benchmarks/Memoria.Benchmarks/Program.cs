using BenchmarkDotNet.Running;
using Memoria.Benchmarks.Store;

if (args.Contains("--round-trips"))
{
    await RoundTripReport.Run(verbose: args.Contains("--verbose"));
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

public partial class Program;
