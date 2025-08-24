using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Helpers;
using FourSer.Gen.Models;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Logic;

internal static class CollectionSerializer
{
    private enum CollMode { Fixed, Count }

    public static void Generate(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx)
    {
        var mode = GetCollectionMode(member);
        if (mode == CollMode.Fixed)
        {
            EmitFixedCollection(sb, member, ctx);
        }
        else
        {
            EmitCountedCollection(sb, member, ctx);
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
        CollectionInfo collectionInfo
    )
    {
        var isHandledByPolymorphic = collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId &&
            string.IsNullOrEmpty(collectionInfo.TypeIdProperty);

        if (collectionInfo.CountSize >= 0)
        {
            var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
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
            var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, countExpression);
        }

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            PolymorphicSerializer.GeneratePolymorphicCollection
            (
                sb,
                member,
                ctx,
                collectionInfo
            );
            return;
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (isByteCollection)
        {
            SerializationWriterEmitter.EmitWriteBytes(sb, ctx, $"obj.{member.Name}");
        }
        else
        {
            GenerateStandardCollectionBody(sb, member, ctx);
        }
    }

    private static void GenerateStandardCollectionBody
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx
    )
    {
        if (member.CollectionTypeInfo?.IsArray == true || member.IsList)
        {
            var loopCountExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
            EmitForLoop(sb, loopCountExpression, (s, indexVar) =>
            {
                if (member.ListTypeArgument is not null)
                {
                    GenerateListElementSerialization
                    (
                        s,
                        member.ListTypeArgument.Value,
                        $"obj.{member.Name}[{indexVar}]",
                        ctx
                    );
                }
                else if (member.CollectionTypeInfo is not null)
                {
                    GenerateCollectionElementSerialization
                    (
                        s,
                        member.CollectionTypeInfo.Value,
                        $"obj.{member.Name}[{indexVar}]",
                        ctx
                    );
                }
            });
        }
        else
        {
            EmitForeach(sb, $"obj.{member.Name}", (s, itemVar) =>
            {
                if (member.CollectionTypeInfo is not null)
                {
                    GenerateCollectionElementSerialization
                    (
                        s,
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
            var defaultOption = info.Options.FirstOrDefault();
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, "0");
            SerializationWriterEmitter.EmitWriteTypeId
            (
                sb,
                ctx,
                defaultOption,
                info
            );
        }
        else if (collectionInfo.CountType != null || collectionInfo.CountSizeReferenceIndex is not null)
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, "0");
        }
        else
        {
            var countType = TypeHelper.GetDefaultCountType();
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
                sb.WriteLineFormat
                (
                    "var bytesWritten = {0}.Serialize({1}, {2});",
                    TypeHelper.GetSimpleTypeName(typeName),
                    elementAccess,
                    ctx.Target
                );
                sb.WriteLine($"{ctx.Target} = {ctx.Target}.Slice(bytesWritten);");
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
        ListTypeArgumentInfo elementInfo,
        string elementAccess,
        SerializationWriterEmitter.WriterCtx ctx
    )
    {
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
        CollectionTypeInfo elementInfo,
        string elementAccess,
        SerializationWriterEmitter.WriterCtx ctx
    )
    {
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
        CollectionInfo collectionInfo
    )
    {
        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();

        BeginCountReservation(sb, ctx, countType);

        sb.WriteLine("int count = 0;");
        sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
        using (sb.BeginBlock())
        {
            sb.WriteLine($"using var enumerator = obj.{member.Name}.GetEnumerator();");
            sb.WriteLine("while (enumerator.MoveNext())");
            using (sb.BeginBlock())
            {
                var elementInfo = member.CollectionTypeInfo!.Value;
                EmitElement
                (
                    sb,
                    "enumerator.Current",
                    ctx,
                    elementInfo.ElementTypeName,
                    elementInfo.HasElementGenerateSerializerAttribute,
                    elementInfo.IsElementUnmanagedType,
                    elementInfo.IsElementStringType
                );
                sb.WriteLine("count++;");
            }
        }

        EndCountReservation(sb, ctx, countType, "count");
    }

    private static void EmitFixedCollection(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx)
    {
        sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
        using (sb.BeginBlock())
        {
            sb.WriteLineFormat("throw new System.ArgumentNullException(nameof(obj.{0}), \"Fixed-size collections cannot be null.\");", member.Name);
        }

        HandleNonNullCollection
        (
            sb,
            member,
            ctx,
            member.CollectionInfo.Value
        );
    }

    private static bool TryEmitCountSizeReferenceCase(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx, CollectionInfo collectionInfo)
    {
        if (collectionInfo.CountSizeReferenceIndex is not null)
        {
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using (sb.BeginBlock())
            {
                if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
                {
                    PolymorphicSerializer.GeneratePolymorphicCollection
                    (
                        sb,
                        member,
                        ctx,
                        collectionInfo
                    );
                }
                else
                {
                    GenerateStandardCollectionBody(sb, member, ctx);
                }
            }

            return true;
        }
        return false;
    }

    private static bool TryEmitSingleTypeIdPolymorphic(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx, CollectionInfo collectionInfo)
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

            PolymorphicSerializer.GeneratePolymorphicEnumerableCollection(sb, member, info, ctx);

            return true;
        }
        return false;
    }

    private static void EmitNullOrNonNullCollection(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx, CollectionInfo collectionInfo)
    {
        var isNotListOrArray = !member.IsList && member.CollectionTypeInfo?.IsArray != true;
        sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
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
                collectionInfo
            );
        }
    }

    private static void EmitCountedCollection(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx)
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
                    collectionInfo
                );
                return;
            }
        }

        if (TryEmitCountSizeReferenceCase(sb, member, ctx, collectionInfo))
        {
            return;
        }

        if (TryEmitSingleTypeIdPolymorphic(sb, member, ctx, collectionInfo))
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
                collectionInfo
            );
            return;
        }

        EmitNullOrNonNullCollection(sb, member, ctx, collectionInfo);
    }
}
