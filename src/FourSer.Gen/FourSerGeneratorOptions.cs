using FourSer.Gen.Models;

namespace FourSer.Gen;

internal enum FourSerOptimizationLevel
{
    Off = 0,
    Conservative = 1,
    AggressivePortable = 2,
    AggressiveNativeLayout = 3,
}

internal readonly record struct FourSerGeneratorOptions(
    FourSerOptimizationLevel OptimizationLevel,
    int MinBatchBytes,
    int StackallocThreshold,
    int MaxBatchBytes,
    bool EmitOptimizationComments,
    SerializerGenerationMethods AdditionalMethods)
{
    public static FourSerGeneratorOptions Default =>
        new(
            FourSerOptimizationLevel.AggressivePortable,
            MinBatchBytes: 8,
            StackallocThreshold: 256,
            MaxBatchBytes: 8192,
            EmitOptimizationComments: false,
            AdditionalMethods: SerializerGenerationMethods.None);

    public bool EnablesGuardCanonicalization =>
        OptimizationLevel >= FourSerOptimizationLevel.Conservative;

    public bool EnablesCountCaching =>
        OptimizationLevel >= FourSerOptimizationLevel.Conservative;

    public bool EnablesTypeIdCaching =>
        OptimizationLevel >= FourSerOptimizationLevel.Conservative;

    public bool EnablesConstantSizeFolding =>
        OptimizationLevel >= FourSerOptimizationLevel.Conservative;

    public bool EnablesBatching =>
        OptimizationLevel >= FourSerOptimizationLevel.Conservative;

    public bool EnablesStringFusion =>
        OptimizationLevel >= FourSerOptimizationLevel.AggressivePortable;

    public bool EnablesCollectionFastPaths =>
        OptimizationLevel >= FourSerOptimizationLevel.AggressivePortable;

    public bool EnablesPolymorphicOptimization =>
        OptimizationLevel >= FourSerOptimizationLevel.AggressivePortable;

    public bool EnablesNativeLayout =>
        OptimizationLevel == FourSerOptimizationLevel.AggressiveNativeLayout;
}
