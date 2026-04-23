using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class PlanScalarEmitter
{
    public static void EmitScalarRead(PlanEmitterContext context, ScalarReadOp op)
    {
        var refPrefix = op.UseRef ? "ref " : string.Empty;
        var readMethod = TypeHelper.GetReadMethodName(op.TypeName);
        context.Builder.WriteLine($"{op.TargetExpression} = {op.HelperName}.{readMethod}({refPrefix}{op.SourceExpression});");
    }

    public static void EmitScalarWrite(PlanEmitterContext context, ScalarWriteOp op)
    {
        var refPrefix = context.Plan.TargetKind == TargetKind.Span ? "ref " : string.Empty;
        var writeMethod = TypeHelper.GetWriteMethodName(op.TypeName);
        var valueExpression = op.UseCheckedConversion
            ? $"checked(({op.TypeName})({op.ValueExpression}))"
            : $"({op.TypeName})({op.ValueExpression})";
        context.Builder.WriteLine($"{op.HelperName}.{writeMethod}({refPrefix}{op.TargetExpression}, {valueExpression});");
    }

    public static void EmitStringRead(PlanEmitterContext context, StringReadOp op)
    {
        var refPrefix = op.UseRef ? "ref " : string.Empty;
        context.Builder.WriteLine($"{op.TargetExpression} = {op.HelperName}.ReadString({refPrefix}{op.SourceExpression});");
    }

    public static void EmitStringWrite(PlanEmitterContext context, StringWriteOp op)
    {
        if (op.UseFusedStreamWrite && context.Plan.TargetKind == TargetKind.Stream)
        {
            EmitFusedStreamStringWrite(context, op);
            return;
        }

        var refPrefix = context.Plan.TargetKind == TargetKind.Span ? "ref " : string.Empty;
        context.Builder.WriteLine($"{op.HelperName}.WriteString({refPrefix}{op.TargetExpression}, {op.ValueExpression});");
    }

    public static void EmitCountRead(PlanEmitterContext context, CountReadOp op)
    {
        var refPrefix = op.UseRef ? "ref " : string.Empty;
        var readMethod = TypeHelper.GetReadMethodName(op.TypeName);
        var expression = $"{op.HelperName}.{readMethod}({refPrefix}{op.SourceExpression})";
        if (!string.IsNullOrEmpty(op.CastTypeName))
        {
            expression = $"({op.CastTypeName}){expression}";
        }

        context.Builder.WriteLine($"{op.TargetExpression} = {expression};");
    }

    public static void EmitCountWrite(PlanEmitterContext context, CountWriteOp op)
    {
        EmitScalarWrite(
            context,
            new ScalarWriteOp(
                op.Key,
                op.TypeName,
                op.IsPlaceholder ? "0" : op.ValueExpression,
                op.TargetExpression,
                op.HelperName,
                op.UseCheckedConversion,
                op.Comment));
    }

    public static void EmitSizeAdd(PlanEmitterContext context, SizeAddOp op)
    {
        context.Builder.WriteLine($"size += {op.Expression};");
    }

    private static void EmitFusedStreamStringWrite(PlanEmitterContext context, StringWriteOp op)
    {
        var prefix = $"{(op.Key ?? "string").ToCamelCase()}String";
        var byteCountVar = $"{prefix}ByteCount";
        var totalBytesVar = $"{prefix}TotalBytes";
        var bufferVar = $"{prefix}Buffer";
        var rentedVar = $"{bufferVar}Rented";
        var actualBytesVar = $"{prefix}ActualBytes";
        var stackallocThreshold = context.Plan.Facts.Options.StackallocThreshold.ToString();

        context.Builder.WriteLine($"if (string.IsNullOrEmpty({op.ValueExpression}))");
        using (context.Builder.BeginBlock())
        {
            context.Builder.WriteLine("global::FourSer.Gen.Helpers.StreamWriterHelpers.WriteInt32(stream, 0);");
        }

        context.Builder.WriteLine("else");
        using (context.Builder.BeginBlock())
        {
            context.Builder.WriteLine($"var {byteCountVar} = System.Text.Encoding.UTF8.GetByteCount({op.ValueExpression});");
            context.Builder.WriteLine($"var {totalBytesVar} = {byteCountVar} + sizeof(int);");
            context.Builder.WriteLine($"if ({totalBytesVar} <= {stackallocThreshold})");
            using (context.Builder.BeginBlock())
            {
                context.Builder.WriteLine($"Span<byte> {bufferVar} = stackalloc byte[{totalBytesVar}];");
                context.Builder.WriteLine($"System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({bufferVar}, {byteCountVar});");
                context.Builder.WriteLine($"var {actualBytesVar} = System.Text.Encoding.UTF8.GetBytes({op.ValueExpression}, {bufferVar}.Slice(sizeof(int)));");
                context.Builder.WriteLine($"stream.Write({bufferVar}.Slice(0, sizeof(int) + {actualBytesVar}));");
            }

            context.Builder.WriteLine("else");
            using (context.Builder.BeginBlock())
            {
                context.Builder.WriteLine($"var {rentedVar} = System.Buffers.ArrayPool<byte>.Shared.Rent({totalBytesVar});");
                context.Builder.WriteLine("try");
                using (context.Builder.BeginBlock())
                {
                    context.Builder.WriteLine($"var {bufferVar} = {rentedVar}.AsSpan(0, {totalBytesVar});");
                    context.Builder.WriteLine($"System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({bufferVar}, {byteCountVar});");
                    context.Builder.WriteLine($"var {actualBytesVar} = System.Text.Encoding.UTF8.GetBytes({op.ValueExpression}, {bufferVar}.Slice(sizeof(int)));");
                    context.Builder.WriteLine($"stream.Write({bufferVar}.Slice(0, sizeof(int) + {actualBytesVar}));");
                }

                context.Builder.WriteLine("finally");
                using (context.Builder.BeginBlock())
                {
                    context.Builder.WriteLine($"System.Buffers.ArrayPool<byte>.Shared.Return({rentedVar});");
                }
            }
        }
    }
}
