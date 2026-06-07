using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class ConstantSizeFoldPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        if (plan.MethodKind != MethodKind.PacketSize || !plan.Facts.Options.EnablesConstantSizeFolding)
        {
            return plan;
        }

        return plan with { Ops = Fold(plan.Ops) };
    }

    private static EquatableArray<PlanOp> Fold(EquatableArray<PlanOp> ops)
    {
        var rewritten = new List<PlanOp>();
        var constantAccumulator = 0;

        void FlushConstantAccumulator()
        {
            if (constantAccumulator == 0)
            {
                return;
            }

            rewritten.Add(new SizeAddOp(
                Key: "__constant",
                Expression: constantAccumulator.ToString(),
                ConstantValue: constantAccumulator));
            constantAccumulator = 0;
        }

        foreach (var op in ops)
        {
            switch (op)
            {
                case SizeAddOp sizeAdd when sizeAdd.ConstantValue is { } constantValue && CanFold(sizeAdd):
                    constantAccumulator += constantValue;
                    break;
                case SizeAddOp sizeAdd:
                    FlushConstantAccumulator();
                    rewritten.Add(sizeAdd);
                    break;
                case GuardOp guard:
                    FlushConstantAccumulator();
                    rewritten.Add(guard with
                    {
                        Body = Fold(guard.Body),
                        ElseBody = Fold(guard.ElseBody),
                    });
                    break;
                case PolymorphicSwitchOp polySwitch:
                    FlushConstantAccumulator();
                    rewritten.Add(polySwitch with
                    {
                        Cases = polySwitch.Cases.Array
                            .Select(c => c with { Body = Fold(c.Body) })
                            .ToEquatableArray(),
                        DefaultBody = Fold(polySwitch.DefaultBody),
                    });
                    break;
                default:
                    FlushConstantAccumulator();
                    rewritten.Add(op);
                    break;
            }
        }

        FlushConstantAccumulator();
        return rewritten.ToEquatableArray();
    }

    private static bool CanFold(SizeAddOp op)
    {
        return string.IsNullOrEmpty(op.InlineComment)
            && !op.Expression.Contains("sizeof(", StringComparison.Ordinal);
    }
}
