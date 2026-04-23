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
        var rewritten = RewriteSequence(
            plan.Ops,
            seen,
            enableCaching,
            new HashSet<string>(StringComparer.Ordinal));
        return plan with { Ops = rewritten };
    }

    private static EquatableArray<PlanOp> RewriteSequence(
        EquatableArray<PlanOp> ops,
        Dictionary<string, string>? seen,
        bool enableCaching,
        HashSet<string> declaredLocals)
    {
        var rewritten = new List<PlanOp>();
        foreach (var op in ops)
        {
            switch (op)
            {
                case DeclareLocalOp declareLocal:
                    rewritten.Add(declareLocal);
                    declaredLocals.Add(declareLocal.Name);
                    break;
                case TypeIdResolveOp resolve:
                {
                    var targetAlreadyDeclared = declaredLocals.Contains(resolve.TargetLocalName);
                    if (!enableCaching)
                    {
                        if (!targetAlreadyDeclared)
                        {
                            rewritten.Add(new DeclareLocalOp(resolve.TypeIdTypeName, resolve.TargetLocalName, resolve.FallbackExpression));
                            declaredLocals.Add(resolve.TargetLocalName);
                        }

                        rewritten.Add(resolve);
                        break;
                    }

                    var key = resolve.Key ?? resolve.InstanceExpression;
                    if (seen is null || !seen.TryGetValue(key, out var existingLocal))
                    {
                        seen ??= new Dictionary<string, string>(StringComparer.Ordinal);
                        seen[key] = resolve.TargetLocalName;

                        if (!targetAlreadyDeclared)
                        {
                            rewritten.Add(new DeclareLocalOp(resolve.TypeIdTypeName, resolve.TargetLocalName, resolve.FallbackExpression));
                            declaredLocals.Add(resolve.TargetLocalName);
                        }

                        rewritten.Add(resolve);
                        break;
                    }

                    if (!string.Equals(existingLocal, resolve.TargetLocalName, StringComparison.Ordinal)
                        && !targetAlreadyDeclared)
                    {
                        rewritten.Add(new DeclareLocalOp(resolve.TypeIdTypeName, resolve.TargetLocalName, existingLocal));
                        declaredLocals.Add(resolve.TargetLocalName);
                    }

                    break;
                }
                case GuardOp guard:
                    rewritten.Add(guard with
                    {
                        Body = RewriteSequence(
                            guard.Body,
                            CloneSeen(seen),
                            enableCaching,
                            new HashSet<string>(declaredLocals, StringComparer.Ordinal)),
                        ElseBody = RewriteSequence(
                            guard.ElseBody,
                            CloneSeen(seen),
                            enableCaching,
                            new HashSet<string>(declaredLocals, StringComparer.Ordinal)),
                    });
                    break;
                case PolymorphicSwitchOp polySwitch:
                    rewritten.Add(polySwitch with
                    {
                        Cases = polySwitch.Cases.Array
                            .Select(c => c with
                            {
                                Body = RewriteSequence(
                                    c.Body,
                                    CloneSeen(seen),
                                    enableCaching,
                                    new HashSet<string>(declaredLocals, StringComparer.Ordinal)),
                            })
                            .ToEquatableArray(),
                        DefaultBody = RewriteSequence(
                            polySwitch.DefaultBody,
                            CloneSeen(seen),
                            enableCaching,
                            new HashSet<string>(declaredLocals, StringComparer.Ordinal)),
                    });
                    break;
                default:
                    rewritten.Add(op);
                    break;
            }
        }

        return rewritten.ToEquatableArray();
    }

    private static Dictionary<string, string>? CloneSeen(Dictionary<string, string>? seen)
    {
        return seen is null
            ? null
            : new Dictionary<string, string>(seen, StringComparer.Ordinal);
    }
}
