using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class SequenceReaderDeserializeEmitter
{
    public static void Emit(IndentedStringBuilder sb, MethodPlan plan, string accessibility)
    {
        var type = plan.Facts.Type;
        var newKeyword = accessibility == "public" && type.HasSerializableBaseType ? "new " : string.Empty;

        sb.WriteLineFormat("{0} static {1}{2} Deserialize(ref global::System.Buffers.SequenceReader<byte> reader)", accessibility, newKeyword, type.Name);
        using (sb.BeginBlock())
        {
            PlanOpEmitter.EmitOps(new PlanEmitterContext(sb, plan), plan.Ops);
            sb.WriteLine("return obj;");
        }
    }
}
