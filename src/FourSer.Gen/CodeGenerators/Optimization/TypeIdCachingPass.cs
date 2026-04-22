using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class TypeIdCachingPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        var enableCaching = plan.Facts.Options.EnablesTypeIdCaching;
        var seen = enableCaching
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : null;
        var rewritten = RewriteSequence(plan.Ops, seen, enableCaching);
        return plan with { Ops = rewritten };
    }

    private static EquatableArray<PlanOp> RewriteSequence(
        EquatableArray<PlanOp> ops,
        Dictionary<string, string>? seen,
        bool enableCaching)
    {
        var rewritten = new List<PlanOp>();
        foreach (var op in ops)
        {
            switch (op)
            {
                case TypeIdResolveOp resolve:
                {
                    if (!enableCaching)
                    {
                        rewritten.Add(new DeclareLocalOp(resolve.TypeIdTypeName, resolve.TargetLocalName, resolve.FallbackExpression));
                        rewritten.Add(resolve);
                        break;
                    }

                    var key = resolve.Key ?? resolve.InstanceExpression;
                    if (seen is null || !seen.TryGetValue(key, out var existingLocal))
                    {
                        seen ??= new Dictionary<string, string>(StringComparer.Ordinal);
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
                        Body = RewriteSequence(guard.Body, seen, enableCaching),
                        ElseBody = RewriteSequence(guard.ElseBody, seen, enableCaching),
                    });
                    break;
                case PolymorphicSwitchOp polySwitch:
                    rewritten.Add(polySwitch with
                    {
                        Cases = polySwitch.Cases.Array
                            .Select(c => c with { Body = RewriteSequence(c.Body, seen, enableCaching) })
                            .ToEquatableArray(),
                        DefaultBody = RewriteSequence(polySwitch.DefaultBody, seen, enableCaching),
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
