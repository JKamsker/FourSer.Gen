using FourSer.Gen.CodeGenerators.Planning;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class ValidatePlanPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        Validate(plan.Ops);
        ValidateConstruction(plan.Facts.ConstructionPlan);
        return plan;
    }

    private static void Validate(IEnumerable<PlanOp> ops)
    {
        foreach (var op in ops)
        {
            switch (op)
            {
                case CollectionReadOp collectionRead when collectionRead.CollectionPlan.BulkLayoutMode == BulkLayoutMode.NativeLayout
                                                      && !collectionRead.CollectionPlan.UsePortableFallback:
                    throw new InvalidOperationException($"Native-layout read for '{collectionRead.CollectionPlan.MemberName}' is missing a portable fallback.");

                case CollectionWriteOp collectionWrite when collectionWrite.CollectionPlan.BulkLayoutMode == BulkLayoutMode.NativeLayout
                                                        && !collectionWrite.CollectionPlan.UsePortableFallback:
                    throw new InvalidOperationException($"Native-layout write for '{collectionWrite.CollectionPlan.MemberName}' is missing a portable fallback.");

                case GuardOp guard:
                    Validate(guard.Body);
                    Validate(guard.ElseBody);
                    break;

                case PolymorphicSwitchOp polySwitch:
                    foreach (var @case in polySwitch.Cases)
                    {
                        Validate(@case.Body);
                    }

                    Validate(polySwitch.DefaultBody);
                    break;
            }
        }
    }

    private static void ValidateConstruction(ConstructionPlan plan)
    {
        var duplicates = plan.PostConstructionAssignments.Array
            .GroupBy(static assignment => assignment.MemberName, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException($"Construction plan contains duplicate assignments: {string.Join(", ", duplicates)}.");
        }
    }
}
