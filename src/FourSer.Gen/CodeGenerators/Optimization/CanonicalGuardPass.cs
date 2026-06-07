using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class CanonicalGuardPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        if (!plan.Facts.Options.EnablesGuardCanonicalization)
        {
            return plan;
        }

        return plan with { Ops = RewriteGuards(plan.Ops) };
    }

    private static EquatableArray<PlanOp> RewriteGuards(EquatableArray<PlanOp> ops)
    {
        var seen = new HashSet<GuardKey>();
        var rewritten = new List<PlanOp>();

        foreach (var op in ops)
        {
            switch (op)
            {
                case GuardOp guard when seen.Add(guard.Key):
                    rewritten.Add(guard with
                    {
                        Body = RewriteGuards(guard.Body),
                        ElseBody = RewriteGuards(guard.ElseBody),
                    });
                    break;
                case GuardOp:
                    break;
                case PolymorphicSwitchOp polySwitch:
                    rewritten.Add(polySwitch with
                    {
                        Cases = polySwitch.Cases.Array
                            .Select(c => c with { Body = RewriteGuards(c.Body) })
                            .ToEquatableArray(),
                        DefaultBody = RewriteGuards(polySwitch.DefaultBody),
                    });
                    break;
                default:
                    rewritten.Add(op);
                    break;
            }
        }

        return rewritten.ToEquatableArray();
    }
}
