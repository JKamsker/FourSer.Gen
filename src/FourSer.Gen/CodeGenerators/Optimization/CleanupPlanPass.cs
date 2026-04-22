using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class CleanupPlanPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        var diagnostics = plan.Diagnostics.Array.ToList();
        var cleanedOps = Cleanup(plan.Ops, plan.Facts.Options.EmitOptimizationComments);
        return plan with
        {
            Ops = cleanedOps,
            Diagnostics = diagnostics.ToEquatableArray(),
        };
    }

    private static EquatableArray<PlanOp> Cleanup(EquatableArray<PlanOp> ops, bool emitComments)
    {
        var cleaned = new List<PlanOp>();
        BarrierOp? previousBarrier = null;

        foreach (var op in ops)
        {
            if (op is BarrierOp barrier)
            {
                if (previousBarrier is not null && previousBarrier.Reason == barrier.Reason)
                {
                    continue;
                }

                previousBarrier = barrier;
            }
            else
            {
                previousBarrier = null;
            }

            cleaned.Add(op switch
            {
                GuardOp guard => guard with
                {
                    Body = Cleanup(guard.Body, emitComments),
                    ElseBody = Cleanup(guard.ElseBody, emitComments),
                },
                PolymorphicSwitchOp polySwitch => polySwitch with
                {
                    Cases = polySwitch.Cases.Array
                        .Select(c => c with { Body = Cleanup(c.Body, emitComments) })
                        .ToEquatableArray(),
                    DefaultBody = Cleanup(polySwitch.DefaultBody, emitComments),
                },
                _ => !emitComments && op.Comment is not null
                    ? op switch
                    {
                        DeclareLocalOp declareLocal => declareLocal with { Comment = null },
                        AssignOp assign => assign with { Comment = null },
                        GuardOp guard => guard with { Comment = null },
                        BarrierOp barrierOp => barrierOp with { Comment = null },
                        ThrowOp throwOp => throwOp with { Comment = null },
                        ScalarReadOp scalarRead => scalarRead with { Comment = null },
                        ScalarWriteOp scalarWrite => scalarWrite with { Comment = null },
                        StringReadOp stringRead => stringRead with { Comment = null },
                        StringWriteOp stringWrite => stringWrite with { Comment = null },
                        CountReadOp countRead => countRead with { Comment = null },
                        CountWriteOp countWrite => countWrite with { Comment = null },
                        SizeAddOp sizeAdd => sizeAdd with { Comment = null },
                        TypeIdResolveOp typeIdResolve => typeIdResolve with { Comment = null },
                        TypeIdMutationOp typeIdMutation => typeIdMutation with { Comment = null },
                        SerializeNestedOp serializeNested => serializeNested with { Comment = null },
                        DeserializeNestedOp deserializeNested => deserializeNested with { Comment = null },
                        CustomSerializerOp customSerializer => customSerializer with { Comment = null },
                        ConstructObjectOp constructObject => constructObject with { Comment = null },
                        FixedBytesReadOp fixedBytesRead => fixedBytesRead with { Comment = null },
                        FixedBytesWriteOp fixedBytesWrite => fixedBytesWrite with { Comment = null },
                        BatchReadOp batchRead => batchRead with { Comment = null },
                        BatchWriteOp batchWrite => batchWrite with { Comment = null },
                        CollectionReadOp collectionRead => collectionRead with { Comment = null },
                        CollectionWriteOp collectionWrite => collectionWrite with { Comment = null },
                        CollectionValidateOp collectionValidate => collectionValidate with { Comment = null },
                        PolymorphicSwitchOp polymorphicSwitch => polymorphicSwitch with { Comment = null },
                        _ => op,
                    }
                    : op,
            });
        }

        return cleaned.ToEquatableArray();
    }
}
