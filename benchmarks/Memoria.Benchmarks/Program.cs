using BenchmarkDotNet.Running;

using Memoria.Benchmarks.Store;



if (args.Contains("--round-trips"))
{
    await RoundTripReport.Run(
        verbose: args.Contains("--verbose"),
        engine: args.Contains("--sqlserver") ? StoreEngine.SqlServer
            : args.Contains("--postgres") ? StoreEngine.PostgreSql
            : StoreEngine.Sqlite);
    return;
}

if (args.Contains("--spike-check"))
{
    await AppendSpike.Check(SpikeEngine(args), args.Contains("--rcsi"));
    return;
}

if (args.Contains("--contention"))
{
    await ContentionSpike.Run(SpikeEngine(args), args.FirstOrDefault(arg => arg.StartsWith("--only="))?["--only=".Length..], args.Contains("--rcsi"));
    return;
}

static StoreEngine SpikeEngine(string[] args) =>
    args.Contains("--sqlserver") ? StoreEngine.SqlServer : StoreEngine.PostgreSql;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

public partial class Program;
