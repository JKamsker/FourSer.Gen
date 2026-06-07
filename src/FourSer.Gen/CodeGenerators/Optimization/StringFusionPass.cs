using FourSer.Gen.CodeGenerators.Planning;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class StringFusionPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        if (plan.MethodKind != MethodKind.Serialize || plan.TargetKind != TargetKind.Stream)
        {
            return plan;
        }

        if (!plan.Facts.Options.EnablesStringFusion)
        {
            return plan;
        }

        return plan with
        {
            Ops = PlanRewriter.Rewrite(plan.Ops, RewriteOp),
        };
    }

    private static IEnumerable<PlanOp> RewriteOp(PlanOp op)
    {
        if (op is StringWriteOp stringWrite)
        {
            yield return stringWrite with { UseFusedStreamWrite = true };
            yield break;
        }

        yield return op;
    }
}
