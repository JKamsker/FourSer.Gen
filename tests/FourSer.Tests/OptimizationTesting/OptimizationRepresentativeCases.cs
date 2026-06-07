namespace FourSer.Tests.OptimizationTesting;

internal static class OptimizationRepresentativeCases
{
    public static IReadOnlyList<OptimizationRepresentativeCase> All { get; } = CreateAllCases();

    private static IReadOnlyList<OptimizationRepresentativeCase> CreateAllCases()
    {
        return CoreOptimizationCases.GetCases()
            .Concat(CollectionOptimizationCases.GetCases())
            .Concat(PolymorphicOptimizationCases.GetCases())
            .ToArray();
    }
}
