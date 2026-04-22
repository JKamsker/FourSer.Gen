using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class PolymorphicOptimizationPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        if (!plan.Facts.Options.EnablesPolymorphicOptimization)
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
        switch (op)
        {
            case TypeIdResolveOp typeIdResolve:
                yield return typeIdResolve with
                {
                    Key = typeIdResolve.Key ?? typeIdResolve.InstanceExpression,
                };
                yield break;
            case PolymorphicSwitchOp polymorphicSwitch:
                var discriminatorLocalName = polymorphicSwitch.PolymorphicPlan.CachedDiscriminatorLocalName
                    ?? $"{polymorphicSwitch.PolymorphicPlan.MemberName.ToCamelCase()}Discriminator";
                yield return polymorphicSwitch with
                {
                    PolymorphicPlan = polymorphicSwitch.PolymorphicPlan with
                    {
                        CachedDiscriminatorLocalName = discriminatorLocalName,
                        HoistSingleTypeCollectionSwitch = polymorphicSwitch.PolymorphicPlan.PolymorphicMode == FourSer.Gen.PolymorphicMode.SingleTypeId,
                        BatchFixedHeaders = true,
                    },
                };
                yield break;
            default:
                yield return op;
                yield break;
        }
    }
}
