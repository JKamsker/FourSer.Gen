using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Helpers;

/// <summary>
/// Helper class for emitting C# code for serialization writers (SpanWriter/StreamWriter).
/// </summary>
internal static class SerializationWriterEmitter
{
    // Define WriterCtx here, perhaps as an internal record
    internal readonly record struct WriterCtx(string Target, string Helper, bool IsSpan)
    {
        public string Ref => IsSpan ? "ref " : "";
    }

    // Move all Emit... methods here
    public static void EmitWrite(IndentedStringBuilder sb, WriterCtx ctx, string typeName, string value, string comment = "")
    {
        var refOrEmpty = ctx.IsSpan ? "ref " : "";
        var friendlyTypeName = TypeHelper.GetMethodFriendlyTypeName(typeName);
        var writeMethod = $"Write{friendlyTypeName}";
        sb.WriteLineFormat(
            "{0}.{1}({2}{3}, ({4})({5}));{6}",
            ctx.Helper, writeMethod, refOrEmpty, ctx.Target, typeName, value, comment
        );
    }

    public static void EmitWriteString(IndentedStringBuilder sb, WriterCtx ctx, string value)
    {
        var refOrEmpty = ctx.IsSpan ? "ref " : "";
        sb.WriteLineFormat("{0}.WriteString({1}{2}, {3});", ctx.Helper, refOrEmpty, ctx.Target, value);
    }

    public static void EmitWriteBytes(IndentedStringBuilder sb, WriterCtx ctx, string value)
    {
        var refOrEmpty = ctx.IsSpan ? "ref " : "";
        sb.WriteLineFormat("{0}.WriteBytes({1}{2}, {3});", ctx.Helper, refOrEmpty, ctx.Target, value);
    }

    public static void EmitWriteTypeId(IndentedStringBuilder sb, WriterCtx ctx, PolymorphicOption option, PolymorphicInfo info)
    {
        var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
        var underlyingType = info.EnumUnderlyingType ?? info.TypeIdType;
        EmitWrite(sb, ctx, underlyingType, key);
    }

    public static void EmitSerializeNestedOrThrow(IndentedStringBuilder sb, WriterCtx ctx, string typeName, string instanceName)
    {
        sb.WriteLineFormat("if ({0} is null)", instanceName);
        using (sb.BeginBlock())
        {
            // Item of list?
            if(instanceName == "typedInstance")
            {
                sb.WriteLineFormat("throw new System.NullReferenceException($\"Instance of type \\\"{0}\\\" cannot be null.\");", typeName);
            }
            else
            {
                sb.WriteLineFormat
                    ("throw new System.NullReferenceException($\"Member \\\"{0}\\\" cannot be null.\");", instanceName);
            }
        }

        if (ctx.IsSpan)
        {
            sb.WriteLineFormat
            (
                "var bytesWritten = {0}.Serialize({1}, {2});",
                TypeHelper.GetSimpleTypeName(typeName),
                instanceName,
                ctx.Target
            );
            sb.WriteLine($"{ctx.Target} = {ctx.Target}.Slice(bytesWritten);");
        }
        else
        {
            sb.WriteLineFormat("{0}.Serialize({1}, {2});", TypeHelper.GetSimpleTypeName(typeName), instanceName, ctx.Target);
        }
    }
}
