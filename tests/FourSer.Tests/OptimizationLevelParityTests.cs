using FourSer.Tests.OptimizationTesting;

namespace FourSer.Tests;

public class OptimizationLevelParityTests
{
    public static IEnumerable<object[]> GetRepresentativeCases()
    {
        return OptimizationRepresentativeCases.All.Select(static testCase => new object[] { testCase });
    }

    [Theory]
    [MemberData(nameof(GetRepresentativeCases))]
    public void RepresentativePackets_ShouldPreserveBytesAcrossOptimizationLevels(OptimizationRepresentativeCase testCase)
    {
        var serializedByLevel = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var optimizationLevel in OptimizationLevels.All)
        {
            using var assembly = OptimizationTestCompiler.BuildRuntimeAssembly(testCase, optimizationLevel);

            var spanSample = assembly.CreateSample();
            var streamSample = assembly.CreateSample();
            var spanRoundTripSample = assembly.CreateSample();
            var streamRoundTripSample = assembly.CreateSample();

            try
            {
                var spanBytes = assembly.SerializeSpan(spanSample);
                var streamBytes = assembly.SerializeStream(streamSample);

                Assert.Equal(spanBytes, streamBytes);
                Assert.Equal(spanBytes, assembly.RoundTripSpan(spanRoundTripSample));
                Assert.Equal(spanBytes, assembly.RoundTripStream(streamRoundTripSample));

                serializedByLevel.Add(optimizationLevel, spanBytes);
            }
            finally
            {
                DisposeIfNeeded(spanSample);
                DisposeIfNeeded(streamSample);
                DisposeIfNeeded(spanRoundTripSample);
                DisposeIfNeeded(streamRoundTripSample);
            }
        }

        var baseline = serializedByLevel[OptimizationLevels.Off];
        foreach (var optimizationLevel in OptimizationLevels.All.Where(static level => level != OptimizationLevels.Off))
        {
            Assert.Equal(baseline, serializedByLevel[optimizationLevel]);
        }
    }

    private static void DisposeIfNeeded(object? value)
    {
        if (value is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
