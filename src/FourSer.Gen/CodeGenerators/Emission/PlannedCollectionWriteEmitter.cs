using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Helpers;
using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class PlannedCollectionWriteEmitter
{
    public static bool TryEmit(
        PlanEmitterContext context,
        CollectionWriteOp op,
        MemberToGenerate member)
    {
        if (!CanEmit(context, op.CollectionPlan, member))
        {
            return false;
        }

        var writerContext = new SerializationWriterEmitter.WriterCtx(
            op.TargetExpression,
            op.HelperName,
            op.TargetKind == TargetKind.Span);
        EmitCollectionWrite(context.Builder, member, op.CollectionPlan, writerContext, op.SourceExpression);
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

    private static void EmitCollectionWrite(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        CollectionPlan plan,
        SerializationWriterEmitter.WriterCtx writerContext,
        string sourceExpression)
    {
        var accessExpression = sourceExpression;
        var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, accessExpression);
        if (plan.CollectionInfo.CountSize >= 0)
        {
            builder.WriteLine($"if ({accessExpression} is null)");
            using (builder.BeginBlock())
            {
                builder.WriteLine($"throw new System.ArgumentNullException(nameof({accessExpression}), \"Fixed-size collections cannot be null.\");");
            }

            builder.WriteLine($"if ({countExpression} != {plan.CollectionInfo.CountSize})");
            using (builder.BeginBlock())
            {
                builder.WriteLine($"throw new System.InvalidOperationException($\"Collection '{member.Name}' must have a size of {plan.CollectionInfo.CountSize} but was {{{countExpression}}}.\");");
            }

            EmitPayload(builder, member, plan, writerContext, accessExpression, countExpression);
            return;
        }

        if (plan.CollectionInfo.CountSizeReferenceIndex is not null)
        {
            builder.WriteLine($"if ({accessExpression} is not null)");
            using (builder.BeginBlock())
            {
                EmitPayload(builder, member, plan, writerContext, accessExpression, countExpression);
            }

            return;
        }

        if (plan.CanBeNull)
        {
            builder.WriteLine($"if ({accessExpression} is null)");
            using (builder.BeginBlock())
            {
                if (!plan.CollectionInfo.Unlimited)
                {
                    var countType = plan.CollectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
                    SerializationWriterEmitter.EmitWrite(builder, writerContext, countType, "0");
                }
            }

            builder.WriteLine("else");
            using (builder.BeginBlock())
            {
                EmitCountAndPayload(builder, member, plan, writerContext, accessExpression, countExpression);
            }

            return;
        }

        EmitCountAndPayload(builder, member, plan, writerContext, accessExpression, countExpression);
    }

    private static void EmitCountAndPayload(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        CollectionPlan plan,
        SerializationWriterEmitter.WriterCtx writerContext,
        string accessExpression,
        string countExpression)
    {
        if (!plan.CollectionInfo.Unlimited)
        {
            var countType = plan.CollectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            SerializationWriterEmitter.EmitCountWrite(builder, writerContext, countType, countExpression);
        }

        EmitPayload(builder, member, plan, writerContext, accessExpression, countExpression);
    }

    private static void EmitPayload(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        CollectionPlan plan,
        SerializationWriterEmitter.WriterCtx writerContext,
        string accessExpression,
        string countExpression)
    {
        if (TryEmitDirectWrite(builder, member, plan, writerContext, accessExpression))
        {
            return;
        }

        builder.WriteLine($"for (int i = 0; i < {countExpression}; i++)");
        using (builder.BeginBlock())
        {
            var itemExpression = plan.IsArray
                ? $"{accessExpression}[i]"
                : $"{accessExpression}[i]";
            EmitElementWrite(builder, member, plan, writerContext, itemExpression);
        }
    }

    private static bool TryEmitDirectWrite(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        CollectionPlan plan,
        SerializationWriterEmitter.WriterCtx writerContext,
        string accessExpression)
    {
        if (plan.BulkLayoutMode == BulkLayoutMode.None)
        {
            return false;
        }

        var spanExpression = GetElementSpanExpression(plan, accessExpression);
        if (spanExpression is null)
        {
            return false;
        }

        if (plan.BulkLayoutMode == BulkLayoutMode.ByteExact)
        {
            SerializationWriterEmitter.EmitWriteBytes(builder, writerContext, spanExpression);
            return true;
        }

        EmitContiguousGuardedWrite(builder, member, plan, writerContext, spanExpression);
        return true;
    }

    private static void EmitContiguousGuardedWrite(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        CollectionPlan plan,
        SerializationWriterEmitter.WriterCtx writerContext,
        string spanExpression)
    {
        builder.WriteLine($"if ({GetFastPathGuard(plan)})");
        using (builder.BeginBlock())
        {
            builder.WriteLine($"var collectionBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes({spanExpression});");
            SerializationWriterEmitter.EmitWriteBytes(builder, writerContext, "collectionBytes");
        }

        builder.WriteLine("else");
        using (builder.BeginBlock())
        {
            builder.WriteLine($"for (int i = 0; i < {spanExpression}.Length; i++)");
            using (builder.BeginBlock())
            {
                EmitElementWrite(builder, member, plan, writerContext, $"{spanExpression}[i]");
            }
        }
    }

    private static void EmitElementWrite(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        CollectionPlan plan,
        SerializationWriterEmitter.WriterCtx writerContext,
        string itemExpression)
    {
        if (plan.ElementHasGenerateSerializerAttribute)
        {
            if (plan.ElementIsValueType)
            {
                if (writerContext.IsSpan)
                {
                    builder.WriteLine($"{TypeHelper.GetGlobalTypeName(plan.ElementTypeName)}.Serialize({itemExpression}, ref {writerContext.Target});");
                }
                else
                {
                    builder.WriteLine($"{TypeHelper.GetGlobalTypeName(plan.ElementTypeName)}.Serialize({itemExpression}, {writerContext.Target});");
                }
            }
            else
            {
                SerializationWriterEmitter.EmitSerializeNestedOrThrow(builder, writerContext, plan.ElementTypeName, itemExpression);
            }

            return;
        }

        if (plan.ElementIsStringType)
        {
            SerializationWriterEmitter.EmitWriteString(builder, writerContext, itemExpression);
            return;
        }

        EmitPrimitiveWrite(builder, writerContext, plan.ElementTypeName, itemExpression);
    }

    private static void EmitPrimitiveWrite(
        IndentedStringBuilder builder,
        SerializationWriterEmitter.WriterCtx writerContext,
        string elementTypeName,
        string itemExpression)
    {
        SerializationWriterEmitter.EmitWrite(builder, writerContext, elementTypeName, itemExpression);
    }

    private static string? GetElementSpanExpression(CollectionPlan plan, string accessExpression)
    {
        if (plan.IsArray)
        {
            return $"{accessExpression}.AsSpan()";
        }

        if (plan.IsList)
        {
            return $"System.Runtime.InteropServices.CollectionsMarshal.AsSpan({accessExpression})";
        }

        return null;
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
}
