using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Helpers;
using FourSer.Gen.CodeGenerators.Logic;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class PlanCollectionEmitter
{
    public static void EmitFixedBytesRead(PlanEmitterContext context, FixedBytesReadOp op)
    {
        if (op.UseRef)
        {
            context.Builder.WriteLine($"{op.HelperName}.ReadBytes(ref {op.SourceExpression}, {op.TargetExpression});");
            return;
        }

        context.Builder.WriteLine($"{op.SourceExpression}.ReadExactly({op.TargetExpression});");
    }

    public static void EmitFixedBytesWrite(PlanEmitterContext context, FixedBytesWriteOp op)
    {
        if (context.Plan.TargetKind == TargetKind.Span)
        {
            context.Builder.WriteLine($"{op.ValueExpression}.CopyTo({op.TargetExpression});");
            context.Builder.WriteLine($"{op.TargetExpression} = {op.TargetExpression}.Slice({op.ByteCount});");
            return;
        }

        context.Builder.WriteLine($"stream.Write({op.ValueExpression});");
    }

    public static void EmitBatchRead(PlanEmitterContext context, BatchReadOp op)
    {
        if (context.Plan.TargetKind == TargetKind.Span)
        {
            EmitSpanBatchRead(context, op);
            return;
        }

        EmitStreamBatchRead(context, op);
    }

    public static void EmitBatchWrite(PlanEmitterContext context, BatchWriteOp op)
    {
        if (context.Plan.TargetKind == TargetKind.Span)
        {
            EmitSpanBatchWrite(context, op);
            return;
        }

        EmitStreamBatchWrite(context, op);
    }

    public static void EmitCollectionRead(PlanEmitterContext context, CollectionReadOp op)
    {
        var member = context.GetMember(op.Key ?? op.CollectionPlan.MemberName);
        if (PlannedCollectionReadEmitter.TryEmit(context, op, member))
        {
            return;
        }

        if (op.CollectionPlan.IsMemoryOwner)
        {
            DeserializationGenerator.GenerateMemoryOwnerDeserialization(
                context.Builder,
                member,
                op.TargetExpression,
                op.SourceExpression,
                op.HelperName,
                context.Plan.Facts.Type);
            return;
        }

        DeserializationGenerator.GenerateCollectionDeserialization(
            context.Builder,
            member,
            op.TargetExpression,
            op.SourceExpression,
            op.HelperName,
            context.Plan.Facts.Type);
    }

    public static void EmitCollectionWrite(PlanEmitterContext context, CollectionWriteOp op)
    {
        var member = context.GetMember(op.Key ?? op.CollectionPlan.MemberName);
        if (PlannedCollectionWriteEmitter.TryEmit(context, op, member))
        {
            return;
        }

        var writerContext = new SerializationWriterEmitter.WriterCtx(
            op.TargetExpression,
            op.HelperName,
            op.TargetKind == TargetKind.Span);

        if (op.CollectionPlan.IsMemoryOwner)
        {
            MemoryOwnerSerializer.Generate(
                context.Builder,
                member,
                writerContext,
                context.Plan.Facts.Type);
            return;
        }

        CollectionSerializer.Generate(
            context.Builder,
            member,
            writerContext,
            context.Plan.Facts.Type,
            op.SourceExpression);
    }

    public static void EmitCollectionValidate(PlanEmitterContext context, CollectionValidateOp op)
    {
        var member = context.GetMember(op.Key ?? op.CollectionPlan.MemberName);
        CollectionValidationEmitter.Generate(
            context.Builder,
            member,
            op.CollectionPlan,
            op.SourceExpression);
    }

    public static void EmitCollectionSize(PlanEmitterContext context, CollectionSizeOp op)
    {
        var member = context.GetMember(op.Key ?? op.CollectionPlan.MemberName);
        if (op.CollectionPlan.IsMemoryOwner)
        {
            PacketSizeGenerator.GenerateMemoryOwnerSizeCalculation(context.Builder, member);
            return;
        }

        PacketSizeGenerator.GenerateCollectionSizeCalculation(context.Builder, member, context.Plan.Facts.Type);
    }

    private static void EmitSpanBatchRead(PlanEmitterContext context, BatchReadOp op)
    {
        var bufferVar = $"{op.BufferName}Span";
        context.Builder.WriteLine($"var {bufferVar} = {op.SourceExpression}.Slice(0, {op.TotalSize});");
        context.Builder.WriteLine($"{op.SourceExpression} = {op.SourceExpression}.Slice({op.TotalSize});");
        EmitBatchReadAssignments(context, op, bufferVar);
    }

    private static void EmitStreamBatchRead(PlanEmitterContext context, BatchReadOp op)
    {
        var bufferVar = $"{op.BufferName}Buffer";
        var threshold = context.Plan.Facts.Options.StackallocThreshold;
        if (op.TotalSize <= threshold)
        {
            context.Builder.WriteLine($"Span<byte> {bufferVar} = stackalloc byte[{op.TotalSize}];");
            context.Builder.WriteLine($"stream.ReadExactly({bufferVar});");
            EmitBatchReadAssignments(context, op, bufferVar);
            return;
        }

        var rentedVar = $"{bufferVar}Rented";
        context.Builder.WriteLine($"var {rentedVar} = System.Buffers.ArrayPool<byte>.Shared.Rent({op.TotalSize});");
        context.Builder.WriteLine("try");
        using (context.Builder.BeginBlock())
        {
            context.Builder.WriteLine($"var {bufferVar} = {rentedVar}.AsSpan(0, {op.TotalSize});");
            context.Builder.WriteLine($"stream.ReadExactly({bufferVar});");
            EmitBatchReadAssignments(context, op, bufferVar);
        }

        context.Builder.WriteLine("finally");
        using (context.Builder.BeginBlock())
        {
            context.Builder.WriteLine($"System.Buffers.ArrayPool<byte>.Shared.Return({rentedVar});");
        }
    }

    private static void EmitBatchReadAssignments(PlanEmitterContext context, BatchReadOp op, string bufferVar)
    {
        foreach (var member in op.Members)
        {
            if (member.IsFixedCollection)
            {
                var elementType = TypeHelper.GetSimpleTypeName(member.ElementTypeName ?? "byte");
                context.Builder.WriteLine($"{member.TargetExpression} = new {elementType}[{member.FixedCount}];");
                if (elementType == "byte")
                {
                    context.Builder.WriteLine($"{bufferVar}.Slice({member.Offset}, {member.Size}).CopyTo({member.TargetExpression}.AsSpan());");
                }
                else
                {
                    context.Builder.WriteLine($"{bufferVar}.Slice({member.Offset}, {member.Size}).CopyTo(System.Runtime.InteropServices.MemoryMarshal.AsBytes({member.TargetExpression}.AsSpan()));");
                }

                continue;
            }

            var readExpression = BatchingUtilities.GetBatchReadExpression(bufferVar, member.TypeName, member.Offset);
            context.Builder.WriteLine($"{member.TargetExpression} = {readExpression};");
        }
    }

    private static void EmitSpanBatchWrite(PlanEmitterContext context, BatchWriteOp op)
    {
        var bufferVar = $"{op.BufferName}Span";
        context.Builder.WriteLine($"var {bufferVar} = {op.TargetExpression}.Slice(0, {op.TotalSize});");
        EmitBatchWriteAssignments(context, op, bufferVar);
        context.Builder.WriteLine($"{op.TargetExpression} = {op.TargetExpression}.Slice({op.TotalSize});");
    }

    private static void EmitStreamBatchWrite(PlanEmitterContext context, BatchWriteOp op)
    {
        var bufferVar = $"{op.BufferName}Buffer";
        var threshold = context.Plan.Facts.Options.StackallocThreshold;
        if (op.TotalSize <= threshold)
        {
            context.Builder.WriteLine($"Span<byte> {bufferVar} = stackalloc byte[{op.TotalSize}];");
            EmitBatchWriteAssignments(context, op, bufferVar);
            context.Builder.WriteLine($"stream.Write({bufferVar});");
            return;
        }

        var rentedVar = $"{bufferVar}Rented";
        context.Builder.WriteLine($"var {rentedVar} = System.Buffers.ArrayPool<byte>.Shared.Rent({op.TotalSize});");
        context.Builder.WriteLine("try");
        using (context.Builder.BeginBlock())
        {
            context.Builder.WriteLine($"var {bufferVar} = {rentedVar}.AsSpan(0, {op.TotalSize});");
            EmitBatchWriteAssignments(context, op, bufferVar);
            context.Builder.WriteLine($"stream.Write({bufferVar});");
        }

        context.Builder.WriteLine("finally");
        using (context.Builder.BeginBlock())
        {
            context.Builder.WriteLine($"System.Buffers.ArrayPool<byte>.Shared.Return({rentedVar});");
        }
    }

    private static void EmitBatchWriteAssignments(PlanEmitterContext context, BatchWriteOp op, string bufferVar)
    {
        foreach (var member in op.Members)
        {
            if (member.IsFixedCollection)
            {
                EmitFixedCollectionBatchWrite(context, member, bufferVar);
                continue;
            }

            if (member.TypeName == "decimal")
            {
                EmitDecimalBatchWrite(context, member, bufferVar);
                continue;
            }

            var writeExpression = BatchingUtilities.GetBatchWriteExpression(bufferVar, member.TypeName, member.Offset, member.ValueExpression);
            context.Builder.WriteLine($"{writeExpression};");
        }
    }

    private static void EmitFixedCollectionBatchWrite(PlanEmitterContext context, BatchMemberPlan member, string bufferVar)
    {
        context.Builder.WriteLine($"if ({member.ValueExpression} is null)");
        using (context.Builder.BeginBlock())
        {
            context.Builder.WriteLine($"throw new System.ArgumentNullException(nameof({member.ValueExpression}), \"Fixed-size collections cannot be null.\");");
        }

        context.Builder.WriteLine($"if ({member.ValueExpression}.Length != {member.FixedCount})");
        using (context.Builder.BeginBlock())
        {
            context.Builder.WriteLine($"throw new System.InvalidOperationException($\"Collection must have a size of {member.FixedCount} but was {{{member.ValueExpression}.Length}}.\");");
        }

        var elementType = TypeHelper.GetSimpleTypeName(member.ElementTypeName ?? "byte");
        if (elementType == "byte")
        {
            context.Builder.WriteLine($"{member.ValueExpression}.AsSpan().CopyTo({bufferVar}.Slice({member.Offset}, {member.Size}));");
            return;
        }

        context.Builder.WriteLine($"System.Runtime.InteropServices.MemoryMarshal.AsBytes({member.ValueExpression}.AsSpan()).CopyTo({bufferVar}.Slice({member.Offset}, {member.Size}));");
    }

    private static void EmitDecimalBatchWrite(PlanEmitterContext context, BatchMemberPlan member, string bufferVar)
    {
        var bitsVar = $"{member.MemberName.ToCamelCase()}Bits";
        context.Builder.WriteLine($"Span<int> {bitsVar} = stackalloc int[4];");
        context.Builder.WriteLine($"decimal.GetBits({member.ValueExpression}, {bitsVar});");
        context.Builder.WriteLine($"System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({bufferVar}.Slice({member.Offset}), {bitsVar}[0]);");
        context.Builder.WriteLine($"System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({bufferVar}.Slice({member.Offset + 4}), {bitsVar}[1]);");
        context.Builder.WriteLine($"System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({bufferVar}.Slice({member.Offset + 8}), {bitsVar}[2]);");
        context.Builder.WriteLine($"System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({bufferVar}.Slice({member.Offset + 12}), {bitsVar}[3]);");
    }
}
