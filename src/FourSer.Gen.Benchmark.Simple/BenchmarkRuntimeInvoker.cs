using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FourSer.Gen.Benchmark.Simple;

internal sealed class BenchmarkRuntimeInvoker : IDisposable
{
    private readonly AssemblyLoadContext _loadContext;
    private readonly MethodInfo _createSample;
    private readonly MethodInfo _getPacketSize;
    private readonly MethodInfo _serializeSpan;
    private readonly MethodInfo _deserializeSpan;
    private readonly MethodInfo _serializeStream;
    private readonly MethodInfo _deserializeStream;

    public BenchmarkRuntimeInvoker(string optimizationLevel)
    {
        var compilation = BenchmarkCompilationFactory.CreateCompilation(
        [
            RuntimeBenchmarkSource,
            RuntimeBridgeSource,
        ]);
        var driver = BenchmarkCompilationFactory.CreateDriver(optimizationLevel);
        driver = driver.RunGenerators(compilation);
        var finalCompilation = compilation.AddSyntaxTrees(driver.GetRunResult().GeneratedTrees);

        using var image = new MemoryStream();
        var emitResult = finalCompilation.Emit(image);
        if (!emitResult.Success)
        {
            var errors = string.Join(Environment.NewLine, emitResult.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Runtime benchmark assembly could not be emitted.{Environment.NewLine}{errors}");
        }

        image.Position = 0;
        _loadContext = new AssemblyLoadContext($"FourSer.RuntimeBenchmark.{optimizationLevel}", isCollectible: true);
        var assembly = _loadContext.LoadFromStream(image);
        var bridgeType = assembly.GetType("FourSer.Benchmark.Runtime.PacketBridge")
            ?? throw new InvalidOperationException("Benchmark runtime bridge was not emitted.");

        _createSample = bridgeType.GetMethod("CreateSample", BindingFlags.Public | BindingFlags.Static)!;
        _getPacketSize = bridgeType.GetMethod("GetPacketSize", BindingFlags.Public | BindingFlags.Static)!;
        _serializeSpan = bridgeType.GetMethod("SerializeSpan", BindingFlags.Public | BindingFlags.Static)!;
        _deserializeSpan = bridgeType.GetMethod("DeserializeSpan", BindingFlags.Public | BindingFlags.Static)!;
        _serializeStream = bridgeType.GetMethod("SerializeStream", BindingFlags.Public | BindingFlags.Static)!;
        _deserializeStream = bridgeType.GetMethod("DeserializeStream", BindingFlags.Public | BindingFlags.Static)!;
    }

    public RuntimeMetrics Measure(int iterations)
    {
        var sample = _createSample.Invoke(null, null)!;
        var getPacketSize = Measure(iterations, () => _getPacketSize.Invoke(null, [sample]));
        var spanBytes = (byte[])_serializeSpan.Invoke(null, [sample])!;
        var spanSerialize = Measure(iterations, () => _serializeSpan.Invoke(null, [sample]));
        var spanDeserialize = Measure(iterations, () => _deserializeSpan.Invoke(null, [spanBytes]));
        var streamSerialize = Measure(iterations, () => _serializeStream.Invoke(null, [sample]));
        var streamDeserialize = Measure(iterations, () => _deserializeStream.Invoke(null, [spanBytes]));

        return new RuntimeMetrics(getPacketSize, spanSerialize, spanDeserialize, streamSerialize, streamDeserialize);
    }

    public void Dispose()
    {
        _loadContext.Unload();
    }

    private static BenchMeasurement Measure(int iterations, Action action)
    {
        action();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var startAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            action();
        }

        stopwatch.Stop();
        return new BenchMeasurement(
            stopwatch.Elapsed.TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - startAllocated);
    }

    private static readonly string RuntimeBenchmarkSource =
        """
        namespace FourSer.Benchmark.Runtime;

        [GenerateSerializer]
        public partial class NestedBenchmark
        {
            public int Code { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        [GenerateSerializer]
        public partial class BenchmarkPacket
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            [SerializeCollection]
            public List<int> Values { get; set; } = new();
            [SerializeCollection(CountSize = 4)]
            public byte[] Signature { get; set; } = new byte[4];
            public NestedBenchmark Nested { get; set; } = new();

            public static BenchmarkPacket CreateSample() => new()
            {
                Id = 42,
                Name = "benchmark-packet",
                Values = [1, 2, 3, 4, 5, 6, 7, 8],
                Signature = [9, 10, 11, 12],
                Nested = new NestedBenchmark { Code = 77, Name = "inner" },
            };
        }
        """;

    private static readonly string RuntimeBridgeSource =
        """
        namespace FourSer.Benchmark.Runtime;

        public static class PacketBridge
        {
            public static BenchmarkPacket CreateSample() => BenchmarkPacket.CreateSample();
            public static int GetPacketSize(BenchmarkPacket value) => BenchmarkPacket.GetPacketSize(value);
            public static byte[] SerializeSpan(BenchmarkPacket value)
            {
                var buffer = new byte[BenchmarkPacket.GetPacketSize(value)];
                BenchmarkPacket.Serialize(value, buffer);
                return buffer;
            }
            public static BenchmarkPacket DeserializeSpan(byte[] data) => BenchmarkPacket.Deserialize(data);
            public static byte[] SerializeStream(BenchmarkPacket value)
            {
                using var stream = new MemoryStream();
                BenchmarkPacket.Serialize(value, stream);
                return stream.ToArray();
            }
            public static BenchmarkPacket DeserializeStream(byte[] data)
            {
                using var stream = new MemoryStream(data, writable: false);
                return BenchmarkPacket.Deserialize(stream);
            }
        }
        """;
}

internal readonly record struct BenchMeasurement(double ElapsedMilliseconds, long AllocatedBytes);

internal readonly record struct RuntimeMetrics(
    BenchMeasurement GetPacketSize,
    BenchMeasurement SpanSerialize,
    BenchMeasurement SpanDeserialize,
    BenchMeasurement StreamSerialize,
    BenchMeasurement StreamDeserialize);
