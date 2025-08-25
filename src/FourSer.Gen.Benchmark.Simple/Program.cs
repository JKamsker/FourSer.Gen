using System.Diagnostics;

namespace FourSer.Gen.Benchmark.Simple;

// 26.08.2025: 900,5361 ms
class Program
{
    static void Main(string[] args)
    {
        var bm = new GeneratorBenchmark();
        // var tc = bm.GetTestCases().FirstOrDefault();

        var cases = bm.GetTestCases().ToArray();

        GetBestOf10(cases, bm);
        var totalElapsedTime = GetBestOf10(cases, bm);
        Console.WriteLine($"Total time for 'TypesWithGenerateSerializerAttribute' step: {totalElapsedTime.TotalMilliseconds} ms");
    }

    private static TimeSpan GetBestOf10(string[] cases, GeneratorBenchmark bm)
    {
        var totalElapsedTime = TimeSpan.Zero;
        for (int i = 0; i < 10; i++)
        {
            foreach (var testCase in cases)
            {
                var elapsedTime = bm.RunGenerator(testCase);
                if (elapsedTime.HasValue)
                {
                    totalElapsedTime += elapsedTime.Value;
                }
            }
        }

        return totalElapsedTime;
    }
}
