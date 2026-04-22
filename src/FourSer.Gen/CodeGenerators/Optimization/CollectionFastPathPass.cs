using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class CollectionFastPathPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        if (!plan.Facts.Options.EnablesCollectionFastPaths)
        {
            return plan;
        }

        var diagnostics = plan.Diagnostics.Array.ToList();
        var rewritten = PlanRewriter.Rewrite(plan.Ops, op => RewriteOp(op, plan, diagnostics));
        return plan with
        {
            Ops = rewritten,
            Diagnostics = diagnostics.ToEquatableArray(),
        };
    }

    private static IEnumerable<PlanOp> RewriteOp(
        PlanOp op,
        MethodPlan plan,
        List<PlanDiagnostic> diagnostics)
    {
        switch (op)
        {
            case CollectionWriteOp collectionWrite:
                yield return collectionWrite with
                {
                    CollectionPlan = OptimizeCollectionPlan(collectionWrite.CollectionPlan, plan, diagnostics),
                };
                yield break;
            case CollectionReadOp collectionRead:
                yield return collectionRead with
                {
                    CollectionPlan = OptimizeCollectionPlan(collectionRead.CollectionPlan, plan, diagnostics),
                };
                yield break;
            default:
                yield return op;
                yield break;
        }
    }

    private static CollectionPlan OptimizeCollectionPlan(
        CollectionPlan plan,
        MethodPlan methodPlan,
        List<PlanDiagnostic> diagnostics)
    {
        var declarationLocation = methodPlan.Facts.Type.Members
            .First(member => member.Name == plan.MemberName)
            .DeclarationLocation;
        var nativeLayoutSkipped = false;

        if (methodPlan.Facts.Options.EnablesNativeLayout && methodPlan.Facts.Capabilities.HasCollectionsMarshalAsSpan)
        {
            if (CanUseNativeLayout(plan))
            {
                return plan with
                {
                    BulkLayoutMode = BulkLayoutMode.NativeLayout,
                    UsePortableFallback = true,
                    UseListSetCount = ShouldUseSetCount(plan, methodPlan),
                };
            }

            diagnostics.Add(new PlanDiagnostic(
                OptimizationDiagnosticKind.NativeLayoutSkipped,
                $"Native-layout bulk path skipped for '{plan.MemberName}'.",
                declarationLocation));
            nativeLayoutSkipped = true;
        }

        if (CanUseByteExact(plan))
        {
            TryAddFallbackChosenDiagnostic(plan, diagnostics, declarationLocation, nativeLayoutSkipped, "byte-exact");
            return plan with
            {
                BulkLayoutMode = BulkLayoutMode.ByteExact,
                UseListSetCount = ShouldUseSetCount(plan, methodPlan),
            };
        }

        if (CanUsePortablePrimitive(plan))
        {
            TryAddFallbackChosenDiagnostic(plan, diagnostics, declarationLocation, nativeLayoutSkipped, "portable primitive");
            return plan with
            {
                BulkLayoutMode = BulkLayoutMode.PortablePrimitive,
                UseListSetCount = ShouldUseSetCount(plan, methodPlan),
            };
        }

        diagnostics.Add(new PlanDiagnostic(
            OptimizationDiagnosticKind.FastPathSkipped,
            $"Portable collection fast path skipped for '{plan.MemberName}'.",
            declarationLocation));

        return plan;
    }

    private static void TryAddFallbackChosenDiagnostic(
        CollectionPlan plan,
        List<PlanDiagnostic> diagnostics,
        LocationInfo? declarationLocation,
        bool nativeLayoutSkipped,
        string fallbackName)
    {
        if (!nativeLayoutSkipped)
        {
            return;
        }

        diagnostics.Add(new PlanDiagnostic(
            OptimizationDiagnosticKind.FallbackChosen,
            $"Optimizer chose the {fallbackName} fallback for '{plan.MemberName}' after rejecting the native-layout path.",
            declarationLocation));
    }

    private static bool CanUseByteExact(CollectionPlan plan)
    {
        return plan.CustomSerializer is null
            && TypeHelper.IsByteCollection(plan.ElementTypeName)
            && !plan.ElementHasGenerateSerializerAttribute;
    }

    private static bool CanUsePortablePrimitive(CollectionPlan plan)
    {
        return plan.CustomSerializer is null
            && plan.ElementIsUnmanagedType
            && !plan.ElementIsStringType
            && !plan.ElementHasGenerateSerializerAttribute;
    }

    private static bool CanUseNativeLayout(CollectionPlan plan)
    {
        return CanUsePortablePrimitive(plan)
            && plan.ElementTypeName is not "bool" and not "decimal";
    }

    private static bool ShouldUseSetCount(CollectionPlan plan, MethodPlan methodPlan)
    {
        return methodPlan.MethodKind == MethodKind.Deserialize
            && plan.IsList
            && !plan.IsPureEnumerable
            && methodPlan.Facts.Capabilities.HasCollectionsMarshalSetCount;
    }
}
