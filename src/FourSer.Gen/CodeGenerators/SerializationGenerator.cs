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
        var plans = new List<MethodPlan>();

        var spanPlan = PlanPipeline.BuildSerializePlan(typeToGenerate, TargetKind.Span, options, capabilities);
        SpanSerializeEmitter.Emit(sb, spanPlan);
        plans.Add(spanPlan);

        if (typeToGenerate.AdditionalMethods.HasFlag(SerializerGenerationMethods.Stream))
        {
            sb.WriteLine();
            sb.WriteLine();
            var streamPlan = PlanPipeline.BuildSerializePlan(typeToGenerate, TargetKind.Stream, options, capabilities);
            StreamSerializeEmitter.Emit(sb, streamPlan);
            plans.Add(streamPlan);
        }

        if (typeToGenerate.AdditionalMethods.HasFlag(SerializerGenerationMethods.BufferWriter))
        {
            sb.WriteLine();
            sb.WriteLine();
            BufferWriterSerializeEmitter.Emit(sb, spanPlan);
        }

        if (typeToGenerate.AdditionalMethods.HasFlag(SerializerGenerationMethods.PipeWriter))
        {
            sb.WriteLine();
            sb.WriteLine();
            PipeWriterSerializeEmitter.Emit(sb, spanPlan);
        }

        return plans.ToEquatableArray();
    }
}
