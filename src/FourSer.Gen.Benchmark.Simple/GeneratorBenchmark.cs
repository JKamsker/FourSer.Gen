using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FourSer.Gen.Benchmark.Simple;

internal sealed class GeneratorBenchmark
{
    private readonly BenchmarkCaseLoader _caseLoader = new();

    public void Run(BenchmarkOptions options)
    {
        foreach (var optimizationLevel in options.Levels)
        {
            Console.WriteLine($"Level: {optimizationLevel}");
            Console.WriteLine("Generation");
            foreach (var caseName in options.Cases)
            {
                var report = MeasureGeneration(caseName, optimizationLevel, options.GenerationIterations);
                WriteGenerationReport(report);
            }

            Console.WriteLine("Runtime");
            using var runtimeInvoker = new BenchmarkRuntimeInvoker(optimizationLevel);
            WriteRuntimeReport(runtimeInvoker.Measure(options.RuntimeIterations));
            Console.WriteLine();
        }
    }

    private GenerationReport MeasureGeneration(string caseName, string optimizationLevel, int iterations)
    {
        var source = _caseLoader.Load(caseName);
        var compilation = BenchmarkCompilationFactory.CreateCompilation([source]);
        var driver = BenchmarkCompilationFactory.CreateDriver(optimizationLevel);

        driver = driver.RunGenerators(compilation);
        var warmResult = driver.GetRunResult();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var startAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        GeneratorDriverRunResult finalResult = warmResult;
        for (var i = 0; i < iterations; i++)
        {
            driver = BenchmarkCompilationFactory.CreateDriver(optimizationLevel);
            driver = driver.RunGenerators(compilation);
            finalResult = driver.GetRunResult();
        }

        stopwatch.Stop();
        var generatedSources = finalResult.Results.Single().GeneratedSources;
        var stepTimings = finalResult.Results.Single().TrackedSteps
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.Sum(static step => step.ElapsedTime.TotalMilliseconds),
                StringComparer.Ordinal);

        return new GenerationReport(
            caseName,
            new BenchMeasurement(stopwatch.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - startAllocated),
            generatedSources.Length,
            generatedSources.Sum(static source => source.SourceText.Length),
            stepTimings);
    }

    private static void WriteGenerationReport(GenerationReport report)
    {
        Console.WriteLine($"  Case: {report.CaseName}");
        Console.WriteLine($"    RunMs: {report.Measurement.ElapsedMilliseconds:F2}");
        Console.WriteLine($"    AllocBytes: {report.Measurement.AllocatedBytes}");
        Console.WriteLine($"    GeneratedSources: {report.GeneratedSourceCount}");
        Console.WriteLine($"    GeneratedSourceChars: {report.GeneratedSourceCharacters}");
        Console.WriteLine("    Steps:");
        foreach (var step in report.StepTimings.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"      {step.Key}: {step.Value:F2} ms");
        }
    }

    private static void WriteRuntimeReport(RuntimeMetrics metrics)
    {
        WriteRuntimeMetric("GetPacketSize", metrics.GetPacketSize);
        WriteRuntimeMetric("SpanSerialize", metrics.SpanSerialize);
        WriteRuntimeMetric("SpanDeserialize", metrics.SpanDeserialize);
        WriteRuntimeMetric("StreamSerialize", metrics.StreamSerialize);
        WriteRuntimeMetric("StreamDeserialize", metrics.StreamDeserialize);
    }

    private static void WriteRuntimeMetric(string name, BenchMeasurement measurement)
    {
        Console.WriteLine($"  {name}: {measurement.ElapsedMilliseconds:F2} ms, {measurement.AllocatedBytes} bytes");
    }
}

internal sealed record GenerationReport(
    string CaseName,
    BenchMeasurement Measurement,
    int GeneratedSourceCount,
    int GeneratedSourceCharacters,
    IReadOnlyDictionary<string, double> StepTimings);
