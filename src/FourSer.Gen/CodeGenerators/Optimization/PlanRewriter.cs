using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal static class PlanRewriter
{
    public static EquatableArray<PlanOp> Rewrite(
        EquatableArray<PlanOp> ops,
        Func<PlanOp, IEnumerable<PlanOp>> rewriter)
    {
        return RewriteCore(ops, rewriter).ToEquatableArray();
    }

    private static IEnumerable<PlanOp> RewriteCore(
        IEnumerable<PlanOp> ops,
        Func<PlanOp, IEnumerable<PlanOp>> rewriter)
    {
        foreach (var op in ops)
        {
            var rewrittenChildren = RewriteChildren(op, rewriter);
            foreach (var rewritten in rewriter(rewrittenChildren))
            {
                yield return rewritten;
            }
        }
    }

    private static PlanOp RewriteChildren(
        PlanOp op,
        Func<PlanOp, IEnumerable<PlanOp>> rewriter)
    {
        return op switch
        {
            GuardOp guard => guard with
            {
                Body = Rewrite(guard.Body, rewriter),
                ElseBody = Rewrite(guard.ElseBody, rewriter),
            },
            PolymorphicSwitchOp polySwitch => polySwitch with
            {
                Cases = polySwitch.Cases.Array
                    .Select(static c => c)
                    .Select(c => c with { Body = Rewrite(c.Body, rewriter) })
                    .ToEquatableArray(),
                DefaultBody = Rewrite(polySwitch.DefaultBody, rewriter),
            },
            _ => op,
        };
    }
}
