using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Helpers;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Logic;

internal static class MemoryOwnerSerializer
{
    public static void Generate(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        TypeToGenerate type)
    {
        if (member.MemoryOwnerTypeInfo is not { } elementInfo)
        {
            return;
        }

        if (member.CollectionInfo is not { } collectionInfo)
        {
            // Should not happen: TypeInfoProvider assigns default CollectionInfo for IMemoryOwner<T>.
            collectionInfo = new CollectionInfo(
                PolymorphicMode.None,
                null,
                null,
                null,
                null,
                null,
                null
            );
        }

        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();

        // Null handling mirrors counted collections: null is treated as zero-length unless fixed-size.
        if (collectionInfo.CountSize > 0)
        {
            sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat(
                    "throw new System.ArgumentNullException(nameof(obj.{0}), \"Fixed-size collections cannot be null.\");",
                    member.Name
                );
            }

            EmitNonNullMemoryOwner(sb, member, elementInfo, ctx, type, collectionInfo, writeCount: false);
            return;
        }

        // Count is provided by another member - do not write it here.
        if (collectionInfo.CountSizeReferenceIndex is not null)
        {
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using (sb.BeginBlock())
            {
                EmitNonNullMemoryOwner(sb, member, elementInfo, ctx, type, collectionInfo, writeCount: false);
            }

            return;
        }

        // Regular counted collection.
        sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
        using (sb.BeginBlock())
        {
            if (!collectionInfo.Unlimited && collectionInfo.CountSize is null or < 0)
            {
                SerializationWriterEmitter.EmitWrite(sb, ctx, countType, "0");
            }
        }

        sb.WriteLine("else");
        using (sb.BeginBlock())
        {
            var shouldWriteCount = !collectionInfo.Unlimited && collectionInfo.CountSize is null or < 0;
            EmitNonNullMemoryOwner(sb, member, elementInfo, ctx, type, collectionInfo, writeCount: shouldWriteCount);
        }
    }

    private static void EmitNonNullMemoryOwner(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        MemoryOwnerTypeInfo elementInfo,
        SerializationWriterEmitter.WriterCtx ctx,
        TypeToGenerate type,
        CollectionInfo collectionInfo,
        bool writeCount)
    {
        if (collectionInfo.CountSize >= 0)
        {
            var expected = collectionInfo.CountSize;
            sb.WriteLineFormat("if (obj.{0}.Memory.Length != {1})", member.Name, expected);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat(
                    "throw new System.InvalidOperationException($\"Collection '{{{0}}}' must have a size of {1} but was {{obj.{0}.Memory.Length}}.\");",
                    member.Name,
                    expected
                );
            }
        }

        if (writeCount)
        {
            var countExpression = $"obj.{member.Name}.Memory.Length";
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, countExpression);
        }

        var spanVar = $"{member.Name.ToCamelCase()}Span";
        sb.WriteLineFormat("var {0} = obj.{1}.Memory.Span;", spanVar, member.Name);

        var isByteCollection = TypeHelper.IsByteCollection(elementInfo.ElementTypeName);
        if (isByteCollection)
        {
            SerializationWriterEmitter.EmitWriteBytes(sb, ctx, spanVar);
            return;
        }

        var useUnmanagedFastPath =
            member.CustomSerializer is null
            && elementInfo.IsElementUnmanagedType
            && !elementInfo.IsElementStringType
            && !elementInfo.HasElementGenerateSerializerAttribute
            && !GeneratorUtilities.HasDefaultSerializerFor(type, elementInfo.ElementTypeName);

        if (useUnmanagedFastPath)
        {
            SerializationWriterEmitter.EmitWriteBytes(
                sb,
                ctx,
                $"System.Runtime.InteropServices.MemoryMarshal.AsBytes({spanVar})"
            );
            return;
        }

        sb.WriteLineFormat("for (int i = 0; i < {0}.Length; i++)", spanVar);
        using (sb.BeginBlock())
        {
            var elementAccess = $"{spanVar}[i]";
            EmitElementSerialization(sb, member, elementInfo, elementAccess, ctx);
        }
    }

    private static void EmitElementSerialization(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        MemoryOwnerTypeInfo elementInfo,
        string elementAccess,
        SerializationWriterEmitter.WriterCtx ctx)
    {
        if (member.CustomSerializer is { } customSerializer)
        {
            var serializerField = global::FourSer.Gen.SerializerGenerator.SanitizeTypeName(customSerializer.SerializerTypeName);
            var serializerAccess = $"FourSer.Generated.Internal.__FourSer_Generated_Serializers.{serializerField}";
            if (ctx.IsSpan)
            {
                var bytesVar = $"bytesWritten_{member.Name.ToCamelCase()}";
                sb.WriteLineFormat("var {0} = {1}.Serialize({2}, {3});", bytesVar, serializerAccess, elementAccess, ctx.Target);
                sb.WriteLineFormat("{0} = {0}.Slice({1});", ctx.Target, bytesVar);
            }
            else
            {
                sb.WriteLineFormat("{0}.Serialize({1}, {2});", serializerAccess, elementAccess, ctx.Target);
            }

            return;
        }

        if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            if (ctx.IsSpan)
            {
                sb.WriteLineFormat(
                    "{0}.Serialize({1}, ref {2});",
                    TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName),
                    elementAccess,
                    ctx.Target
                );
            }
            else
            {
                sb.WriteLineFormat(
                    "{0}.Serialize({1}, {2});",
                    TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName),
                    elementAccess,
                    ctx.Target
                );
            }

            return;
        }

        if (elementInfo.IsElementUnmanagedType)
        {
            SerializationWriterEmitter.EmitWrite(sb, ctx, elementInfo.ElementTypeName, elementAccess);
            return;
        }

        if (elementInfo.IsElementStringType)
        {
            SerializationWriterEmitter.EmitWriteString(sb, ctx, elementAccess);
        }
    }
}

