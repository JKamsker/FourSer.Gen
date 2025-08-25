using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Helpers;
using FourSer.Gen.CodeGenerators.Logic;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
/// Generates Serialize method implementations.
/// </summary>
public static class SerializationGenerator
{
    // The context can now use the public definition from the emitter helper
    private static SerializationWriterEmitter.WriterCtx SpanCtx => new("data", "SpanWriter", true);
    private static SerializationWriterEmitter.WriterCtx StreamCtx => new("stream", "StreamWriter", false);

    public static void GenerateSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        GenerateSpanSerialize(sb, typeToGenerate);
        sb.WriteLine();
        GenerateStreamSerialize(sb, typeToGenerate);
    }

    private static void GenerateSpanSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLineFormat("public static int Serialize({0} obj, System.Span<byte> data)", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        // null check
        if (!typeToGenerate.IsValueType)
        {
            sb.WriteLine("if (obj is null) return 0;");
        }
        
        
        sb.WriteLine("var originalData = data;");
        GenerateSerializationBody(sb, typeToGenerate, SpanCtx);
        sb.WriteLine("return originalData.Length - data.Length;");
    }

    private static void GenerateStreamSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLineFormat("public static void Serialize({0} obj, System.IO.Stream stream)", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        // null check
        if (!typeToGenerate.IsValueType)
        {
            sb.WriteLine("if (obj is null) return;");
        }
        GenerateSerializationBody(sb, typeToGenerate, StreamCtx);
    }

    private static void GenerateSerializationBody(IndentedStringBuilder sb, TypeToGenerate typeToGenerate, SerializationWriterEmitter.WriterCtx ctx)
    {
        GenerateTypeIdPrePass(sb, typeToGenerate);

        foreach (var member in typeToGenerate.Members)
        {
            if (member.IsCountSizeReferenceFor is not null)
            {
                GenerateCountSizeReferenceSerialization(sb, typeToGenerate, member, ctx);
            }
            else if (member.IsTypeIdPropertyFor is not null)
            {
                GenerateTypeIdPropertySerialization(sb, typeToGenerate, member, ctx);
            }
            else
            {
                GenerateMemberSerialization(sb, member, typeToGenerate, ctx);
            }
        }
    }

    // This method is now a simple dispatcher
    private static void GenerateMemberSerialization(IndentedStringBuilder sb, MemberToGenerate member, TypeToGenerate type, SerializationWriterEmitter.WriterCtx ctx)
    {
        var customSerializer = GeneratorUtilities.ResolveSerializer(member, type);
        if (customSerializer != null)
        {
            sb.WriteLineFormat("new {0}().Serialize(obj.{1}, {2});", customSerializer, member.Name, ctx.Target);
            return;
        }

        if (member.IsList || member.IsCollection)
        {
            CollectionSerializer.Generate(sb, member, ctx);
        }
        else if (member.PolymorphicInfo is not null)
        {
            PolymorphicSerializer.GeneratePolymorphicMember(sb, member, ctx);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            SerializationWriterEmitter.EmitSerializeNestedOrThrow(sb, ctx, member.TypeName, $"obj.{member.Name}");
        }
        else if (member.IsStringType)
        {
            SerializationWriterEmitter.EmitWriteString(sb, ctx, $"obj.{member.Name}");
        }
        else if (member.IsUnmanagedType)
        {
            SerializationWriterEmitter.EmitWrite(sb, ctx, member.TypeName, $"obj.{member.Name}");
        }
    }

    private static void GenerateCountSizeReferenceSerialization(
        IndentedStringBuilder sb,
        TypeToGenerate typeToGenerate,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx)
    {
        var collectionMember = typeToGenerate.Members[member.IsCountSizeReferenceFor.Value];
        if (collectionMember.CollectionTypeInfo?.IsPureEnumerable != true)
        {
            var collectionName = collectionMember.Name;
            var countExpression = $"obj.{collectionName}?.Count ?? 0";
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
            SerializationWriterEmitter.EmitWrite(sb, ctx, member.TypeName, countExpression);
        }
    }

    private static void GenerateTypeIdPropertySerialization(
        IndentedStringBuilder sb,
        TypeToGenerate typeToGenerate,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx)
    {
        var referencedMember = typeToGenerate.Members[member.IsTypeIdPropertyFor.Value];
        if (referencedMember.IsList || referencedMember.IsCollection)
        {
            var collectionName = referencedMember.Name;
            var info = referencedMember.PolymorphicInfo.Value;
            var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;

            var defaultOption = info.Options.FirstOrDefault();
            var defaultKey = PolymorphicUtilities.FormatTypeIdKey(defaultOption.Key, info);

            sb.WriteLineFormat($"if (obj.{collectionName} is null || obj.{collectionName}.Count == 0)");
            using (sb.BeginBlock())
            {
                SerializationWriterEmitter.EmitWrite(sb, ctx, typeIdType, defaultKey);
            }

            sb.WriteLine("else");
            using (sb.BeginBlock())
            {
                sb.WriteLine($"var firstItem = obj.{collectionName}[0];");
                sb.WriteLine("var discriminator = firstItem switch");
                sb.WriteLine("{");
                sb.Indent();
                foreach (var option in info.Options)
                {
                    var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                    sb.WriteLineFormat("{0} => ({1}){2},", TypeHelper.GetSimpleTypeName(option.Type), typeIdType, key);
                }

                sb.WriteLine
                (
                    $"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{firstItem.GetType().Name}}\")"
                );
                sb.Unindent();
                sb.WriteLine("};");

                SerializationWriterEmitter.EmitWrite(sb, ctx, typeIdType, "discriminator");
            }
        }
        else
        {
            GenerateMemberSerialization(sb, member, typeToGenerate, ctx);
        }
    }

    private static void GenerateTypeResolutionSwitch(
        IndentedStringBuilder sb,
        string switchExpression,
        string assignmentTarget,
        PolymorphicInfo info)
    {
        sb.WriteLineFormat("switch ({0})", switchExpression);
        using (sb.BeginBlock())
        {
            foreach (var option in info.Options)
            {
                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                sb.WriteLineFormat("case {0}:", typeName);
                sb.WriteLineFormat("    {0} = {1};", assignmentTarget, key);
                sb.WriteLine("    break;");
            }

            sb.WriteLine("case null:");
            sb.WriteLine("    break;");
        }
    }

    private static void GenerateTypeIdPrePass(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        for (var i = 0; i < typeToGenerate.Members.Count; i++)
        {
            var member = typeToGenerate.Members[i];
            if (member.PolymorphicInfo is not { TypeIdPropertyIndex: not null } info)
            {
                continue;
            }

            if ((member.IsList || member.IsCollection) &&
                member.CollectionInfo?.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                continue;
            }

            var referencedMember = typeToGenerate.Members[info.TypeIdPropertyIndex.Value];
            GenerateTypeResolutionSwitch(sb, $"obj.{member.Name}", $"obj.{referencedMember.Name}", info);
        }
    }
}
