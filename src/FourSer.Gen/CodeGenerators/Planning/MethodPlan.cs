using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal sealed record MethodPlan(
    MethodKind MethodKind,
    TargetKind TargetKind,
    PlanFacts Facts,
    EquatableArray<PlanOp> Ops,
    EquatableArray<PlanDiagnostic> Diagnostics)
{
    public MethodPlan WithOps(IEnumerable<PlanOp> ops)
    {
        return this with { Ops = ops.ToEquatableArray() };
    }

    public MethodPlan WithDiagnostics(IEnumerable<PlanDiagnostic> diagnostics)
    {
        return this with { Diagnostics = diagnostics.ToEquatableArray() };
    }
}
