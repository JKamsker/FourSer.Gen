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
        sb.WriteLineFormat("public static void Serialize({0} obj, ref System.Span<byte> data)", typeToGenerate.Name);
        using (var _ = sb.BeginBlock())
        {
            if (!typeToGenerate.IsValueType)
            {
                sb.WriteLine("if (obj is null) return;");
            }

            GenerateSerializationBody(sb, typeToGenerate, SpanCtx);
        }

        sb.WriteLine();
        sb.WriteLineFormat("public static void Serialize({0} obj, System.Span<byte> data)", typeToGenerate.Name);
        using (var _ = sb.BeginBlock())
        {
            sb.WriteLine("Serialize(obj, ref data);");
        }
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

        if (!ctx.IsSpan)
        {
            var instructions = BatchingUtilities.AnalyzeMembers(typeToGenerate.Members, typeToGenerate, BatchingUtilities.DefaultMinBatchSize);
            var batchIndex = 0;

            foreach (var instruction in instructions)
            {
                switch (instruction)
                {
                    case BatchGroup batchGroup:
                        GenerateBatchGroupStreamSerialization(sb, batchGroup, ref batchIndex, typeToGenerate);
                        break;
                    case SingleMember single:
                        if (single.Member.IsCountSizeReferenceFor is not null)
                        {
                            GenerateCountSizeReferenceSerialization(sb, typeToGenerate, single.Member, ctx);
                        }
                        else if (single.Member.IsTypeIdPropertyFor is not null)
                        {
                            GenerateTypeIdPropertySerialization(sb, typeToGenerate, single.Member, ctx);
                        }
                        else
                        {
                            GenerateMemberSerialization(sb, single.Member, typeToGenerate, ctx);
                        }
                        break;
                }
            }
            return;
        }

        // Span path: also use batching to reduce per-field overhead when contiguous.
        {
            var instructions = BatchingUtilities.AnalyzeMembers(typeToGenerate.Members, typeToGenerate, BatchingUtilities.DefaultMinBatchSize);
            var batchIndex = 0;

            foreach (var instruction in instructions)
            {
                switch (instruction)
                {
                    case BatchGroup batchGroup:
                        GenerateBatchGroupSpanSerialization(sb, batchGroup, ref batchIndex, ctx);
                        break;
                    case SingleMember single:
                        if (single.Member.IsCountSizeReferenceFor is not null)
                        {
                            GenerateCountSizeReferenceSerialization(sb, typeToGenerate, single.Member, ctx);
                        }
                        else if (single.Member.IsTypeIdPropertyFor is not null)
                        {
                            GenerateTypeIdPropertySerialization(sb, typeToGenerate, single.Member, ctx);
                        }
                        else
                        {
                            GenerateMemberSerialization(sb, single.Member, typeToGenerate, ctx);
                        }
                        break;
                }
            }
        }
    }

    private static void GenerateBatchGroupStreamSerialization(
        IndentedStringBuilder sb,
        BatchGroup batchGroup,
        ref int batchIndex,
        TypeToGenerate type)
    {
        var batchVar = $"batch{batchIndex++}";
        var totalSize = batchGroup.TotalSize;

        var needsPool = totalSize > BatchingUtilities.StackAllocThreshold;
        if (!needsPool)
        {
            sb.WriteLineFormat("Span<byte> {0} = stackalloc byte[{1}];", batchVar, totalSize);
        }
        else
        {
            sb.WriteLineFormat("var {0}Rented = System.Buffers.ArrayPool<byte>.Shared.Rent({1});", batchVar, totalSize);
            sb.WriteLineFormat("var {0} = {0}Rented.AsSpan(0, {1});", batchVar, totalSize);
        }

        foreach (var batchedMember in batchGroup.Members)
        {
            var member = batchedMember.Member;

            if (batchedMember.IsFixedCollection)
            {
                sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
                using (sb.BeginBlock())
                {
                    sb.WriteLineFormat("throw new System.ArgumentNullException(nameof(obj.{0}), \"Fixed-size collections cannot be null.\");", member.Name);
                }
                sb.WriteLineFormat("if (obj.{0}.Length != {1})", member.Name, batchedMember.FixedCount);
                using (sb.BeginBlock())
                {
                    sb.WriteLineFormat("throw new System.InvalidOperationException($\"Collection '{0}' must have a size of {1} but was {{obj.{0}.Length}}.\");", member.Name, batchedMember.FixedCount);
                }

                var elemType = TypeHelper.GetSimpleTypeName(batchedMember.ElementTypeName ?? "byte");

                if (elemType == "byte")
                {
                    sb.WriteLineFormat("obj.{0}.AsSpan().CopyTo({1}.Slice({2}, {3}));", member.Name, batchVar, batchedMember.Offset, batchedMember.Size);
                }
                else
                {
                    sb.WriteLineFormat("System.Runtime.InteropServices.MemoryMarshal.AsBytes(obj.{0}.AsSpan()).CopyTo({1}.Slice({2}, {3}));", member.Name, batchVar, batchedMember.Offset, batchedMember.Size);
                }
                continue;
            }

            // Decimal needs special handling because GetBatchWriteExpression is not yet optimized for it.
            if (member.TypeName == "decimal")
            {
                var bitsVar = $"bits_{batchVar}_{member.Name.ToCamelCase()}";
                sb.WriteLineFormat("int[] {0} = new int[4];", bitsVar);
                sb.WriteLineFormat("decimal.GetBits(obj.{0}, {1});", member.Name, bitsVar);
                sb.WriteLineFormat("System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({0}.Slice({1}), {2}[0]);", batchVar, batchedMember.Offset, bitsVar);
                sb.WriteLineFormat("System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({0}.Slice({1}), {2}[1]);", batchVar, batchedMember.Offset + 4, bitsVar);
                sb.WriteLineFormat("System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({0}.Slice({1}), {2}[2]);", batchVar, batchedMember.Offset + 8, bitsVar);
                sb.WriteLineFormat("System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({0}.Slice({1}), {2}[3]);", batchVar, batchedMember.Offset + 12, bitsVar);
                continue;
            }

            var writeExpr = BatchingUtilities.GetBatchWriteExpression(batchVar, member.TypeName, batchedMember.Offset, $"obj.{member.Name}");
            sb.WriteLine(writeExpr + ";");
        }

        if (!needsPool)
        {
            sb.WriteLineFormat("stream.Write({0});", batchVar);
        }
        else
        {
            sb.WriteLine("try");
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat("stream.Write({0});", batchVar);
            }
            sb.WriteLine("finally");
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat("System.Buffers.ArrayPool<byte>.Shared.Return({0}Rented);", batchVar);
            }
        }
    }

    private static void GenerateBatchGroupSpanSerialization(
        IndentedStringBuilder sb,
        BatchGroup batchGroup,
        ref int batchIndex,
        SerializationWriterEmitter.WriterCtx ctx)
    {
        var batchVar = $"batchSpan{batchIndex++}";
        var totalSize = batchGroup.TotalSize;

        sb.WriteLineFormat("var {0} = {1}.Slice(0, {2});", batchVar, ctx.Target, totalSize);

        foreach (var batchedMember in batchGroup.Members)
        {
            var member = batchedMember.Member;

            if (batchedMember.IsFixedCollection)
            {
                sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
                using (sb.BeginBlock())
                {
                    sb.WriteLineFormat("throw new System.ArgumentNullException(nameof(obj.{0}), \"Fixed-size collections cannot be null.\");", member.Name);
                }
                sb.WriteLineFormat("if (obj.{0}.Length != {1})", member.Name, batchedMember.FixedCount);
                using (sb.BeginBlock())
                {
                    sb.WriteLineFormat("throw new System.InvalidOperationException($\"Collection '{0}' must have a size of {1} but was {{obj.{0}.Length}}.\");", member.Name, batchedMember.FixedCount);
                }

                var elemType = TypeHelper.GetSimpleTypeName(batchedMember.ElementTypeName ?? "byte");

                if (elemType == "byte")
                {
                    sb.WriteLineFormat("obj.{0}.AsSpan().CopyTo({1}.Slice({2}, {3}));", member.Name, batchVar, batchedMember.Offset, batchedMember.Size);
                }
                else
                {
                    sb.WriteLineFormat("System.Runtime.InteropServices.MemoryMarshal.AsBytes(obj.{0}.AsSpan()).CopyTo({1}.Slice({2}, {3}));", member.Name, batchVar, batchedMember.Offset, batchedMember.Size);
                }
                continue;
            }

            if (member.TypeName == "decimal")
            {
                var bitsVar = $"bits_{batchVar}_{member.Name.ToCamelCase()}";
                sb.WriteLineFormat("int[] {0} = new int[4];", bitsVar);
                sb.WriteLineFormat("decimal.GetBits(obj.{0}, {1});", member.Name, bitsVar);
                sb.WriteLineFormat("System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({0}.Slice({1}), {2}[0]);", batchVar, batchedMember.Offset, bitsVar);
                sb.WriteLineFormat("System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({0}.Slice({1}), {2}[1]);", batchVar, batchedMember.Offset + 4, bitsVar);
                sb.WriteLineFormat("System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({0}.Slice({1}), {2}[2]);", batchVar, batchedMember.Offset + 8, bitsVar);
                sb.WriteLineFormat("System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({0}.Slice({1}), {2}[3]);", batchVar, batchedMember.Offset + 12, bitsVar);
                continue;
            }

            var writeExpr = BatchingUtilities.GetBatchWriteExpression(batchVar, member.TypeName, batchedMember.Offset, $"obj.{member.Name}");
            sb.WriteLine(writeExpr + ";");
        }

        sb.WriteLineFormat("{0} = {0}.Slice({1});", ctx.Target, totalSize);
    }

    // This method is now a simple dispatcher
    private static void GenerateMemberSerialization(IndentedStringBuilder sb, MemberToGenerate member, TypeToGenerate type, SerializationWriterEmitter.WriterCtx ctx)
    {
        var resolvedSerializer = GeneratorUtilities.ResolveSerializer(member, type);
        if (resolvedSerializer is { } serializer)
        {
            if (ctx.IsSpan)
            {
                sb.WriteLineFormat("var bytesWritten_{1} = FourSer.Generated.Internal.__FourSer_Generated_Serializers.{0}.Serialize(obj.{1}, {2});", serializer.FieldName, member.Name, ctx.Target);
                sb.WriteLineFormat("{0} = {0}.Slice(bytesWritten_{1});", ctx.Target, member.Name);
            }
            else // stream
            {
                sb.WriteLineFormat("FourSer.Generated.Internal.__FourSer_Generated_Serializers.{0}.Serialize(obj.{1}, {2});", serializer.FieldName, member.Name, ctx.Target);
            }
            return;
        }

        if (member.IsList || member.IsCollection)
        {
            CollectionSerializer.Generate(sb, member, ctx, type);
        }
        else if (member.PolymorphicInfo is not null)
        {
            PolymorphicSerializer.GeneratePolymorphicMember(sb, member, ctx);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            if (!member.IsValueType)
            {
                sb.WriteLine($"if (obj.{member.Name} is null)");
                using (sb.BeginBlock())
                {
                    sb.WriteLine($"throw new System.NullReferenceException($\"Member \\\"obj.{member.Name}\\\" cannot be null.\");");
                }
            }
            if (ctx.IsSpan)
            {
                sb.WriteLineFormat($"{member.TypeName}.Serialize(obj.{member.Name}, ref {ctx.Target});");
            }
            else // stream
            {
                sb.WriteLineFormat($"{member.TypeName}.Serialize(obj.{member.Name}, {ctx.Target});");
            }
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
        if (member.IsCountSizeReferenceFor is null)
        {
            return;
        }
        var countRefIndex = member.IsCountSizeReferenceFor!.Value;
        var collectionMember = typeToGenerate.Members[countRefIndex];
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
        if (member.IsTypeIdPropertyFor is null)
        {
            return;
        }
        var typeIdIndex = member.IsTypeIdPropertyFor!.Value;
        var referencedMember = typeToGenerate.Members[typeIdIndex];
        if (referencedMember.IsList || referencedMember.IsCollection)
        {
            var collectionName = referencedMember.Name;
            if (referencedMember.PolymorphicInfo is null)
            {
                throw new InvalidOperationException("TypeIdPropertyFor member must have PolymorphicInfo.");
            }
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
