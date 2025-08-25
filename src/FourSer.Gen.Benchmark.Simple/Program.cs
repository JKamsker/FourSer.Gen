using System.Diagnostics;

namespace FourSer.Gen.Benchmark.Simple;

class Program
{
    static void Main(string[] args)
    {
        var bm = new GeneratorBenchmark();
        // var tc = bm.GetTestCases().FirstOrDefault();

        var cases = bm.GetTestCases().ToArray();

        for (int i = 0; i < 10; i++)
        {
            foreach (var testCase in cases)
            {
                bm.RunGenerator(testCase);
            }
        }
            
        TimeSpan bestOf100 = TimeSpan.MaxValue;
        for (int j = 0; j < 10; j++)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                foreach (var testCase in cases)
                {
                    bm.RunGenerator(testCase);
                }
            } 
            sw.Stop();
            if (sw.Elapsed < bestOf100)
                bestOf100 = sw.Elapsed;
        }
        Console.WriteLine($"Best of 10: {bestOf100.TotalMilliseconds} ms");
    }
}
