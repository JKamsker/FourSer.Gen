using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class StreamSerializeEmitter
{
    public static void Emit(IndentedStringBuilder sb, MethodPlan plan)
    {
        var type = plan.Facts.Type;
        sb.WriteLineFormat("public static void Serialize({0} obj, System.IO.Stream stream)", type.Name);
        using (sb.BeginBlock())
        {
            if (!type.IsValueType)
            {
                sb.WriteLine("if (obj is null)");
                using (sb.BeginBlock())
                {
                    sb.WriteLine("throw new System.ArgumentNullException(nameof(obj));");
                }
            }

            PlanOpEmitter.EmitOps(new PlanEmitterContext(sb, plan), plan.Ops);
        }
    }
}
