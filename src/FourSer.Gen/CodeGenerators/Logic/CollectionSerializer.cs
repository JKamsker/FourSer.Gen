using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Helpers;
using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Models;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Logic;

internal static class CollectionSerializer
{
    private enum CollMode { Fixed, Count }

    public static void Generate(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx, TypeToGenerate type)
        => Generate(sb, member, ctx, type, $"obj.{member.Name}");

    public static void Generate(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        TypeToGenerate type,
        string sourceExpression,
        CollectionPlan? collectionPlan = null)
    {
        var mode = GetCollectionMode(member);
        if (mode == CollMode.Fixed)
        {
            EmitFixedCollection(sb, member, ctx, type, sourceExpression, collectionPlan);
        }
        else
        {
            EmitCountedCollection(sb, member, ctx, type, sourceExpression, collectionPlan);
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
        string sourceExpression,
        CollectionPlan? collectionPlan
    )
    {
        var isHandledByPolymorphic = collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId &&
            string.IsNullOrEmpty(collectionInfo.TypeIdProperty);

        if (collectionInfo.CountSize >= 0)
        {
            var countExpression = GetCountExpression(member, sourceExpression, collectionPlan);
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
            var countExpression = GetCountExpression(member, sourceExpression, collectionPlan);
            SerializationWriterEmitter.EmitCountWrite(sb, ctx, countType, countExpression);
        }

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            PolymorphicSerializer.GeneratePolymorphicCollection
            (
                sb,
                member,
                ctx,
                collectionInfo,
                sourceExpression,
                collectionPlan
            );
            return;
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (isByteCollection && CollectionUtilities.CanUseDirectByteCollectionPath(member))
        {
            SerializationWriterEmitter.EmitWriteBytes(sb, ctx, sourceExpression);
            return;
        }

        if (TryEmitUnmanagedContiguousCollectionWrite(sb, member, ctx, type, sourceExpression))
        {
            return;
        }

        GenerateStandardCollectionBody(sb, member, ctx, sourceExpression);
    }

    private static bool TryEmitUnmanagedContiguousCollectionWrite(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        TypeToGenerate type,
        string sourceExpression)
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
            elementSpanExpr = $"{sourceExpression}.AsSpan()";
        }
        else if (member.IsList)
        {
            elementSpanExpr = $"System.Runtime.InteropServices.CollectionsMarshal.AsSpan({sourceExpression})";
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
        string sourceExpression
    )
    {
        if (member.CollectionTypeInfo?.SupportsIndexing == true || member.IsList)
        {
            var loopCountExpression = GeneratorUtilities.GetCountExpressionForAccess(member, sourceExpression);
            EmitForLoop(sb, loopCountExpression, (s, indexVar) =>
            {
                if (member.ListTypeArgument is not null)
                {
                    GenerateListElementSerialization
                    (
                        s,
                        member,
                        member.ListTypeArgument.Value,
                        $"{sourceExpression}[{indexVar}]",
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
                        $"{sourceExpression}[{indexVar}]",
                        ctx
                    );
                }
            });
        }
        else
        {
            EmitForeach(sb, sourceExpression, (s, itemVar) =>
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
        bool isNotIndexable
    )
    {
        var isPolymorphicSingleTypeId = GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            && collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId
            && string.IsNullOrEmpty(collectionInfo.TypeIdProperty);

        if (isPolymorphicSingleTypeId)
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
        bool isValueType,
        bool hasGenerateSerializerAttribute,
        bool isUnmanagedType,
        bool isStringType)
    {
        if (hasGenerateSerializerAttribute)
        {
            if (isValueType)
            {
                if (ctx.IsSpan)
                {
                    sb.WriteLineFormat("{0}.Serialize({1}, ref {2});", TypeHelper.GetGlobalTypeName(typeName), elementAccess, ctx.Target);
                }
                else
                {
                    sb.WriteLineFormat("{0}.Serialize({1}, {2});", TypeHelper.GetGlobalTypeName(typeName), elementAccess, ctx.Target);
                }
            }
            else
            {
                SerializationWriterEmitter.EmitSerializeNestedOrThrow(sb, ctx, typeName, elementAccess);
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
            elementInfo.IsValueType,
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
            elementInfo.IsElementValueType,
            elementInfo.HasElementGenerateSerializerAttribute,
            elementInfo.IsElementUnmanagedType,
            elementInfo.IsElementStringType
        );
    }

    private static void BeginCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType)
    {
        BeginCountReservation(sb, ctx, countType, "count");
    }

    private static void BeginCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType, string variablePrefix)
    {
        var countPositionVariableName = $"{variablePrefix}CountPosition";
        var countSpanVariableName = $"{variablePrefix}CountSpan";

        if (!ctx.IsSpan)
        {
            sb.WriteLine("if (!stream.CanSeek)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("throw new NotSupportedException(\"Stream must be seekable to serialize this collection.\");");
            }

            sb.WriteLineFormat("var {0} = stream.Position;", countPositionVariableName);
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, "0", " // Placeholder for count");
        }
        else
        {
            sb.WriteLineFormat("var {0} = data;", countSpanVariableName);
            sb.WriteLine($"data = data.Slice(sizeof({countType}));");
        }
    }

    private static void EndCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType, string countExpr)
    {
        EndCountReservation(sb, ctx, countType, countExpr, "count");
    }

    private static void EndCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType, string countExpr, string variablePrefix)
    {
        var countPositionVariableName = $"{variablePrefix}CountPosition";
        var countSpanVariableName = $"{variablePrefix}CountSpan";

        if (!ctx.IsSpan)
        {
            var endPositionVariableName = $"{variablePrefix}EndPosition";
            sb.WriteLineFormat("var {0} = stream.Position;", endPositionVariableName);
            sb.WriteLineFormat("stream.Position = {0};", countPositionVariableName);
            SerializationWriterEmitter.EmitCountWrite(sb, ctx, countType, countExpr);
            sb.WriteLineFormat("stream.Position = {0};", endPositionVariableName);
        }
        else
        {
            var countCtx = ctx with { Target = countSpanVariableName };
            SerializationWriterEmitter.EmitCountWrite(sb, countCtx, countType, countExpr);
        }
    }

    private static bool TryEmitPureEnumerableByteCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        string sourceExpression)
    {
        if (member.CollectionTypeInfo?.IsPureEnumerable != true
            || GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            || collectionInfo.CountSizeReferenceIndex is not null)
        {
            return false;
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        if (!TypeHelper.IsByteCollection(elementTypeName))
        {
            return false;
        }

        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        var collectionVariableName = $"{member.Name.ToCamelCase()}Collection";
        var readOnlyCollectionVariableName = $"{member.Name.ToCamelCase()}ReadOnlyCollection";
        var sequenceVariableName = $"{member.Name.ToCamelCase()}Sequence";

        void EmitCountAndWrite(string collectionExpression, string countExpression)
        {
            SerializationWriterEmitter.EmitCountWrite(sb, ctx, countType, countExpression);
            SerializationWriterEmitter.EmitWriteBytes(sb, ctx, collectionExpression);
        }

        void EmitCountedSequenceBody()
        {
            sb.WriteLineFormat("if ({0} is System.Collections.Generic.ICollection<byte> {1})", sourceExpression, collectionVariableName);
            using (sb.BeginBlock())
            {
                EmitCountAndWrite(collectionVariableName, $"{collectionVariableName}.Count");
            }

            sb.WriteLineFormat("else if ({0} is System.Collections.Generic.IReadOnlyCollection<byte> {1})", sourceExpression, readOnlyCollectionVariableName);
            using (sb.BeginBlock())
            {
                EmitCountAndWrite(readOnlyCollectionVariableName, $"{readOnlyCollectionVariableName}.Count");
            }

            sb.WriteLine("else");
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat("var {0} = global::System.Linq.Enumerable.ToArray({1});", sequenceVariableName, sourceExpression);
                EmitCountAndWrite(sequenceVariableName, $"{sequenceVariableName}.Length");
            }
        }

        if (member.CollectionTypeInfo?.CanBeNull == true)
        {
            sb.WriteLineFormat("if ({0} is null)", sourceExpression);
            using (sb.BeginBlock())
            {
                SerializationWriterEmitter.EmitWrite(sb, ctx, countType, "0");
            }

            sb.WriteLine("else");
            using (sb.BeginBlock())
            {
                EmitCountedSequenceBody();
            }
        }
        else
        {
            EmitCountedSequenceBody();
        }

        return true;
    }

    private static void GenerateEnumerableCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        string sourceExpression
    )
    {
        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        var variablePrefix = member.Name.ToCamelCase();
        var countVariableName = $"{variablePrefix}Count";

        BeginCountReservation(sb, ctx, countType, variablePrefix);

        sb.WriteLineFormat("int {0} = 0;", countVariableName);
        if (member.CollectionTypeInfo?.CanBeNull == true)
        {
            sb.WriteLineFormat("if ({0} is not null)", sourceExpression);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat("foreach (var item in {0})", sourceExpression);
                using (sb.BeginBlock())
                {
                    var elementInfo = member.CollectionTypeInfo!.Value;
                    GenerateCollectionElementSerialization
                    (
                        sb,
                        member,
                        elementInfo,
                        "item",
                        ctx
                    );
                    sb.WriteLineFormat("{0}++;", countVariableName);
                }
            }
        }
        else
        {
            sb.WriteLineFormat("foreach (var item in {0})", sourceExpression);
            using (sb.BeginBlock())
            {
                var elementInfo = member.CollectionTypeInfo!.Value;
                GenerateCollectionElementSerialization
                (
                    sb,
                    member,
                    elementInfo,
                    "item",
                    ctx
                );
                sb.WriteLineFormat("{0}++;", countVariableName);
            }
        }

        EndCountReservation(sb, ctx, countType, countVariableName, variablePrefix);
    }

    private static void EmitFixedCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        TypeToGenerate type,
        string sourceExpression,
        CollectionPlan? collectionPlan)
    {
        var collectionInfo = member.CollectionInfo ?? throw new InvalidOperationException("Collection attribute is required for fixed collections.");
        if (member.CollectionTypeInfo?.CanBeNull != false)
        {
            sb.WriteLineFormat("if ({0} is null)", sourceExpression);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat("throw new System.ArgumentNullException(nameof(obj.{0}), \"Fixed-size collections cannot be null.\");", member.Name);
            }
        }

        HandleNonNullCollection
        (
            sb,
            member,
            ctx,
            collectionInfo,
            type,
            sourceExpression,
            collectionPlan
        );
    }

    private static bool TryEmitCountSizeReferenceCase(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        string sourceExpression,
        CollectionPlan? collectionPlan)
    {
        if (collectionInfo.CountSizeReferenceIndex is not null)
        {
            if (member.CollectionTypeInfo?.CanBeNull == true)
            {
                sb.WriteLineFormat("if ({0} is not null)", sourceExpression);
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
                            sourceExpression,
                            collectionPlan
                        );
                    }
                    else
                    {
                        GenerateStandardCollectionBody(sb, member, ctx, sourceExpression);
                    }
                }
            }
            else if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
            {
                PolymorphicSerializer.GeneratePolymorphicCollection
                (
                    sb,
                    member,
                    ctx,
                    collectionInfo,
                    sourceExpression,
                    collectionPlan
                );
            }
            else
            {
                GenerateStandardCollectionBody(sb, member, ctx, sourceExpression);
            }

            return true;
        }
        return false;
    }

    private static void EmitNullOrNonNullCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        TypeToGenerate type,
        string sourceExpression,
        CollectionPlan? collectionPlan)
    {
        if (member.CollectionTypeInfo?.CanBeNull == false)
        {
            HandleNonNullCollection
            (
                sb,
                member,
                ctx,
                collectionInfo,
                type,
                sourceExpression,
                collectionPlan
            );
            return;
        }

        var isNotIndexable = member.CollectionTypeInfo?.SupportsIndexing != true && !member.IsList;
        sb.WriteLineFormat("if ({0} is null)", sourceExpression);
        using (sb.BeginBlock())
        {
            HandleNullCollection
            (
                sb,
                member,
                ctx,
                collectionInfo,
                isNotIndexable
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
                sourceExpression,
                collectionPlan
            );
        }
    }

    private static void EmitCountedCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        TypeToGenerate type,
        string sourceExpression,
        CollectionPlan? collectionPlan)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (TryEmitPureEnumerableByteCollection(sb, member, ctx, collectionInfo, sourceExpression))
        {
            return;
        }

        if (TryEmitCountSizeReferenceCase(sb, member, ctx, collectionInfo, sourceExpression, collectionPlan))
        {
            return;
        }

        if (member.CollectionTypeInfo?.IsPureEnumerable == true
            && !GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            && IsOriginalMemberAccess(member, sourceExpression))
        {
            if (!isByteCollection)
            {
                GenerateEnumerableCollection
                (
                    sb,
                    member,
                    ctx,
                    collectionInfo,
                    sourceExpression
                );
                return;
            }
        }

        if (!member.IsList
            && member.CollectionTypeInfo?.IsArray != true
            && member.CollectionTypeInfo?.IsPureEnumerable == true
            && !isByteCollection
            && !GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            && IsOriginalMemberAccess(member, sourceExpression))
        {
            GenerateEnumerableCollection
            (
                sb,
                member,
                ctx,
                collectionInfo,
                sourceExpression
            );
            return;
        }

        EmitNullOrNonNullCollection(sb, member, ctx, collectionInfo, type, sourceExpression, collectionPlan);
    }

    private static bool IsOriginalMemberAccess(MemberToGenerate member, string sourceExpression)
    {
        return string.Equals(sourceExpression, $"obj.{member.Name}", StringComparison.Ordinal);
    }

    private static string GetCountExpression(
        MemberToGenerate member,
        string sourceExpression,
        CollectionPlan? collectionPlan)
    {
        if (collectionPlan is { CollectionInfo.CountSizeReferenceIndex: not null, CachedCountLocalName: not null } cachedPlan)
        {
            return cachedPlan.CachedCountLocalName;
        }

        return GeneratorUtilities.GetCountExpressionForAccess(member, sourceExpression);
    }
}
