using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
///     Generates Serialize method implementations
/// </summary>
public static class SerializationGenerator
{
    private readonly record struct WriterCtx(string Target, string Helper, bool IsSpan)
    {
        public string Ref => IsSpan ? "ref " : "";
    }

    private static WriterCtx SpanCtx => new("data", "SpanWriter", true);
    private static WriterCtx StreamCtx => new("stream", "StreamWriter", false);

    private enum CollMode
    {
        Fixed,
        Count,
        Terminated
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
        sb.WriteLine("var originalData = data;");

        GenerateSerializationBody(sb, typeToGenerate, SpanCtx);

        sb.WriteLine("return originalData.Length - data.Length;");
    }

    private static void GenerateStreamSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLineFormat("public static void Serialize({0} obj, System.IO.Stream stream)", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        GenerateSerializationBody(sb, typeToGenerate, StreamCtx);
    }

    private static void GenerateSerializationBody
    (
        IndentedStringBuilder sb,
        TypeToGenerate typeToGenerate,
        WriterCtx ctx
    )
    {
        GenerateTypeIdPrePass(sb, typeToGenerate);

        for (var i = 0; i < typeToGenerate.Members.Count; i++)
        {
            var member = typeToGenerate.Members[i];
            if (member.IsCountSizeReferenceFor is not null)
            {
                var collectionMember = typeToGenerate.Members[member.IsCountSizeReferenceFor.Value];
                if (collectionMember.CollectionTypeInfo?.IsPureEnumerable != true)
                {
                    var collectionName = collectionMember.Name;
                    var countExpression = $"obj.{collectionName}?.Count ?? 0";
                    var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
                    EmitWrite(sb, ctx, ctx.Target, member.TypeName, countExpression, ctx.IsSpan);
                }
            }
            else if (member.IsTypeIdPropertyFor is not null)
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
                        EmitWrite(sb, ctx, ctx.Target, typeIdType, defaultKey, ctx.IsSpan);
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

                        EmitWrite(sb, ctx, ctx.Target, typeIdType, "discriminator", ctx.IsSpan);
                    }
                }
                else
                {
                GenerateMemberSerialization(sb, member, ctx);
                }
            }
            else
            {
            GenerateMemberSerialization(sb, member, ctx);
            }
        }
    }

    private static void EmitSerializeNestedOrThrow(
        IndentedStringBuilder sb,
        string typeName,
        string instanceName,
        WriterCtx ctx
    )
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

    private static void GenerateMemberSerialization
        (IndentedStringBuilder sb, MemberToGenerate member, WriterCtx ctx)
    {
        if (member.IsList || member.IsCollection)
        {
            GenerateCollectionSerialization(sb, member, ctx);
        }
        else if (member.PolymorphicInfo is not null)
        {
            GeneratePolymorphicSerialization(sb, member, ctx);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            EmitSerializeNestedOrThrow(sb, member.TypeName, $"obj.{member.Name}", ctx);
        }
        else if (member.IsStringType)
        {
            EmitWriteString(sb, ctx, ctx.Target, $"obj.{member.Name}", ctx.IsSpan);
        }
        else if (member.IsUnmanagedType)
        {
            EmitWrite(sb, ctx, ctx.Target, member.TypeName, $"obj.{member.Name}", ctx.IsSpan);
        }
    }

    private static void GenerateCollectionSerialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx
    )
    {
        var mode = GetCollectionMode(member);
        EmitSerializeForCollection(sb, member, ctx, mode);
    }

    private static void HandleNonNullCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
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
            EmitWrite(sb, ctx, ctx.Target, countType, countExpression, ctx.IsSpan);
        }

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            GeneratePolymorphicCollectionBody
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
            EmitWriteBytes(sb, ctx, ctx.Target, $"obj.{member.Name}", ctx.IsSpan);
        }
        else
        {
            GenerateStandardCollectionBody(sb, member, ctx);
        }
    }

    private static void GeneratePolymorphicCollectionBody
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
        CollectionInfo collectionInfo
    )
    {
        if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
        {
            GenerateIndividualTypeIdCollection(sb, member, ctx);
        }
        else if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
        {
            GenerateSingleTypeIdPolymorphicCollection
            (
                sb,
                member,
                ctx,
                collectionInfo
            );
        }
    }

    private static void GenerateSingleTypeIdPolymorphicCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
        CollectionInfo collectionInfo
    )
    {
        if (member.PolymorphicInfo is not { } info)
        {
            return;
        }

        if (info.TypeIdPropertyIndex is null)
        {
            GenerateSingleTypeIdPolymorphicCollectionImplicit
            (
                sb,
                member,
                ctx,
                collectionInfo,
                info
            );
        }
        else
        {
            GenerateSingleTypeIdPolymorphicCollectionWithProperty
            (
                sb,
                member,
                ctx,
                info
            );
        }
    }

    private static void EmitDiscriminatorFromFirstItem(
        IndentedStringBuilder sb,
        WriterCtx ctx,
        string collectionName,
        PolymorphicInfo info,
        string discriminatorVarName)
    {
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        sb.WriteLine($"var firstItem = {collectionName}[0];");
        sb.WriteLine($"var {discriminatorVarName} = firstItem switch");
        sb.WriteLine("{");
        sb.Indent();
        foreach (var option in info.Options)
        {
            var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
            sb.WriteLineFormat("{0} => ({1}){2},", TypeHelper.GetSimpleTypeName(option.Type), typeIdType, key);
        }

        sb.WriteLine($"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{firstItem.GetType().Name}}\")");
        sb.Unindent();
        sb.WriteLine("};");

        EmitWrite(sb, ctx, ctx.Target, typeIdType, discriminatorVarName, ctx.IsSpan);
    }

    private static void EmitNullOrEmptyCollectionHeader(
        IndentedStringBuilder sb,
        WriterCtx ctx,
        CollectionInfo collectionInfo,
        PolymorphicInfo polymorphicInfo)
    {
        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        EmitWrite(sb, ctx, ctx.Target, countType, "0", ctx.IsSpan);

        if (polymorphicInfo.TypeIdPropertyIndex is null)
        {
            var defaultOption = polymorphicInfo.Options.FirstOrDefault();
            if (!defaultOption.Equals(default(PolymorphicOption)))
            {
                EmitWriteTypeId(sb, ctx, defaultOption, polymorphicInfo);
            }
        }
    }

    private static void GenerateSingleTypeIdPolymorphicCollectionWithProperty
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
        PolymorphicInfo info
    )
    {
        sb.WriteLineFormat($"if (obj.{member.Name}.Count > 0)");
        using (sb.BeginBlock())
        {
            sb.WriteLineFormat("switch (obj.{0}[0])", member.Name);
            using (sb.BeginBlock())
            {
                foreach (var option in info.Options)
                {
                    var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                    sb.WriteLineFormat("case {0}:", typeName);
                    using (sb.BeginBlock())
                    {
                        sb.WriteLineFormat("foreach(var item in obj.{0})", member.Name);
                        using (sb.BeginBlock())
                        {
                            if (ctx.IsSpan)
                            {
                                sb.WriteLineFormat
                                (
                                    "var bytesWritten = {0}.Serialize(({0})item, {1});",
                                    typeName,
                                    ctx.Target
                                );
                                sb.WriteLine($"{ctx.Target} = {ctx.Target}.Slice(bytesWritten);");
                            }
                            else
                            {
                                sb.WriteLineFormat
                                (
                                    "{0}.Serialize(({0})item, {1});",
                                    typeName,
                                    ctx.Target
                                );
                            }
                        }

                        sb.WriteLine("break;");
                    }
                }

                sb.WriteLine("default:");
                sb.WriteLineFormat
                (
                    "    throw new System.IO.InvalidDataException($\"Unknown type for item in {0}: {{obj.{0}[0].GetType().Name}}\");",
                    member.Name
                );
            }
        }
    }

    private static void GenerateSingleTypeIdPolymorphicCollectionImplicit
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
        CollectionInfo collectionInfo,
        PolymorphicInfo info
    )
    {
        var listItemsVar = member.Name.ToCamelCase();
        if (listItemsVar == "items")
        {
            listItemsVar = "collectionItems"; // Avoid conflict with loop variable
        }

        sb.WriteLine($"var {listItemsVar} = obj.{member.Name};");

        var defaultOption = info.Options.FirstOrDefault();
        if (defaultOption.Equals(default(PolymorphicOption)))
        {
            return;
        }

        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;

        sb.WriteLine($"if ({listItemsVar} is null || {listItemsVar}.Count == 0)");
        using (sb.BeginBlock())
        {
            EmitNullOrEmptyCollectionHeader(sb, ctx, collectionInfo, info);
        }

        sb.WriteLine("else");
        using (sb.BeginBlock())
        {
            var countExpression = $"{listItemsVar}.Count";
            EmitWrite(sb, ctx, ctx.Target, countType, countExpression, ctx.IsSpan);

            EmitDiscriminatorFromFirstItem(sb, ctx, listItemsVar, info, "discriminator");

            sb.WriteLine("switch (discriminator)");
            using (sb.BeginBlock())
            {
                foreach (var option in info.Options)
                {
                    var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                    var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                    sb.WriteLine($"case {key}:");
                    using (sb.BeginBlock())
                    {
                        sb.WriteLine($"for (int i = 0; i < {countExpression}; i++)");
                        using (sb.BeginBlock())
                        {
                            if (ctx.IsSpan)
                            {
                                sb.WriteLine($"var bytesWritten = {typeName}.Serialize(({typeName}){listItemsVar}[i], {ctx.Target});");
                                sb.WriteLine($"{ctx.Target} = {ctx.Target}.Slice(bytesWritten);");
                            }
                            else
                            {
                                sb.WriteLine($"{typeName}.Serialize(({typeName}){listItemsVar}[i], {ctx.Target});");
                            }
                        }

                        sb.WriteLine("break;");
                    }
                }
            }
        }
    }

    private static void GenerateIndividualTypeIdCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx
    )
    {
        sb.WriteLineFormat("foreach(var item in obj.{0})", member.Name);
        using var __ = sb.BeginBlock();
        var itemMember = new MemberToGenerate
        (
            "item",
            member.ListTypeArgument!.Value.TypeName,
            member.ListTypeArgument.Value.IsUnmanagedType,
            member.ListTypeArgument.Value.IsStringType,
            member.ListTypeArgument.Value.HasGenerateSerializerAttribute,
            false,
            null,
            null,
            member.PolymorphicInfo,
            false,
            null,
            false,
            false,
            null,
            null
        );

        GeneratePolymorphicItemSerialization
        (
            sb,
            itemMember,
            "item",
            ctx
        );
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

    private static void GenerateStandardCollectionBody
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx
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
        WriterCtx ctx,
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
            EmitWrite(sb, ctx, ctx.Target, countType, "0", ctx.IsSpan);
            EmitWriteTypeId
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
            EmitWrite(sb, ctx, ctx.Target, countType, "0", ctx.IsSpan);
        }
        else
        {
            var countType = TypeHelper.GetDefaultCountType();
            EmitWrite(sb, ctx, ctx.Target, countType, "0", ctx.IsSpan);

            // sb.WriteLineFormat
            //     ("throw new System.NullReferenceException($\"Collection \\\"{0}\\\" cannot be null.\");", member.Name);
        }
    }

    private static void GeneratePolymorphicSerialization
        (IndentedStringBuilder sb, MemberToGenerate member, WriterCtx ctx)
    {
        var info = member.PolymorphicInfo!.Value;

        sb.WriteLineFormat("switch (obj.{0})", member.Name);
        using var _ = sb.BeginBlock();

        GeneratePolymorphicSerializationLogic
        (
            sb,
            ctx,
            info,
            false
        );

        sb.WriteLine("case null:");
        sb.WriteLineFormat
            ("    throw new System.NullReferenceException($\"Property \\\"{0}\\\" cannot be null.\");", member.Name);
        sb.WriteLine("default:");
        sb.WriteLineFormat
        (
            "    throw new System.IO.InvalidDataException($\"Unknown type for {0}: {{obj.{0}?.GetType().FullName}}\");",
            member.Name
        );
    }

    private static void GeneratePolymorphicItemSerialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string instanceName,
        WriterCtx ctx
    )
    {
        var info = member.PolymorphicInfo!.Value;

        sb.WriteLineFormat("switch ({0})", instanceName);
        using var _ = sb.BeginBlock();

        GeneratePolymorphicSerializationLogic
        (
            sb,
            ctx,
            info,
            true
        );

        sb.WriteLine("case null:");
        sb.WriteLine("    throw new System.NullReferenceException($\"Item in collection cannot be null.\");");
        sb.WriteLine("default:");
        sb.WriteLine
        (
            $"    throw new System.IO.InvalidDataException($\"Unknown type for {instanceName}: {{{instanceName}?.GetType().FullName}}\");"
        );
    }

    private static void GeneratePolymorphicSwitch(
        IndentedStringBuilder sb,
        PolymorphicInfo info,
        Action<IndentedStringBuilder, PolymorphicOption> caseEmitter)
    {
        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.WriteLineFormat("case {0} typedInstance:", typeName);
            using (sb.BeginBlock())
            {
                caseEmitter(sb, option);
                sb.WriteLine("break;");
            }
        }
    }

    private static void GeneratePolymorphicSerializationLogic
    (
        IndentedStringBuilder sb,
        WriterCtx ctx,
        PolymorphicInfo info,
        bool isItem
    )
    {
        GeneratePolymorphicSwitch(sb, info, (s, option) =>
        {
            if (info.TypeIdPropertyIndex is null)
            {
                EmitWriteTypeId
                (
                    s,
                    ctx,
                    option,
                    info
                );
            }

            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            EmitSerializeNestedOrThrow(s, typeName, "typedInstance", ctx);
        });
    }

    private static void EmitElement(
        IndentedStringBuilder sb,
        string elementAccess,
        WriterCtx ctx,
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
            EmitWrite(sb, ctx, ctx.Target, typeName, elementAccess, ctx.IsSpan);
        }
        else if (isStringType)
        {
            EmitWriteString(sb, ctx, ctx.Target, elementAccess, ctx.IsSpan);
        }
    }

    private static void GenerateListElementSerialization
    (
        IndentedStringBuilder sb,
        ListTypeArgumentInfo elementInfo,
        string elementAccess,
        WriterCtx ctx
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
        WriterCtx ctx
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

    private static void BeginCountReservation(IndentedStringBuilder sb, WriterCtx ctx, string countType)
    {
        if (!ctx.IsSpan)
        {
            sb.WriteLine("if (!stream.CanSeek)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("throw new NotSupportedException(\"Stream must be seekable to serialize this collection.\");");
            }

            sb.WriteLine("var countPosition = stream.Position;");
            EmitWrite(sb, ctx, "stream", countType, "0", false, " // Placeholder for count");
        }
        else
        {
            sb.WriteLine("var countSpan = data;");
            sb.WriteLine($"data = data.Slice(sizeof({countType}));");
        }
    }

    private static void EndCountReservation(IndentedStringBuilder sb, WriterCtx ctx, string countType, string countExpr)
    {
        if (!ctx.IsSpan)
        {
            sb.WriteLine("var endPosition = stream.Position;");
            sb.WriteLine("stream.Position = countPosition;");
            EmitWrite(sb, ctx, "stream", countType, countExpr, false);
            sb.WriteLine("stream.Position = endPosition;");
        }
        else
        {
            EmitWrite(sb, ctx, "countSpan", countType, countExpr, true);
        }
    }

    private static void GenerateEnumerableCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
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

    private static void GenerateSpanPolymorphicCollectionSerialization
        (IndentedStringBuilder sb, MemberToGenerate member, PolymorphicInfo info, CollectionInfo collectionInfo)
    {
        GeneratePolymorphicCollection
        (
            sb,
            member,
            info,
            SpanCtx
        );
    }

    private static void GenerateStreamPolymorphicCollectionSerialization
        (IndentedStringBuilder sb, MemberToGenerate member, PolymorphicInfo info, CollectionInfo collectionInfo)
    {
        GeneratePolymorphicCollection
        (
            sb,
            member,
            info,
            StreamCtx
        );
    }

    private static void GeneratePolymorphicCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        PolymorphicInfo info,
        WriterCtx ctx
    )
    {
        var defaultOption = info.Options.FirstOrDefault();
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;

        BeginCountReservation(sb, ctx, "int");

        sb.WriteLine("int count = 0;");
        sb.WriteLine($"using var enumerator = obj.{member.Name}.GetEnumerator();");

        sb.WriteLine("if (!enumerator.MoveNext())");
        sb.WriteLine("{");
        sb.Indent();
        EmitWriteTypeId
        (
            sb,
            ctx,
            defaultOption,
            info
        );
        EndCountReservation(sb, ctx, "int", "0");
        sb.Unindent();
        sb.WriteLine("}");
        sb.WriteLine("else");
        sb.WriteLine("{");
        sb.Indent();
        sb.WriteLine("var discriminator = enumerator.Current switch");
        sb.WriteLine("{");
        sb.Indent();
        foreach (var option in info.Options)
        {
            var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
            sb.WriteLineFormat($"{TypeHelper.GetSimpleTypeName(option.Type)} => ({typeIdType}){key},");
        }

        sb.WriteLine
            ($"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{enumerator.Current.GetType().Name}}\")");
        sb.Unindent();
        sb.WriteLine("};");

        EmitWrite(sb, ctx, ctx.Target, typeIdType, "discriminator", ctx.IsSpan);

        sb.WriteLine("switch (discriminator)");
        using (sb.BeginBlock())
        {
            foreach (var option in info.Options)
            {
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                sb.WriteLine($"case {key}:");
                using (sb.BeginBlock())
                {
                    sb.WriteLine("do");
                    using (sb.BeginBlock())
                    {
                        if (ctx.IsSpan)
                        {
                            sb.WriteLine($"var bytesWritten = {typeName}.Serialize(({typeName})enumerator.Current, data);");
                            sb.WriteLine("data = data.Slice(bytesWritten);");
                        }
                        else
                        {
                            sb.WriteLine($"{typeName}.Serialize(({typeName})enumerator.Current, stream);");
                        }

                        sb.WriteLine("count++;");
                    }

                    sb.WriteLine("while (enumerator.MoveNext());");

                    sb.WriteLine("break;");
                }
            }
        }
        EndCountReservation(sb, ctx, "int", "count");
        sb.Unindent();
        sb.WriteLine("}");
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

            sb.WriteLineFormat("switch (obj.{0})", member.Name);
            using var __ = sb.BeginBlock();
            foreach (var option in info.Options)
            {
                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                var key = option.Key.ToString();
                if (info.EnumUnderlyingType is not null)
                {
                    key = $"({info.TypeIdType}){key}";
                }
                else if (info.TypeIdType.EndsWith("Enum"))
                {
                    key = $"{info.TypeIdType}.{key}";
                }

                sb.WriteLineFormat("case {0}:", typeName);
                sb.WriteLineFormat("    obj.{0} = {1};", referencedMember.Name, key);
                sb.WriteLine("    break;");
            }

            sb.WriteLine("case null:");
            sb.WriteLine("    break;");
        }
    }

    private static void EmitWrite(IndentedStringBuilder sb, WriterCtx ctx, string target, string typeName, string value, bool useRef, string comment = "")
    {
        var refOrEmpty = useRef ? "ref " : "";
        var friendlyTypeName = TypeHelper.GetMethodFriendlyTypeName(typeName);
        var writeMethod = $"Write{friendlyTypeName}";
        // Generates a call to the appropriate writer method, e.g., SpanWriter.WriteInt32(ref data, (int)myValue);
        sb.WriteLineFormat
        (
            "{0}.{1}({2}{3}, ({4})({5}));{6}",
            ctx.Helper,
            writeMethod,
            refOrEmpty,
            target,
            typeName,
            value,
            comment
        );
    }

    private static void EmitWriteString(IndentedStringBuilder sb, WriterCtx ctx, string target, string value, bool useRef)
    {
        var refOrEmpty = useRef ? "ref " : "";
        sb.WriteLineFormat
        (
            "{0}.WriteString({1}{2}, {3});",
            ctx.Helper,
            refOrEmpty,
            target,
            value
        );
    }

    private static void EmitWriteBytes(IndentedStringBuilder sb, WriterCtx ctx, string target, string value, bool useRef)
    {
        var refOrEmpty = useRef ? "ref " : "";
        sb.WriteLineFormat
        (
            "{0}.WriteBytes({1}{2}, {3});",
            ctx.Helper,
            refOrEmpty,
            target,
            value
        );
    }

    private static void EmitFixedCollection(IndentedStringBuilder sb, MemberToGenerate member, WriterCtx ctx)
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

    private static bool TryEmitCountSizeReferenceCase(IndentedStringBuilder sb, MemberToGenerate member, WriterCtx ctx, CollectionInfo collectionInfo)
    {
        if (collectionInfo.CountSizeReferenceIndex is not null)
        {
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using (sb.BeginBlock())
            {
                if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
                {
                    GeneratePolymorphicCollectionBody
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

    private static bool TryEmitSingleTypeIdPolymorphic(IndentedStringBuilder sb, MemberToGenerate member, WriterCtx ctx, CollectionInfo collectionInfo)
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

            if (ctx.IsSpan)
            {
                GenerateSpanPolymorphicCollectionSerialization(sb, member, info, collectionInfo);
            }
            else
            {
                GenerateStreamPolymorphicCollectionSerialization(sb, member, info, collectionInfo);
            }

            return true;
        }
        return false;
    }

    private static void EmitNullOrNonNullCollection(IndentedStringBuilder sb, MemberToGenerate member, WriterCtx ctx, CollectionInfo collectionInfo)
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

    private static void EmitCountedCollection(IndentedStringBuilder sb, MemberToGenerate member, WriterCtx ctx)
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

    private static void EmitSerializeForCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
        CollMode mode)
    {
        if (mode == CollMode.Fixed)
        {
            EmitFixedCollection(sb, member, ctx);
            return;
        }

        EmitCountedCollection(sb, member, ctx);
    }

    private static void EmitWriteTypeId
    (
        IndentedStringBuilder sb,
        WriterCtx ctx,
        PolymorphicOption option,
        PolymorphicInfo info
    )
    {
        var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
        var underlyingType = info.EnumUnderlyingType ?? info.TypeIdType;
        EmitWrite(sb, ctx, ctx.Target, underlyingType, key, ctx.IsSpan);
    }
}
