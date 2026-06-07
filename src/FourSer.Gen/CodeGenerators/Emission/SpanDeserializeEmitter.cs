using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class SpanDeserializeEmitter
{
    public static void Emit(IndentedStringBuilder sb, MethodPlan plan)
    {
        var type = plan.Facts.Type;
        var newKeyword = type.HasSerializableBaseType ? "new " : string.Empty;

        sb.WriteLineFormat("public static {0}{1} Deserialize(ref System.ReadOnlySpan<byte> buffer)", newKeyword, type.Name);
        using (sb.BeginBlock())
        {
            PlanOpEmitter.EmitOps(new PlanEmitterContext(sb, plan), plan.Ops);
            sb.WriteLine("return obj;");
        }

        sb.WriteLine();
        sb.WriteLineFormat("public static {0}{1} Deserialize(System.ReadOnlySpan<byte> buffer)", newKeyword, type.Name);
        using (sb.BeginBlock())
        {
            sb.WriteLine("return Deserialize(ref buffer);");
        }
    }
}
