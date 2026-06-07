using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Gen;

internal static class FourSerGeneratorOptionsProvider
{
    private const string OptimizationLevelKey = "build_property.FourSerOptimizationLevel";
    private const string MinBatchBytesKey = "build_property.FourSerMinBatchBytes";
    private const string StackallocThresholdKey = "build_property.FourSerStackallocThreshold";
    private const string MaxBatchBytesKey = "build_property.FourSerMaxBatchBytes";
    private const string EmitOptimizationCommentsKey = "build_property.FourSerEmitOptimizationComments";

    public static FourSerGeneratorOptions GetOptions(AnalyzerConfigOptionsProvider provider)
    {
        return GetOptions(provider.GlobalOptions);
    }

    public static FourSerGeneratorOptions GetOptions(AnalyzerConfigOptions options)
    {
        var defaults = FourSerGeneratorOptions.Default;

        var optimizationLevel = TryGetEnum(options, OptimizationLevelKey, defaults.OptimizationLevel);
        var minBatchBytes = TryGetPositiveInt(options, MinBatchBytesKey, defaults.MinBatchBytes);
        var stackallocThreshold = TryGetPositiveInt(options, StackallocThresholdKey, defaults.StackallocThreshold);
        var maxBatchBytes = TryGetPositiveInt(options, MaxBatchBytesKey, defaults.MaxBatchBytes);
        var emitOptimizationComments = TryGetBool(options, EmitOptimizationCommentsKey, defaults.EmitOptimizationComments);

        if (maxBatchBytes < minBatchBytes)
        {
            maxBatchBytes = minBatchBytes;
        }

        if (stackallocThreshold > maxBatchBytes)
        {
            stackallocThreshold = maxBatchBytes;
        }

        return new FourSerGeneratorOptions(
            optimizationLevel,
            minBatchBytes,
            stackallocThreshold,
            maxBatchBytes,
            emitOptimizationComments);
    }

    private static bool TryGetBool(AnalyzerConfigOptions options, string key, bool fallback)
    {
        if (!options.TryGetValue(key, out var rawValue))
        {
            return fallback;
        }

        return bool.TryParse(rawValue, out var parsed)
            ? parsed
            : fallback;
    }

    private static int TryGetPositiveInt(AnalyzerConfigOptions options, string key, int fallback)
    {
        if (!options.TryGetValue(key, out var rawValue))
        {
            return fallback;
        }

        if (!int.TryParse(rawValue, out var parsed))
        {
            return fallback;
        }

        return parsed > 0
            ? parsed
            : fallback;
    }

    private static TEnum TryGetEnum<TEnum>(AnalyzerConfigOptions options, string key, TEnum fallback)
        where TEnum : struct
    {
        if (!options.TryGetValue(key, out var rawValue))
        {
            return fallback;
        }

        return Enum.TryParse<TEnum>(rawValue, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }
}
