using FourSer.Gen.CodeGenerators.Emission;
using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

internal static class SerializationGenerator
{
    internal static EquatableArray<MethodPlan> GenerateSerialize(
        IndentedStringBuilder sb,
        TypeToGenerate typeToGenerate,
        FourSerGeneratorOptions options,
        TargetCapabilities capabilities)
    {
        var spanPlan = PlanPipeline.BuildSerializePlan(typeToGenerate, TargetKind.Span, options, capabilities);
        SpanSerializeEmitter.Emit(sb, spanPlan);

        sb.WriteLine();

        var streamPlan = PlanPipeline.BuildSerializePlan(typeToGenerate, TargetKind.Stream, options, capabilities);
        StreamSerializeEmitter.Emit(sb, streamPlan);

        return new[] { spanPlan, streamPlan }.ToEquatableArray();
    }
}
