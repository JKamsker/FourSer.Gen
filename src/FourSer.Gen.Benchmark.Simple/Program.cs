using System.Diagnostics;

namespace FourSer.Gen.Benchmark.Simple;

class Program
{
    static void Main(string[] args)
    {
        var bm = new GeneratorBenchmark();
        // var tc = bm.GetTestCases().FirstOrDefault();

        var cases = bm.GetTestCases().ToArray();

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
        Console.WriteLine($"Total time for 'TypesWithGenerateSerializerAttribute' step: {totalElapsedTime.TotalMilliseconds} ms");
    }
}
