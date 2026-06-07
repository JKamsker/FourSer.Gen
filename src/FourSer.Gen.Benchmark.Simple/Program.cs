namespace FourSer.Gen.Benchmark.Simple;

internal static class Program
{
    private static void Main(string[] args)
    {
        var options = BenchmarkOptions.Parse(args);
        var benchmark = new GeneratorBenchmark();
        benchmark.Run(options);
    }
}
