using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class TypeIdCachingPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        if (!plan.Facts.Options.EnablesTypeIdCaching)
        {
            return plan;
        }

        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        var rewritten = RewriteSequence(plan.Ops, seen);
        return plan with { Ops = rewritten };
    }

    private static EquatableArray<PlanOp> RewriteSequence(
        EquatableArray<PlanOp> ops,
        Dictionary<string, string> seen)
    {
        var rewritten = new List<PlanOp>();
        foreach (var op in ops)
        {
            switch (op)
            {
                case TypeIdResolveOp resolve:
                {
                    var key = resolve.Key ?? resolve.InstanceExpression;
                    if (!seen.TryGetValue(key, out var existingLocal))
                    {
                        seen[key] = resolve.TargetLocalName;
                        rewritten.Add(new DeclareLocalOp(resolve.TypeIdTypeName, resolve.TargetLocalName, resolve.FallbackExpression));
                        rewritten.Add(resolve);
                        break;
                    }

                    if (!string.Equals(existingLocal, resolve.TargetLocalName, StringComparison.Ordinal))
                    {
                        rewritten.Add(new DeclareLocalOp(resolve.TypeIdTypeName, resolve.TargetLocalName, existingLocal));
                    }

                    break;
                }
                case GuardOp guard:
                    rewritten.Add(guard with
                    {
                        Body = RewriteSequence(guard.Body, seen),
                        ElseBody = RewriteSequence(guard.ElseBody, seen),
                    });
                    break;
                case PolymorphicSwitchOp polySwitch:
                    rewritten.Add(polySwitch with
                    {
                        Cases = polySwitch.Cases.Array
                            .Select(c => c with { Body = RewriteSequence(c.Body, seen) })
                            .ToEquatableArray(),
                        DefaultBody = RewriteSequence(polySwitch.DefaultBody, seen),
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
