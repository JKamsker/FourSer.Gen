using FourSer.Tests.OptimizationTesting;
using Microsoft.CodeAnalysis;

namespace FourSer.Tests;

public class OptimizationDiagnosticTests
{
    [Fact]
    public void AggressivePortable_ShouldExplainSkippedCollectionFastPath()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Diagnostics;

        [GenerateSerializer]
        public partial class StringListPacket
        {
            [SerializeCollection]
            public List<string> Names { get; set; } = new();
        }
        """;

        var result = OptimizationTestCompiler.Generate(
            source,
            OptimizationTestCompiler.CreateOptions(OptimizationLevels.AggressivePortable));

        Assert.Contains(
            result.RunResult.Diagnostics,
            static diagnostic => diagnostic.Id == "FSGOPT001" && diagnostic.Severity == DiagnosticSeverity.Info);
    }

    [Fact]
    public void AggressiveNativeLayout_ShouldExplainFallbackSelection()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Diagnostics;

        [GenerateSerializer]
        public partial class DecimalListPacket
        {
            [SerializeCollection]
            public List<decimal> Values { get; set; } = new();
        }
        """;

        var result = OptimizationTestCompiler.Generate(
            source,
            OptimizationTestCompiler.CreateOptions(OptimizationLevels.AggressiveNativeLayout));

        Assert.Contains(
            result.RunResult.Diagnostics,
            static diagnostic => diagnostic.Id == "FSGOPT002" && diagnostic.Severity == DiagnosticSeverity.Info);
        Assert.Contains(
            result.RunResult.Diagnostics,
            static diagnostic => diagnostic.Id == "FSGOPT003" && diagnostic.Severity == DiagnosticSeverity.Info);
    }

    [Fact]
    public void Conservative_ShouldExplainSkippedTinyBatch()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Diagnostics;

        [GenerateSerializer]
        public partial class TinyBatchPacket
        {
            public int Value { get; set; }
        }
        """;

        var result = OptimizationTestCompiler.Generate(
            source,
            OptimizationTestCompiler.CreateOptions(OptimizationLevels.Conservative));

        Assert.Contains(
            result.RunResult.Diagnostics,
            static diagnostic => diagnostic.Id == "FSGOPT004" && diagnostic.Severity == DiagnosticSeverity.Info);
    }
}
