using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class SizePlanEmitter
{
    public static void Emit(IndentedStringBuilder sb, MethodPlan plan)
    {
        var type = plan.Facts.Type;
        sb.WriteLineFormat("public static int GetPacketSize({0} obj)", type.Name);
        using (sb.BeginBlock())
        {
            if (!type.IsValueType)
            {
                sb.WriteLine("if (obj is null) return 0;");
            }

            sb.WriteLine("var size = 0;");
            PlanOpEmitter.EmitOps(new PlanEmitterContext(sb, plan), plan.Ops);
            sb.WriteLine("return size;");
        }
    }
}
