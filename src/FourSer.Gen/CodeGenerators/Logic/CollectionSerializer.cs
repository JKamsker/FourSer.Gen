using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Helpers;
using FourSer.Gen.Models;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Logic;

internal static class CollectionSerializer
{
    private enum CollMode { Fixed, Count }

    public static void Generate(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx, TypeToGenerate type, string? collectionAccess = null)
    {
        var accessExpression = collectionAccess ?? $"obj.{member.Name}";
        var mode = GetCollectionMode(member);
        if (mode == CollMode.Fixed)
        {
            EmitFixedCollection(sb, member, ctx, type, accessExpression);
        }
        else
        {
            EmitCountedCollection(sb, member, ctx, type, accessExpression);
        }
    }

    private static CollMode GetCollectionMode(MemberToGenerate member)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            throw new InvalidOperationException("GetCollectionMode should only be called on collection members.");
        }

        if (collectionInfo.CountSize > 0)
        {
            return CollMode.Fixed;
        }

        return CollMode.Count;
    }

    private static void HandleNonNullCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        TypeToGenerate type,
        string collectionAccess
    )
    {
        var isHandledByPolymorphic = collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId &&
            string.IsNullOrEmpty(collectionInfo.TypeIdProperty);

        if (collectionInfo.CountSize >= 0)
        {
            var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, collectionAccess);
            sb.WriteLineFormat("if ({0} != {1})", countExpression, collectionInfo.CountSize);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat
                (
                    "throw new System.InvalidOperationException($\"Collection '{0}' must have a size of {1} but was {{{2}}}.\");",
                    member.Name,
                    collectionInfo.CountSize,
                    countExpression
                );
            }
        }
        else if (collectionInfo is { Unlimited: false, CountSizeReferenceIndex: null } && !isHandledByPolymorphic)
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, collectionAccess);
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, countExpression);
        }

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            PolymorphicSerializer.GeneratePolymorphicCollection
            (
                sb,
                member,
                ctx,
                collectionInfo,
                collectionAccess
            );
            return;
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (isByteCollection)
        {
            SerializationWriterEmitter.EmitWriteBytes(sb, ctx, collectionAccess);
            return;
        }

        if (TryEmitUnmanagedContiguousCollectionWrite(sb, member, ctx, type, collectionAccess))
        {
            return;
        }

        GenerateStandardCollectionBody(sb, member, ctx, collectionAccess);
    }

    private static bool TryEmitUnmanagedContiguousCollectionWrite(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        TypeToGenerate type,
        string collectionAccess)
    {
        // Only non-polymorphic, unmanaged element collections without custom/default serializers qualify.
        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            return false;
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        if (string.IsNullOrEmpty(elementTypeName))
        {
            return false;
        }
        var elementTypeNameNonNull = elementTypeName!;

        var elementIsUnmanaged = member.ListTypeArgument?.IsUnmanagedType ?? member.CollectionTypeInfo?.IsElementUnmanagedType ?? false;
        var elementIsString = member.ListTypeArgument?.IsStringType ?? member.CollectionTypeInfo?.IsElementStringType ?? false;
        var elementHasGenerateSerializer = member.ListTypeArgument?.HasGenerateSerializerAttribute ?? member.CollectionTypeInfo?.HasElementGenerateSerializerAttribute ?? false;

        if (!elementIsUnmanaged || elementIsString || elementHasGenerateSerializer)
        {
            return false;
        }

        if (GeneratorUtilities.HasDefaultSerializerFor(type, elementTypeNameNonNull))
        {
            return false;
        }

        // Fast path only for arrays or List<T> where contiguous memory is guaranteed.
        string? elementSpanExpr = null;
        if (member.CollectionTypeInfo?.IsArray == true)
        {
            elementSpanExpr = $"{collectionAccess}.AsSpan()";
        }
        else if (member.IsList)
        {
            elementSpanExpr = $"System.Runtime.InteropServices.CollectionsMarshal.AsSpan({collectionAccess})";
        }

        if (elementSpanExpr is null)
        {
            return false;
        }

        var bytesVar = $"{member.Name.ToCamelCase()}Bytes";
        sb.WriteLineFormat("var {0} = System.Runtime.InteropServices.MemoryMarshal.AsBytes({1});", bytesVar, elementSpanExpr);

        if (!ctx.IsSpan)
        {
            sb.WriteLineFormat("stream.Write({0});", bytesVar);
        }
        else
        {
            sb.WriteLineFormat("{0}.CopyTo({1});", bytesVar, ctx.Target);
            sb.WriteLineFormat("{0} = {0}.Slice({1}.Length);", ctx.Target, bytesVar);
        }

        return true;
    }

    private static void GenerateStandardCollectionBody
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        string collectionAccess
    )
    {
        if (member.CollectionTypeInfo?.IsArray == true || member.IsList)
        {
            var loopCountExpression = GeneratorUtilities.GetCountExpressionForAccess(member, collectionAccess);
            EmitForLoop(sb, loopCountExpression, (s, indexVar) =>
            {
                if (member.ListTypeArgument is not null)
                {
                    GenerateListElementSerialization
                    (
                        s,
                        member,
                        member.ListTypeArgument.Value,
                        $"{collectionAccess}[{indexVar}]",
                        ctx
                    );
                }
                else if (member.CollectionTypeInfo is not null)
                {
                    GenerateCollectionElementSerialization
                    (
                        s,
                        member,
                        member.CollectionTypeInfo.Value,
                        $"{collectionAccess}[{indexVar}]",
                        ctx
                    );
                }
            });
        }
        else
        {
            EmitForeach(sb, collectionAccess, (s, itemVar) =>
            {
                if (member.CollectionTypeInfo is not null)
                {
                    GenerateCollectionElementSerialization
                    (
                        s,
                        member,
                        member.CollectionTypeInfo.Value,
                        itemVar,
                        ctx
                    );
                }
            });
        }
    }

    private static void HandleNullCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        bool isNotListOrArray
    )
    {
        var isPolymorphicSingleTypeId = GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            && collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId
            && string.IsNullOrEmpty(collectionInfo.TypeIdProperty);

        if (isPolymorphicSingleTypeId && !isNotListOrArray)
        {
            var info = member.PolymorphicInfo!.Value;
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, "0");
            if (PolymorphicUtilities.TryGetDefaultOption(info, out var defaultOption))
            {
                SerializationWriterEmitter.EmitWriteTypeId
                (
                    sb,
                    ctx,
                    defaultOption,
                    info
                );
            }
        }
        else if (!collectionInfo.Unlimited)
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, "0");
        }
    }

    private static void EmitForLoop(IndentedStringBuilder sb, string countExpr, Action<IndentedStringBuilder, string> bodyEmitter)
    {
        sb.WriteLineFormat("for (int i = 0; i < {0}; i++)", countExpr);
        using (sb.BeginBlock())
        {
            bodyEmitter(sb, "i");
        }
    }

    private static void EmitForeach(IndentedStringBuilder sb, string collectionExpr, Action<IndentedStringBuilder, string> bodyEmitter)
    {
        sb.WriteLineFormat("foreach (var item in {0})", collectionExpr);
        using (sb.BeginBlock())
        {
            bodyEmitter(sb, "item");
        }
    }

    private static bool TryEmitCustomSerializerElement(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string elementAccess,
        SerializationWriterEmitter.WriterCtx ctx)
    {
        if (member.CustomSerializer is not { } customSerializer)
        {
            return false;
        }

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

        return true;
    }

    private static void EmitElement(
        IndentedStringBuilder sb,
        string elementAccess,
        SerializationWriterEmitter.WriterCtx ctx,
        string typeName,
        bool hasGenerateSerializerAttribute,
        bool isUnmanagedType,
        bool isStringType)
    {
        if (hasGenerateSerializerAttribute)
        {
            if (ctx.IsSpan)
            {
                sb.WriteLineFormat("{0}.Serialize({1}, ref {2});", TypeHelper.GetSimpleTypeName(typeName), elementAccess, ctx.Target);
            }
            else
            {
                sb.WriteLineFormat("{0}.Serialize({1}, {2});", TypeHelper.GetSimpleTypeName(typeName), elementAccess, ctx.Target);
            }
        }
        else if (isUnmanagedType)
        {
            SerializationWriterEmitter.EmitWrite(sb, ctx, typeName, elementAccess);
        }
        else if (isStringType)
        {
            SerializationWriterEmitter.EmitWriteString(sb, ctx, elementAccess);
        }
    }

    private static void GenerateListElementSerialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        ListTypeArgumentInfo elementInfo,
        string elementAccess,
        SerializationWriterEmitter.WriterCtx ctx
    )
    {
        if (TryEmitCustomSerializerElement(sb, member, elementAccess, ctx))
        {
            return;
        }

        EmitElement
        (
            sb,
            elementAccess,
            ctx,
            elementInfo.TypeName,
            elementInfo.HasGenerateSerializerAttribute,
            elementInfo.IsUnmanagedType,
            elementInfo.IsStringType
        );
    }

    private static void GenerateCollectionElementSerialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        CollectionTypeInfo elementInfo,
        string elementAccess,
        SerializationWriterEmitter.WriterCtx ctx
    )
    {
        if (TryEmitCustomSerializerElement(sb, member, elementAccess, ctx))
        {
            return;
        }

        EmitElement
        (
            sb,
            elementAccess,
            ctx,
            elementInfo.ElementTypeName,
            elementInfo.HasElementGenerateSerializerAttribute,
            elementInfo.IsElementUnmanagedType,
            elementInfo.IsElementStringType
        );
    }

    private static void BeginCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType)
    {
        if (!ctx.IsSpan)
        {
            sb.WriteLine("if (!stream.CanSeek)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("throw new NotSupportedException(\"Stream must be seekable to serialize this collection.\");");
            }

            sb.WriteLine("var countPosition = stream.Position;");
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, "0", " // Placeholder for count");
        }
        else
        {
            sb.WriteLine("var countSpan = data;");
            sb.WriteLine($"data = data.Slice(sizeof({countType}));");
        }
    }

    private static void EndCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType, string countExpr)
    {
        if (!ctx.IsSpan)
        {
            sb.WriteLine("var endPosition = stream.Position;");
            sb.WriteLine("stream.Position = countPosition;");
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, countExpr);
            sb.WriteLine("stream.Position = endPosition;");
        }
        else
        {
            var countCtx = ctx with { Target = "countSpan" };
            SerializationWriterEmitter.EmitWrite(sb, countCtx, countType, countExpr);
        }
    }

    private static void GenerateEnumerableCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        string collectionAccess
    )
    {
        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();

        BeginCountReservation(sb, ctx, countType);

        sb.WriteLine("int count = 0;");
        sb.WriteLineFormat("if ({0} is not null)", collectionAccess);
        using (sb.BeginBlock())
        {
            sb.WriteLine($"using var enumerator = {collectionAccess}.GetEnumerator();");
            sb.WriteLine("while (enumerator.MoveNext())");
            using (sb.BeginBlock())
            {
                var elementInfo = member.CollectionTypeInfo!.Value;
                GenerateCollectionElementSerialization
                (
                    sb,
                    member,
                    elementInfo,
                    "enumerator.Current",
                    ctx
                );
                sb.WriteLine("count++;");
            }
        }

        EndCountReservation(sb, ctx, countType, "count");
    }

    private static void EmitFixedCollection(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx, TypeToGenerate type, string collectionAccess)
    {
        var collectionInfo = member.CollectionInfo ?? throw new InvalidOperationException("Collection attribute is required for fixed collections.");
        sb.WriteLineFormat("if ({0} is null)", collectionAccess);
        using (sb.BeginBlock())
        {
            sb.WriteLineFormat("throw new System.ArgumentNullException(nameof(obj.{0}), \"Fixed-size collections cannot be null.\");", member.Name);
        }

        HandleNonNullCollection
        (
            sb,
            member,
            ctx,
            collectionInfo,
            type,
            collectionAccess
        );
    }

    private static bool TryEmitCountSizeReferenceCase(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx, CollectionInfo collectionInfo, string collectionAccess)
    {
        if (collectionInfo.CountSizeReferenceIndex is not null)
        {
            sb.WriteLineFormat("if ({0} is not null)", collectionAccess);
            using (sb.BeginBlock())
            {
                if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
                {
                    PolymorphicSerializer.GeneratePolymorphicCollection
                    (
                        sb,
                        member,
                        ctx,
                        collectionInfo,
                        collectionAccess
                    );
                }
                else
                {
                    GenerateStandardCollectionBody(sb, member, ctx, collectionAccess);
                }
            }

            return true;
        }
        return false;
    }

    private static bool TryEmitSingleTypeIdPolymorphic(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx, CollectionInfo collectionInfo, string collectionAccess)
    {
        var isNotListOrArray = !member.IsList && member.CollectionTypeInfo?.IsArray != true;
        var useSingleTypeIdPolymorphicSerialization = isNotListOrArray
            && GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            && collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId
            && string.IsNullOrEmpty(collectionInfo.TypeIdProperty);

        if (useSingleTypeIdPolymorphicSerialization)
        {
            if (member.PolymorphicInfo is not { } info)
            {
                return true; // it was handled, but did nothing.
            }

            PolymorphicSerializer.GeneratePolymorphicEnumerableCollection(sb, member, info, ctx, collectionAccess);

            return true;
        }
        return false;
    }

    private static void EmitNullOrNonNullCollection(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx, CollectionInfo collectionInfo, TypeToGenerate type, string collectionAccess)
    {
        var isNotListOrArray = !member.IsList && member.CollectionTypeInfo?.IsArray != true;
        sb.WriteLineFormat("if ({0} is null)", collectionAccess);
        using (sb.BeginBlock())
        {
            HandleNullCollection
            (
                sb,
                member,
                ctx,
                collectionInfo,
                isNotListOrArray
            );
        }

        sb.WriteLine("else");
        using (sb.BeginBlock())
        {
            HandleNonNullCollection
            (
                sb,
                member,
                ctx,
                collectionInfo,
                type,
                collectionAccess
            );
        }
    }

    private static void EmitCountedCollection(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx, TypeToGenerate type, string collectionAccess)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (member.CollectionTypeInfo?.IsPureEnumerable == true && !GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            if (!isByteCollection)
            {
                GenerateEnumerableCollection
                (
                    sb,
                    member,
                    ctx,
                    collectionInfo,
                    collectionAccess
                );
                return;
            }
        }

        if (TryEmitCountSizeReferenceCase(sb, member, ctx, collectionInfo, collectionAccess))
        {
            return;
        }

        if (TryEmitSingleTypeIdPolymorphic(sb, member, ctx, collectionInfo, collectionAccess))
        {
            return;
        }

        if (!member.IsList && member.CollectionTypeInfo?.IsArray != true && member.CollectionTypeInfo?.IsPureEnumerable == true && !isByteCollection)
        {
                GenerateEnumerableCollection
                (
                    sb,
                    member,
                    ctx,
                    collectionInfo,
                    collectionAccess
                );
            return;
        }

        EmitNullOrNonNullCollection(sb, member, ctx, collectionInfo, type, collectionAccess);
    }
}
