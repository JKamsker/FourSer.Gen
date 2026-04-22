using FourSer.Gen.CodeGenerators.Optimization;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal static class PlanPipeline
{
    private static readonly IPlanPass[] s_passes =
    {
        new NormalizePlanPass(),
        new CanonicalGuardPass(),
        new CountCachingPass(),
        new TypeIdCachingPass(),
        new ConstantSizeFoldPass(),
        new BatchPrimitiveRunsPass(),
        new StringFusionPass(),
        new CollectionFastPathPass(),
        new PolymorphicOptimizationPass(),
        new CleanupPlanPass(),
        new ValidatePlanPass(),
    };

    public static MethodPlan BuildPacketSizePlan(
        TypeToGenerate type,
        FourSerGeneratorOptions options,
        TargetCapabilities capabilities)
    {
        return BuildPlan(
            type,
            MethodKind.PacketSize,
            TargetKind.None,
            SizePlanBuilder.Build(type),
            options,
            capabilities);
    }

    public static MethodPlan BuildSerializePlan(
        TypeToGenerate type,
        TargetKind targetKind,
        FourSerGeneratorOptions options,
        TargetCapabilities capabilities)
    {
        return BuildPlan(
            type,
            MethodKind.Serialize,
            targetKind,
            SerializePlanBuilder.Build(type, targetKind),
            options,
            capabilities);
    }

    public static MethodPlan BuildDeserializePlan(
        TypeToGenerate type,
        TargetKind targetKind,
        FourSerGeneratorOptions options,
        TargetCapabilities capabilities)
    {
        return BuildPlan(
            type,
            MethodKind.Deserialize,
            targetKind,
            DeserializePlanBuilder.Build(type, targetKind),
            options,
            capabilities);
    }

    private static MethodPlan BuildPlan(
        TypeToGenerate type,
        MethodKind methodKind,
        TargetKind targetKind,
        EquatableArray<PlanOp> ops,
        FourSerGeneratorOptions options,
        TargetCapabilities capabilities)
    {
        var plan = new MethodPlan(
            methodKind,
            targetKind,
            PlanFactFactory.Create(type, options, capabilities),
            ops,
            Diagnostics: Array.Empty<PlanDiagnostic>().ToEquatableArray());

        foreach (var pass in s_passes)
        {
            plan = pass.Apply(plan);
        }

        return plan;
    }
}
