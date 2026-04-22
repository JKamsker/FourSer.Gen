using FourSer.Gen.CodeGenerators.Planning;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class NormalizePlanPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        var facts = PlanFactFactory.Create(
            plan.Facts.Type,
            plan.Facts.Options,
            plan.Facts.Capabilities);

        return plan with { Facts = facts };
    }
}
