using Microsoft.CodeAnalysis;

namespace FourSer.Gen;

internal enum OptimizationDiagnosticKind
{
    FastPathSkipped,
    FallbackChosen,
    NativeLayoutSkipped,
    BatchingOrFusionSkipped,
}

internal static class OptimizationDiagnostics
{
    private const string Category = "FourSer.Gen.Optimization";

    public static readonly DiagnosticDescriptor FastPathSkipped = new(
        id: "FSGOPT001",
        title: "FourSer optimizer skipped a fast path",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FallbackChosen = new(
        id: "FSGOPT002",
        title: "FourSer optimizer selected a fallback path",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NativeLayoutSkipped = new(
        id: "FSGOPT003",
        title: "FourSer optimizer skipped the native-layout path",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor BatchingOrFusionSkipped = new(
        id: "FSGOPT004",
        title: "FourSer optimizer skipped batching or string fusion",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor GetDescriptor(OptimizationDiagnosticKind kind)
    {
        return kind switch
        {
            OptimizationDiagnosticKind.FastPathSkipped => FastPathSkipped,
            OptimizationDiagnosticKind.FallbackChosen => FallbackChosen,
            OptimizationDiagnosticKind.NativeLayoutSkipped => NativeLayoutSkipped,
            OptimizationDiagnosticKind.BatchingOrFusionSkipped => BatchingOrFusionSkipped,
            _ => FastPathSkipped,
        };
    }
}
