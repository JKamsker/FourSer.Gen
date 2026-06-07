using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class CountCachingPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        if (!plan.Facts.Options.EnablesCountCaching)
        {
            return plan;
        }

        var rewritten = PlanRewriter.Rewrite(plan.Ops, RewriteOp);
        return plan with { Ops = rewritten };
    }

    private static IEnumerable<PlanOp> RewriteOp(PlanOp op)
    {
        switch (op)
        {
            case CollectionWriteOp collectionWrite:
                yield return collectionWrite with
                {
                    CollectionPlan = ApplyCaching(collectionWrite.CollectionPlan),
                };
                yield break;
            case CollectionReadOp collectionRead:
                yield return collectionRead with
                {
                    CollectionPlan = ApplyCaching(collectionRead.CollectionPlan),
                };
                yield break;
            case CollectionValidateOp collectionValidate:
                yield return collectionValidate with
                {
                    CollectionPlan = ApplyCaching(collectionValidate.CollectionPlan),
                };
                yield break;
            case CollectionSizeOp collectionSize:
                yield return collectionSize with
                {
                    CollectionPlan = ApplyCaching(collectionSize.CollectionPlan),
                };
                yield break;
            default:
                yield return op;
                yield break;
        }
    }

    private static CollectionPlan ApplyCaching(CollectionPlan plan)
    {
        if (plan.CachedCountLocalName is not null)
        {
            return plan;
        }

        if (plan.CollectionInfo.Unlimited)
        {
            return plan;
        }

        return plan with
        {
            CachedCountLocalName = $"{plan.MemberName.ToCamelCase()}Count",
        };
    }
}
