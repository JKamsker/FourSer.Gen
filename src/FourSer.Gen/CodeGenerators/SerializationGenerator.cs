using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using System;
using System.Diagnostics.CodeAnalysis;

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

        EmitSerializeMembers(sb, typeToGenerate, SpanCtx);

        sb.WriteLine("return originalData.Length - data.Length;");
    }

    private static void GenerateStreamSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLineFormat("public static void Serialize({0} obj, System.IO.Stream stream)", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        EmitSerializeMembers(sb, typeToGenerate, StreamCtx);
    }

    private static void EmitSerializeMembers
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
                    var writeMethod = $"Write{typeName}";
                    sb.WriteLineFormat
                    (
                        "{0}.{1}({2}{3}, ({4})({5}));",
                        ctx.Helper,
                        writeMethod,
                        ctx.Ref,
                        ctx.Target,
                        typeName,
                        countExpression
                    );
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
                    var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
                    var writeMethod = $"Write{typeIdTypeName}";

                    var defaultOption = info.Options.FirstOrDefault();
                    var defaultKey = PolymorphicUtilities.FormatTypeIdKey(defaultOption.Key, info);

                    sb.WriteLineFormat($"if (obj.{collectionName} is null || obj.{collectionName}.Count == 0)");
                    using (sb.BeginBlock())
                    {
                        sb.WriteLineFormat
                        (
                            "{0}.{1}({2}{3}, ({4}){5});",
                            ctx.Helper,
                            writeMethod,
                            ctx.Ref,
                            ctx.Target,
                            typeIdType,
                            defaultKey
                        );
                    }

                    sb.WriteLine("else");
                    using (sb.BeginBlock())
                    {
                        EmitDiscriminatorFromFirstItem(sb, $"obj.{collectionName}", info, typeIdType, out var discriminatorVar);
                        sb.WriteLineFormat
                        (
                            "{0}.{1}({2}{3}, {4});",
                            ctx.Helper,
                            writeMethod,
                            ctx.Ref,
                            ctx.Target,
                            discriminatorVar
                        );
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
            if (instanceName.StartsWith("(") && instanceName.EndsWith(")"))
            {
                EmitThrowInstanceNull(sb, typeName);
            }
            else
            {
                EmitThrowMemberNull(sb, instanceName);
            }
        }

        EmitSerializeNested(sb, typeName, instanceName, ctx);
    }

    private static void EmitSerializeNested(IndentedStringBuilder sb, string typeName, string instanceName, WriterCtx ctx)
    {
        if (ctx.IsSpan)
        {
            sb.WriteLineFormat
            (
                "var bytesWritten = {0}.Serialize({1}, data);",
                TypeHelper.GetSimpleTypeName(typeName),
                instanceName
            );
            sb.WriteLine("data = data.Slice(bytesWritten);");
        }
        else
        {
            sb.WriteLineFormat("{0}.Serialize({1}, stream);", TypeHelper.GetSimpleTypeName(typeName), instanceName);
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
            EmitWriteString(sb, ctx, $"obj.{member.Name}");
        }
        else if (member.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
            var writeMethod = $"Write{typeName}";
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4})obj.{5});",
                ctx.Helper,
                writeMethod,
                ctx.Ref,
                ctx.Target,
                typeName,
                member.Name
            );
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

    private static void EmitCollectionBody
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
                EmitThrowIncorrectCollectionSize(sb, member.Name, collectionInfo.CountSize.Value, countExpression);
            }
        }
        else if (collectionInfo is { Unlimited: false, CountSizeReferenceIndex: null } && !isHandledByPolymorphic)
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
            var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4}){5});",
                ctx.Helper,
                countWriteMethod,
                ctx.Ref,
                ctx.Target,
                countType,
                countExpression
            );
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

        var isByteCollection = IsByteCollection(member);

        if (isByteCollection)
        {
            sb.WriteLineFormat
            (
                "{0}.WriteBytes({1}{2}, obj.{3});",
                ctx.Helper,
                ctx.Ref,
                ctx.Target,
                member.Name
            );
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
                                    "var bytesWritten = {0}.Serialize(({0})item, data);",
                                    typeName
                                );
                                sb.WriteLine("data = data.Slice(bytesWritten);");
                            }
                            else
                            {
                                sb.WriteLineFormat
                                (
                                    "{0}.Serialize(({0})item, stream);",
                                    typeName
                                );
                            }
                        }

                        sb.WriteLine("break;");
                    }
                }

                sb.WriteLine("default:");
                EmitThrowUnknownItemType(sb, member.Name, $"obj.{member.Name}[0]");
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
        var listItemsVar = $"obj.{member.Name}";

        sb.WriteLine($"if ({listItemsVar} is null || {listItemsVar}.Count == 0)");
        using (sb.BeginBlock())
        {
            EmitEmptyCollectionHeader(sb, ctx, collectionInfo, info);
        }
        sb.WriteLine("else");
        using (sb.BeginBlock())
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
            var countExpression = $"{listItemsVar}.Count";
            sb.WriteLineFormat(
                "{0}.{1}({2}{3}, ({4}){5});",
                ctx.Helper,
                countWriteMethod,
                ctx.Ref,
                ctx.Target,
                countType,
                countExpression
            );

            var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
            EmitDiscriminatorFromFirstItem(sb, listItemsVar, info, typeIdType, out var discriminatorVar);

            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
            sb.WriteLineFormat(
                "{0}.Write{1}({2}{3}, {4});",
                ctx.Helper,
                typeIdTypeName,
                ctx.Ref,
                ctx.Target,
                discriminatorVar
            );

            sb.WriteLine($"switch ({discriminatorVar})");
            using (sb.BeginBlock())
            {
                foreach (var option in info.Options)
                {
                    var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                    var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                    sb.WriteLine($"case {key}:");
                    using (sb.BeginBlock())
                    {
                        EmitForLoop(sb, countExpression, loopSb =>
                        {
                            var isReferenceType = !option.IsValueType;
                            EmitElement(loopSb, $"({typeName}){listItemsVar}[i]", ctx, typeName, false, false, true, null, isReferenceType);
                        });
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
        EmitForeach(sb, $"obj.{member.Name}", loopSb =>
        {
            var elementInfo = member.ListTypeArgument!.Value;
            var isReferenceType = !elementInfo.IsValueType;
            EmitElement(loopSb, "item", ctx, elementInfo.TypeName, elementInfo.IsUnmanagedType, elementInfo.IsStringType, elementInfo.HasGenerateSerializerAttribute, member.PolymorphicInfo, isReferenceType);
        });
    }

    private static void GenerateStandardCollectionBody
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx
    )
    {
        string typeName;
        bool isUnmanaged, isString, hasGenerator, isReferenceType;

        if (member.ListTypeArgument is { } listArg)
        {
            typeName = listArg.TypeName;
            isUnmanaged = listArg.IsUnmanagedType;
            isString = listArg.IsStringType;
            hasGenerator = listArg.HasGenerateSerializerAttribute;
            isReferenceType = !listArg.IsValueType;
        }
        else if (member.CollectionTypeInfo is { } collInfo)
        {
            typeName = collInfo.ElementTypeName;
            isUnmanaged = collInfo.IsElementUnmanagedType;
            isString = collInfo.IsElementStringType;
            hasGenerator = collInfo.HasElementGenerateSerializerAttribute;
            isReferenceType = !collInfo.IsElementValueType;
        }
        else
        {
            return;
        }

        if (member.CollectionTypeInfo?.IsArray == true || member.IsList)
        {
            var countExpr = GeneratorUtilities.GetCountExpression(member, member.Name);
            EmitForLoop(sb, countExpr, loopSb =>
            {
                var elementAccess = $"obj.{member.Name}[i]";
                EmitElement(loopSb, elementAccess, ctx, typeName, isUnmanaged, isString, hasGenerator, null, isReferenceType);
            });
        }
        else
        {
            EmitForeach(sb, $"obj.{member.Name}", loopSb =>
            {
                EmitElement(loopSb, "item", ctx, typeName, isUnmanaged, isString, hasGenerator, null, isReferenceType);
            });
        }
    }

    private static void EmitCollectionNull
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
            EmitEmptyCollectionHeader(sb, ctx, collectionInfo, member.PolymorphicInfo);
        }
        else if (collectionInfo.CountType != null || collectionInfo.CountSizeReferenceIndex is not null)
        {
            EmitEmptyCollectionHeader(sb, ctx, collectionInfo, null);
        }
        else
        {
            EmitEmptyCollectionHeader(sb, ctx, collectionInfo, null);
        }
    }

    private static void GeneratePolymorphicSerialization
        (IndentedStringBuilder sb, MemberToGenerate member, WriterCtx ctx)
        {
            var info = member.PolymorphicInfo!.Value;
    
            sb.WriteLineFormat($"if (obj.{member.Name} is null)");
            using (sb.BeginBlock())
            {
                EmitThrowMemberNull(sb, $"obj.{member.Name}");
            }
            sb.WriteLine();
    
            var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
            sb.WriteLine($"var discriminator = obj.{member.Name} switch");
            sb.WriteLine("{");
            sb.Indent();
            foreach (var option in info.Options)
            {
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                sb.WriteLineFormat($"{TypeHelper.GetSimpleTypeName(option.Type)} => {key},");
            }
            sb.WriteLine($"_ => {GetThrowUnknownType($"obj.{member.Name}")}");
            sb.Unindent();
            sb.WriteLine("};");
            sb.WriteLine();
    
            PolymorphicUtilities.GeneratePolymorphicSwitch(
                sb,
                info,
                "discriminator",
                (option, key) =>
                {
                    using (sb.BeginBlock())
                    {
                        var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                        if (info.TypeIdPropertyIndex is null)
                        {
                            EmitWriteTypeId(sb, ctx, option, info);
                        }
                        var typedInstance = $"({typeName})obj.{member.Name}";
                        EmitSerializeNestedOrThrow(sb, typeName, typedInstance, ctx);
                        sb.WriteLine("break;");
                    }
                },
                () =>
                {
                    using (sb.BeginBlock())
                    {
                        sb.WriteLine("break;");
                    }
                });
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
    
            if (!member.IsValueType)
            {
                sb.WriteLineFormat($"if ({instanceName} is null)");
                using (sb.BeginBlock())
                {
                    EmitThrowItemNull(sb);
                }
                sb.WriteLine();
            }
    
            var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
            sb.WriteLine($"var discriminator = {instanceName} switch");
            sb.WriteLine("{");
            sb.Indent();
            foreach (var option in info.Options)
            {
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                sb.WriteLineFormat($"{TypeHelper.GetSimpleTypeName(option.Type)} => {key},");
            }
            sb.WriteLine($"_ => {GetThrowUnknownType(instanceName)}");
            sb.Unindent();
            sb.WriteLine("};");
            sb.WriteLine();
    
            PolymorphicUtilities.GeneratePolymorphicSwitch(
                sb,
                info,
                "discriminator",
                (option, key) =>
                {
                    using (sb.BeginBlock())
                    {
                        var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                        if (info.TypeIdPropertyIndex is null)
                        {
                            EmitWriteTypeId(sb, ctx, option, info);
                        }
                        var typedInstance = $"({typeName}){instanceName}";
                        EmitSerializeNestedOrThrow(sb, typeName, typedInstance, ctx);
                        sb.WriteLine("break;");
                    }
                },
                () =>
                {
                    using (sb.BeginBlock())
                    {
                        sb.WriteLine("break;");
                    }
                });
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
        if (!ctx.IsSpan)
        {
            sb.WriteLine("if (!stream.CanSeek)");
            using (sb.BeginBlock())
            {
                EmitThrowSeekableStreamRequired(sb);
            }
        }

        BeginCountReservation(sb, ctx, countType);

        sb.WriteLine("int count = 0;");
        sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
        using (sb.BeginBlock())
        {
            var elementInfo = member.CollectionTypeInfo!.Value;
            EmitForeach(sb, $"obj.{member.Name}", loopBody =>
            {
                var isReferenceType = !elementInfo.IsElementValueType;
                EmitElement(loopBody, "item", ctx, elementInfo.ElementTypeName, elementInfo.IsElementUnmanagedType, elementInfo.IsElementStringType, elementInfo.HasElementGenerateSerializerAttribute, null, isReferenceType);
                loopBody.WriteLine("count++;");
            });
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

        if (!ctx.IsSpan)
        {
            sb.WriteLine("if (!stream.CanSeek)");
            using (sb.BeginBlock())
            {
                EmitThrowSeekableStreamRequired(sb);
            }
        }
        
        BeginCountReservation(sb, ctx, "int");

        sb.WriteLine("int count = 0;");
        sb.WriteLine($"using var enumerator = obj.{member.Name}.GetEnumerator();");

        sb.WriteLine("if (!enumerator.MoveNext())");
        using (sb.BeginBlock())
        {
            EndCountReservation(sb, ctx, "int", "0");
            EmitWriteTypeId
            (
                sb,
                ctx,
                defaultOption,
                info
            );
        }

        sb.WriteLine("else");
        using (sb.BeginBlock())
        {
            sb.WriteLine("var discriminator = enumerator.Current switch");
            sb.WriteLine("{");
            sb.Indent();
            foreach (var option in info.Options)
            {
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                sb.WriteLineFormat($"{TypeHelper.GetSimpleTypeName(option.Type)} => ({typeIdType}){key},");
            }

            sb.WriteLine
                ($"_ => {GetThrowUnknownType("enumerator.Current")}");
            sb.Unindent();
            sb.WriteLine("};");

            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
            sb.WriteLineFormat($"{ctx.Helper}.Write{typeIdTypeName}({ctx.Ref}{ctx.Target}, discriminator);");

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
                            var isReferenceType = !option.IsValueType;
                            EmitElement(sb, $"({typeName})enumerator.Current", ctx, typeName, false, false, true, null, isReferenceType);
                            sb.WriteLine("count++;");
                        }
                        sb.WriteLine("while (enumerator.MoveNext());");
                        sb.WriteLine("break;");
                    }
                }
            }
            EndCountReservation(sb, ctx, "int", "count");
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
    
                sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
                using (sb.BeginBlock())
                {
                    var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
                    sb.WriteLine($"var discriminator = obj.{member.Name} switch");
                    sb.WriteLine("{");
                    sb.Indent();
                    foreach (var option in info.Options)
                    {
                        var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                        sb.WriteLineFormat($"{TypeHelper.GetSimpleTypeName(option.Type)} => ({typeIdType}?){key},");
                    }
                    sb.WriteLine($"_ => ({typeIdType}?)null");
                    sb.Unindent();
                    sb.WriteLine("};");
                    sb.WriteLine();
    
                    sb.WriteLine("if (discriminator.HasValue)");
                    using (sb.BeginBlock())
                    {
                        PolymorphicUtilities.GeneratePolymorphicSwitch(
                            sb,
                            info,
                            "discriminator.Value",
                            (option, key) =>
                            {
                                using (sb.BeginBlock())
                                {
                                    sb.WriteLineFormat($"obj.{referencedMember.Name} = {key};");
                                    sb.WriteLine("break;");
                                }
                            },
                            () =>
                            {
                                using (sb.BeginBlock())
                                {
                                    sb.WriteLine("break;");
                                }
                            });
                    }
                }
            }
        }

    private static void EmitWrite(IndentedStringBuilder sb, WriterCtx ctx, string target, string typeName, string value, string comment = "")
    {
        var friendlyTypeName = TypeHelper.GetMethodFriendlyTypeName(typeName);
        var writeMethod = $"Write{friendlyTypeName}";
        sb.WriteLineFormat
        (
            "{0}.{1}({2}{3}, ({4}){5});{6}",
            ctx.Helper,
            writeMethod,
            ctx.Ref,
            target,
            typeName,
            value,
            comment
        );
    }

    private static void EmitSerializeForCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
        CollMode mode)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        switch (mode)
        {
            case CollMode.Fixed:
                EmitFixedCollection(sb, member, ctx, collectionInfo);
                break;
            case CollMode.Count:
                EmitCountedCollection(sb, member, ctx, collectionInfo);
                break;
        }
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
        EmitWrite(sb, ctx, ctx.Target, underlyingType, key);
    }
    
    private static void EmitWriteString(IndentedStringBuilder sb, WriterCtx ctx, string expr)
    {
        sb.WriteLineFormat
        (
            "{0}.WriteString({1}{2}, {3});",
            ctx.Helper,
            ctx.Ref,
            ctx.Target,
            expr
        );
    }
    
    private static void BeginCountReservation(IndentedStringBuilder sb, WriterCtx ctx, string countType)
    {
        if (ctx.IsSpan)
        {
            sb.WriteLine($"var countSpan = {ctx.Target};");
            sb.WriteLine($"{ctx.Target} = {ctx.Target}.Slice(sizeof({countType}));");
        }
        else
        {
            sb.WriteLine($"var countPosition = {ctx.Target}.Position;");
            EmitWrite(sb, ctx, ctx.Target, countType, "0", " // Placeholder for count");
        }
    }

    private static void EndCountReservation(IndentedStringBuilder sb, WriterCtx ctx, string countType, string countExpr)
    {
        if (ctx.IsSpan)
        {
            EmitWrite(sb, ctx, "countSpan", countType, countExpr);
        }
        else
        {
            sb.WriteLine($"var endPosition = {ctx.Target}.Position;");
            sb.WriteLine($"{ctx.Target}.Position = countPosition;");
            EmitWrite(sb, ctx, ctx.Target, countType, countExpr);
            sb.WriteLine($"{ctx.Target}.Position = endPosition;");
        }
    }

    private static void EmitFixedCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
        CollectionInfo collectionInfo)
    {
        sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
        using (sb.BeginBlock())
        {
            EmitThrowFixedSizeCollectionNull(sb, member.Name);
        }

        EmitCollectionBody(sb, member, ctx, collectionInfo);
    }

    private static void EmitCountedCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
        CollectionInfo collectionInfo)
    {
        if (TryEmitFastEnumerableBytes(sb, member, ctx))
        {
            return;
        }

        if (EmitCountSizeReferenceCase(sb, member, ctx, collectionInfo))
        {
            return;
        }

        if (TryEmitSingleTypeIdPolymorphic(sb, member, ctx, collectionInfo))
        {
            return;
        }

        EmitNullOrNonNullCollection(sb, member, ctx, collectionInfo);
    }

    private static bool TryEmitFastEnumerableBytes(IndentedStringBuilder sb, MemberToGenerate member, WriterCtx ctx)
    {
        return false;
    }

    private static bool EmitCountSizeReferenceCase(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
        CollectionInfo collectionInfo)
    {
        if (collectionInfo.CountSizeReferenceIndex is null)
        {
            return false;
        }

    if (member.CollectionTypeInfo?.IsPureEnumerable == true)
        {
        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        if (!ctx.IsSpan)
            {
            sb.WriteLine("if (!stream.CanSeek)");
            using (sb.BeginBlock())
            {
                EmitThrowSeekableStreamRequired(sb);
            }
        }

        BeginCountReservation(sb, ctx, countType);

        sb.WriteLine("int count = 0;");
        sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
        using (sb.BeginBlock())
        {
            var elementInfo = member.CollectionTypeInfo!.Value;
            EmitForeach(sb, $"obj.{member.Name}", loopBody =>
            {
                var isReferenceType = !elementInfo.IsElementValueType;
                EmitElement(loopBody, "item", ctx, elementInfo.ElementTypeName, elementInfo.IsElementUnmanagedType, elementInfo.IsElementStringType, elementInfo.HasElementGenerateSerializerAttribute, null, isReferenceType);
                loopBody.WriteLine("count++;");
            });
            }
        EndCountReservation(sb, ctx, countType, "count");
    }
    else
    {
        sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
        using (sb.BeginBlock())
            {
            if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
            {
                GeneratePolymorphicCollectionBody(sb, member, ctx, collectionInfo);
            }
            else
            {
                GenerateStandardCollectionBody(sb, member, ctx);
            }
            }
        }
        return true;
    }

    private static bool TryEmitSingleTypeIdPolymorphic(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
        CollectionInfo collectionInfo)
    {
        var isNotListOrArray = !member.IsList && member.CollectionTypeInfo?.IsArray != true;
        var useSingleTypeIdPolymorphicSerialization = isNotListOrArray
            && GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            && collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId
            && string.IsNullOrEmpty(collectionInfo.TypeIdProperty);

        if (!useSingleTypeIdPolymorphicSerialization)
        {
            return false;
        }

        if (member.PolymorphicInfo is not { } info)
        {
            return true;
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

    private static void EmitNullOrNonNullCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        WriterCtx ctx,
        CollectionInfo collectionInfo)
    {
        var isNotListOrArray = !member.IsList && member.CollectionTypeInfo?.IsArray != true;
        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = IsByteCollection(member);

        if (isNotListOrArray && member.CollectionTypeInfo?.IsPureEnumerable == true && !isByteCollection)
        {
            GenerateEnumerableCollection(sb, member, ctx, collectionInfo);
            return;
        }

        sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
        using (sb.BeginBlock())
        {
            EmitCollectionNull(sb, member, ctx, collectionInfo, isNotListOrArray);
        }

        sb.WriteLine("else");
        using (sb.BeginBlock())
        {
            EmitCollectionBody(sb, member, ctx, collectionInfo);
        }
    }

    private static void EmitForLoop(IndentedStringBuilder sb, string countExpr, Action<IndentedStringBuilder> body)
    {
        sb.WriteLineFormat("for (int i = 0; i < {0}; i++)", countExpr);
        using (sb.BeginBlock())
        {
            body(sb);
        }
    }

    private static void EmitForeach(IndentedStringBuilder sb, string collectionExpr, Action<IndentedStringBuilder> body)
    {
        sb.WriteLineFormat("foreach (var item in {0})", collectionExpr);
        using (sb.BeginBlock())
        {
            body(sb);
        }
    }

    private static void EmitElement(
        IndentedStringBuilder sb,
        string elementExpr,
        WriterCtx ctx,
        string typeName,
        bool isUnmanaged,
        bool isString,
        bool hasGenerator,
        PolymorphicInfo? polymorphicInfo,
        bool isReferenceType)
    {
        if (isReferenceType)
        {
            sb.WriteLineFormat("if ({0} is null)", elementExpr);
            using (sb.BeginBlock())
            {
                sb.WriteLine("throw new System.NullReferenceException(\"An item in a collection was null.\");");
            }
        }
        
        if (polymorphicInfo is not null)
        {
            var itemMember = new MemberToGenerate(
                "item",
                typeName,
                !isReferenceType,
                isUnmanaged,
                isString,
                hasGenerator,
                false, null, null, polymorphicInfo, false, null, false, false, null, null
            );
            GeneratePolymorphicItemSerialization(sb, itemMember, elementExpr, ctx);
        }
        else if (hasGenerator)
        {
            if (isReferenceType)
            {
                EmitSerializeNestedOrThrow(sb, typeName, elementExpr, ctx);
            }
            else
            {
                EmitSerializeNested(sb, typeName, elementExpr, ctx);
            }
        }
        else if (isUnmanaged)
        {
            var friendlyTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeName);
            var writeMethod = $"Write{friendlyTypeName}";
            sb.WriteLineFormat(
                "{0}.{1}({2}{3}, ({4}){5});",
                ctx.Helper,
                writeMethod,
                ctx.Ref,
                ctx.Target,
                friendlyTypeName,
                elementExpr
            );
        }
        else if (isString)
        {
            EmitWriteString(sb, ctx, elementExpr);
        }
    }

    private static void EmitDiscriminatorFromFirstItem(
        IndentedStringBuilder sb,
        string collectionExpr,
        PolymorphicInfo info,
        string typeIdType,
        out string discriminatorVar)
    {
        discriminatorVar = "discriminator";
        sb.WriteLine($"var firstItem = {collectionExpr}[0];");
        sb.WriteLine($"var {discriminatorVar} = firstItem switch");
        sb.WriteLine("{");
        sb.Indent();
        foreach (var option in info.Options)
        {
            var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
            sb.WriteLineFormat("{0} => ({1}){2},", TypeHelper.GetSimpleTypeName(option.Type), typeIdType, key);
        }
        sb.WriteLine($"_ => {GetThrowUnknownType("firstItem")}");
        sb.Unindent();
        sb.WriteLine("};");
    }

    private static void EmitEmptyCollectionHeader(
        IndentedStringBuilder sb,
        WriterCtx ctx,
        CollectionInfo collectionInfo,
        PolymorphicInfo? polymorphicInfo)
    {
        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        var countWriteMethod = TypeHelper.GetWriteMethodName(countType);

        sb.WriteLineFormat(
            "{0}.{1}({2}{3}, ({4})0);",
            ctx.Helper,
            countWriteMethod,
            ctx.Ref,
            ctx.Target,
            countType
        );

        if (polymorphicInfo is { } info &&
            collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId &&
            string.IsNullOrEmpty(collectionInfo.TypeIdProperty))
        {
            var defaultOption = info.Options.FirstOrDefault();
            EmitWriteTypeId(sb, ctx, defaultOption, info);
        }
    }
    #region Helpers

    private static bool IsByteCollection(MemberToGenerate m)
    {
        var elementTypeName = m.ListTypeArgument?.TypeName ?? m.CollectionTypeInfo?.ElementTypeName;
        return TypeHelper.IsByteCollection(elementTypeName);
    }

    #endregion

    #region Exception-Helpers
    
    private static string GetThrowUnknownType(string contextExpr)
        => $"throw new System.IO.InvalidDataException(\"Unknown type for {contextExpr}: \" + {contextExpr}?.GetType().FullName)";
    
    private static void EmitThrowSeekableStreamRequired(IndentedStringBuilder sb)
        => sb.WriteLine("throw new NotSupportedException(\"Stream must be seekable to serialize this collection.\");");

    private static void EmitThrowIncorrectCollectionSize(IndentedStringBuilder sb, string memberName, int expectedSize, string actualSizeExpr)
        => sb.WriteLine($"throw new System.InvalidOperationException($\"Collection '{memberName}' must have a size of {expectedSize} but was {{{actualSizeExpr}}}.\");");

    private static void EmitThrowFixedSizeCollectionNull(IndentedStringBuilder sb, string memberName)
        => sb.WriteLine($"throw new System.ArgumentNullException(nameof(obj.{memberName}), \"Fixed-size collections cannot be null.\");");
    
    private static void EmitThrowMemberNull(IndentedStringBuilder sb, string memberName)
        => sb.WriteLine($"throw new System.NullReferenceException($\"Member '{memberName}' cannot be null.\");");
    
    private static void EmitThrowInstanceNull(IndentedStringBuilder sb, string typeName)
        => sb.WriteLine($"throw new System.NullReferenceException($\"Instance of type '{typeName}' cannot be null.\");");

    private static void EmitThrowItemNull(IndentedStringBuilder sb)
        => sb.WriteLine("throw new System.NullReferenceException(\"Item in collection cannot be null.\");");

    private static void EmitThrowUnknownItemType(IndentedStringBuilder sb, string collectionName, string itemExpr)
        => sb.WriteLine($"throw new System.IO.InvalidDataException($\"Unknown type for item in {collectionName}: {{{itemExpr}.GetType().Name}}\");");

    #endregion
}
