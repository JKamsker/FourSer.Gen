using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class PlannedCollectionReadEmitter
{
    public static bool TryEmit(
        PlanEmitterContext context,
        CollectionReadOp op,
        MemberToGenerate member)
    {
        if (!CanEmit(context, op.CollectionPlan, member))
        {
            return false;
        }

        EmitCollectionRead(context.Builder, member, op.CollectionPlan, op.TargetExpression, op.SourceExpression, op.HelperName);
        return true;
    }

    private static bool CanEmit(PlanEmitterContext context, CollectionPlan plan, MemberToGenerate member)
    {
        if (plan.IsMemoryOwner
            || (!plan.IsArray && !plan.IsList)
            || plan.IsPureEnumerable
            || GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            || member.CustomSerializer is not null
            || GeneratorUtilities.HasDefaultSerializerFor(context.Plan.Facts.Type, plan.ElementTypeName))
        {
            return false;
        }

        return plan.ElementIsUnmanagedType || plan.ElementIsStringType || plan.ElementHasGenerateSerializerAttribute;
    }

    private static void EmitCollectionRead(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        CollectionPlan plan,
        string targetExpression,
        string sourceExpression,
        string helperName)
    {
        var countVariableName = GetCountVariableName(member);
        var countExpression = EmitCount(builder, plan, member, sourceExpression, helperName, countVariableName);

        if (plan.IsArray)
        {
            EmitArrayRead(builder, member, plan, targetExpression, sourceExpression, helperName, countExpression);
            return;
        }

        EmitListRead(builder, member, plan, targetExpression, sourceExpression, helperName, countExpression);
    }

    private static string EmitCount(
        IndentedStringBuilder builder,
        CollectionPlan plan,
        MemberToGenerate member,
        string sourceExpression,
        string helperName,
        string countVariableName)
    {
        var refPrefix = NeedsRefSource(sourceExpression) ? "ref " : string.Empty;
        if (plan.CollectionInfo.CountSize >= 0)
        {
            return plan.CollectionInfo.CountSize.Value.ToString();
        }

        if (plan.CollectionInfo.CountSizeReferenceIndex is not null)
        {
            return (plan.CollectionInfo.CountSizeReference ?? "countRef").ToCamelCase();
        }

        var countType = plan.CollectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        var readMethod = TypeHelper.GetReadMethodName(countType);
        builder.WriteLine($"var {countVariableName} = {helperName}.{readMethod}({refPrefix}{sourceExpression});");
        return countVariableName;
    }

    private static void EmitArrayRead(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        CollectionPlan plan,
        string targetExpression,
        string sourceExpression,
        string helperName,
        string countExpression)
    {
        var (declaration, variableName, assignmentTarget) = GetTargetBinding(member, targetExpression);
        builder.WriteLine($"{declaration} = new {plan.ElementTypeName}[checked((int){countExpression})];");

        if (!TryEmitDirectRead(builder, plan, sourceExpression, helperName, variableName, countExpression))
        {
            EmitElementReadLoop(builder, plan, sourceExpression, helperName, variableName, countExpression, "array");
        }

        if (assignmentTarget is not null)
        {
            builder.WriteLine($"{assignmentTarget} = {variableName};");
        }
    }

    private static void EmitListRead(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        CollectionPlan plan,
        string targetExpression,
        string sourceExpression,
        string helperName,
        string countExpression)
    {
        var (declaration, variableName, assignmentTarget) = GetTargetBinding(member, targetExpression);
        builder.WriteLine($"{declaration} = new System.Collections.Generic.List<{plan.ElementTypeName}>(checked((int){countExpression}));");

        if (plan.UseListSetCount)
        {
            builder.WriteLine($"System.Runtime.InteropServices.CollectionsMarshal.SetCount({variableName}, checked((int){countExpression}));");
            if (!TryEmitDirectRead(builder, plan, sourceExpression, helperName, variableName, countExpression))
            {
                EmitElementReadLoop(builder, plan, sourceExpression, helperName, variableName, countExpression, "listSpan");
            }
        }
        else
        {
            EmitElementReadLoop(builder, plan, sourceExpression, helperName, variableName, countExpression, "list");
        }

        if (assignmentTarget is not null)
        {
            builder.WriteLine($"{assignmentTarget} = {variableName};");
        }
    }

    private static bool TryEmitDirectRead(
        IndentedStringBuilder builder,
        CollectionPlan plan,
        string sourceExpression,
        string helperName,
        string targetExpression,
        string countExpression)
    {
        if (plan.BulkLayoutMode == BulkLayoutMode.None)
        {
            return false;
        }

        var spanExpression = plan.IsArray
            ? $"{targetExpression}.AsSpan()"
            : $"System.Runtime.InteropServices.CollectionsMarshal.AsSpan({targetExpression})";

        if (plan.BulkLayoutMode == BulkLayoutMode.ByteExact)
        {
            EmitReadBytes(builder, sourceExpression, spanExpression);
            return true;
        }

        builder.WriteLine($"if ({GetFastPathGuard(plan)})");
        using (builder.BeginBlock())
        {
            builder.WriteLine($"var collectionBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes({spanExpression});");
            EmitReadBytes(builder, sourceExpression, "collectionBytes");
        }

        builder.WriteLine("else");
        using (builder.BeginBlock())
        {
            EmitElementReadLoop(builder, plan, sourceExpression, helperName, targetExpression, countExpression, plan.IsArray ? "array" : "listSpan");
        }

        return true;
    }

    private static void EmitElementReadLoop(
        IndentedStringBuilder builder,
        CollectionPlan plan,
        string sourceExpression,
        string helperName,
        string targetExpression,
        string countExpression,
        string mode)
    {
        var refPrefix = NeedsRefSource(sourceExpression) ? "ref " : string.Empty;
        builder.WriteLine($"for (int i = 0; i < checked((int){countExpression}); i++)");
        using (builder.BeginBlock())
        {
            var readExpression = GetElementReadExpression(plan, helperName, refPrefix, sourceExpression);
            switch (mode)
            {
                case "array":
                    builder.WriteLine($"{targetExpression}[i] = {readExpression};");
                    break;
                case "listSpan":
                    builder.WriteLine($"System.Runtime.InteropServices.CollectionsMarshal.AsSpan({targetExpression})[i] = {readExpression};");
                    break;
                default:
                    builder.WriteLine($"{targetExpression}.Add({readExpression});");
                    break;
            }
        }
    }

    private static string GetElementReadExpression(CollectionPlan plan, string helperName, string refPrefix, string sourceExpression)
    {
        if (plan.ElementHasGenerateSerializerAttribute)
        {
            if (sourceExpression == "reader")
            {
                return $"global::FourSer.Gen.Helpers.SequenceReaderHelpers.DeserializeSerializable<{TypeHelper.GetGlobalTypeName(plan.ElementTypeName)}>(ref reader)";
            }

            return $"{TypeHelper.GetGlobalTypeName(plan.ElementTypeName)}.Deserialize({refPrefix}{sourceExpression})";
        }

        if (plan.ElementIsStringType)
        {
            return $"{helperName}.ReadString({refPrefix}{sourceExpression})";
        }

        return $"{helperName}.{TypeHelper.GetReadMethodName(plan.ElementTypeName)}({refPrefix}{sourceExpression})";
    }

    private static void EmitReadBytes(IndentedStringBuilder builder, string sourceExpression, string targetExpression)
    {
        if (sourceExpression == "buffer")
        {
            builder.WriteLine($"global::FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadBytes(ref buffer, {targetExpression});");
            return;
        }

        if (sourceExpression == "reader")
        {
            builder.WriteLine($"global::FourSer.Gen.Helpers.SequenceReaderHelpers.ReadBytes(ref reader, {targetExpression});");
            return;
        }

        builder.WriteLine($"stream.ReadExactly({targetExpression});");
    }

    private static bool NeedsRefSource(string sourceExpression)
    {
        return sourceExpression is "buffer" or "reader";
    }

    private static string GetFastPathGuard(CollectionPlan plan)
    {
        if (plan.ElementHasGenerateSerializerAttribute && plan.ElementBulkLayoutSafe && plan.ElementFixedSizeBytes is { } fixedSizeBytes)
        {
            return $"global::System.BitConverter.IsLittleEndian && !global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{plan.ElementTypeName}>() && global::System.Runtime.CompilerServices.Unsafe.SizeOf<{plan.ElementTypeName}>() == {fixedSizeBytes}";
        }

        if (plan.BulkLayoutMode == BulkLayoutMode.NativeLayout)
        {
            return $"global::System.BitConverter.IsLittleEndian && !global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{plan.ElementTypeName}>()";
        }

        return "global::System.BitConverter.IsLittleEndian";
    }

    private static string GetCountVariableName(MemberToGenerate member)
    {
        return $"{member.Name.ToCamelCase()}Count";
    }

    private static (string Declaration, string VariableName, string? AssignmentTarget) GetTargetBinding(
        MemberToGenerate member,
        string targetExpression)
    {
        if (targetExpression.StartsWith("var ", StringComparison.Ordinal))
        {
            var declaredVariableName = targetExpression.Substring(4);
            return (targetExpression, declaredVariableName, null);
        }

        var assignedVariableName = $"{member.Name.ToCamelCase()}Value";
        return ($"var {assignedVariableName}", assignedVariableName, targetExpression);
    }
}
