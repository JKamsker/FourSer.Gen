using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
///     Generates Serialize method implementations
/// </summary>
public static class SerializationGenerator
{
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

        GenerateSerializationBody(sb, typeToGenerate, "data", "SpanWriter");

        sb.WriteLine("return originalData.Length - data.Length;");
    }

    private static void GenerateStreamSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLineFormat("public static void Serialize({0} obj, System.IO.Stream stream)", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        GenerateSerializationBody(sb, typeToGenerate, "stream", "StreamWriter");
    }

    private static void GenerateSerializationBody
    (
        IndentedStringBuilder sb,
        TypeToGenerate typeToGenerate,
        string target,
        string helper
    )
    {
        GenerateTypeIdPrePass(sb, typeToGenerate);

        for (var i = 0; i < typeToGenerate.Members.Count; i++)
        {
            var member = typeToGenerate.Members[i];
            if (member.IsCountSizeReferenceFor is not null)
            {
                var collectionMember = typeToGenerate.Members[member.IsCountSizeReferenceFor.Value];
                var refOrEmpty = target == "data" ? "ref " : "";
                var collectionName = collectionMember.Name;
                var countExpression = $"obj.{collectionName}?.Count ?? 0";
                var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
                var writeMethod = $"Write{typeName}";
                sb.WriteLineFormat
                (
                    "{0}.{1}({2}{3}, ({4})({5}));",
                    helper,
                    writeMethod,
                    refOrEmpty,
                    target,
                    typeName,
                    countExpression
                );
            }
            else if (member.IsTypeIdPropertyFor is not null)
            {
                var referencedMember = typeToGenerate.Members[member.IsTypeIdPropertyFor.Value];
                if (referencedMember.IsList || referencedMember.IsCollection)
                {
                    var refOrEmpty = target == "data" ? "ref " : "";
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
                            helper,
                            writeMethod,
                            refOrEmpty,
                            target,
                            typeIdType,
                            defaultKey
                        );
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
                            sb.WriteLineFormat
                                ("{0} => ({1}){2},", TypeHelper.GetSimpleTypeName(option.Type), typeIdType, key);
                        }

                        sb.WriteLine
                            ($"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{firstItem.GetType().Name}}\")");
                        sb.Unindent();
                        sb.WriteLine("};");

                        sb.WriteLineFormat
                        (
                            "{0}.{1}({2}{3}, discriminator);",
                            helper,
                            writeMethod,
                            refOrEmpty,
                            target
                        );
                    }
                }
                else
                {
                    GenerateMemberSerialization(sb, member, target, helper);
                }
            }
            else
            {
                GenerateMemberSerialization(sb, member, target, helper);
            }
        }
    }

    private static void GenerateMemberSerialization
        (IndentedStringBuilder sb, MemberToGenerate member, string target, string helper)
    {
        var refOrEmpty = target == "data" ? "ref " : "";

        if (member.IsList || member.IsCollection)
        {
            GenerateCollectionSerialization(sb, member, target, helper);
        }
        else if (member.PolymorphicInfo is not null)
        {
            GeneratePolymorphicSerialization(sb, member, target, helper);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat
                    ("throw new System.NullReferenceException($\"Property \\\"{0}\\\" cannot be null.\");", member.Name);
            }

            if (target == "data")
            {
                sb.WriteLineFormat
                (
                    "var bytesWritten = {0}.Serialize(obj.{1}, data);",
                    TypeHelper.GetSimpleTypeName(member.TypeName),
                    member.Name
                );
                sb.WriteLine("data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.WriteLineFormat("{0}.Serialize(obj.{1}, stream);", TypeHelper.GetSimpleTypeName(member.TypeName), member.Name);
            }
        }
        else if (member.IsStringType)
        {
            sb.WriteLineFormat
            (
                "{0}.WriteString({1}{2}, obj.{3});",
                helper,
                refOrEmpty,
                target,
                member.Name
            );
        }
        else if (member.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
            var writeMethod = $"Write{typeName}";
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4})obj.{5});",
                helper,
                writeMethod,
                refOrEmpty,
                target,
                typeName,
                member.Name
            );
        }
    }

    private static void GenerateCollectionSerialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string target,
        string helper
    )
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        if (collectionInfo.CountSizeReferenceIndex is not null)
        {
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using (sb.BeginBlock())
            {
                if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
                {
                    GeneratePolymorphicCollectionBody(sb, member, target, helper, collectionInfo);
                }
                else
                {
                    GenerateStandardCollectionBody(sb, member, target, helper);
                }
            }
            return;
        }

        var isNotListOrArray = !member.IsList && member.CollectionTypeInfo?.IsArray != true;
        var useSingleTypeIdPolymorphicSerialization = isNotListOrArray
            && GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            && collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId
            && string.IsNullOrEmpty(collectionInfo.TypeIdProperty);

        if (useSingleTypeIdPolymorphicSerialization)
        {
            if (member.PolymorphicInfo is not { } info)
            {
                return;
            }

            if (target == "data")
            {
                GenerateSpanPolymorphicCollectionSerialization(sb, member, info, collectionInfo);
            }
            else
            {
                GenerateStreamPolymorphicCollectionSerialization(sb, member, info, collectionInfo);
            }

            return;
        }

        sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
        using (sb.BeginBlock())
        {
            HandleNullCollection
            (
                sb,
                member,
                target,
                helper,
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
                target,
                helper,
                collectionInfo
            );
        }
    }

    private static void HandleNonNullCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string target,
        string helper,
        CollectionInfo collectionInfo
    )
    {
        var refOrEmpty = target == "data" ? "ref " : "";
        var isHandledByPolymorphic = collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId &&
            string.IsNullOrEmpty(collectionInfo.TypeIdProperty);

        if (collectionInfo.CountSize >= 0)
        {
            var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
            sb.WriteLineFormat("if ({0} != {1})", countExpression, collectionInfo.CountSize);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat(
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
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
            var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4}){5});",
                helper,
                countWriteMethod,
                refOrEmpty,
                target,
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
                target,
                helper,
                collectionInfo
            );
            return;
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (isByteCollection)
        {
            sb.WriteLineFormat
            (
                "{0}.WriteBytes({1}{2}, obj.{3});",
                helper,
                refOrEmpty,
                target,
                member.Name
            );
        }
        else
        {
            GenerateStandardCollectionBody(sb, member, target, helper);
        }
    }

    private static void GeneratePolymorphicCollectionBody
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string target,
        string helper,
        CollectionInfo collectionInfo
    )
    {
        if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
        {
            GenerateIndividualTypeIdCollection(sb, member, target, helper);
        }
        else if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
        {
            GenerateSingleTypeIdPolymorphicCollection
            (
                sb,
                member,
                target,
                helper,
                collectionInfo
            );
        }
    }

    private static void GenerateSingleTypeIdPolymorphicCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string target,
        string helper,
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
                target,
                helper,
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
                target,
                helper,
                info
            );
        }
    }

    private static void GenerateSingleTypeIdPolymorphicCollectionWithProperty
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string target,
        string helper,
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
                            if (target == "data")
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
        string target,
        string helper,
        CollectionInfo collectionInfo,
        PolymorphicInfo info
    )
    {
        var refOrEmpty = target == "data" ? "ref " : "";
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
        var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;

        sb.WriteLine($"if ({listItemsVar} is null || {listItemsVar}.Count == 0)");
        using (sb.BeginBlock())
        {
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4})0);",
                helper,
                countWriteMethod,
                refOrEmpty,
                target,
                countType
            );
            PolymorphicUtilities.GenerateWriteTypeIdCode
            (
                sb,
                defaultOption,
                info,
                target,
                helper
            );
        }

        sb.WriteLine("else");
        using (sb.BeginBlock())
        {
            var countExpression = $"{listItemsVar}.Count";
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4}){5});",
                helper,
                countWriteMethod,
                refOrEmpty,
                target,
                countType,
                countExpression
            );

            sb.WriteLine($"var firstItem = {listItemsVar}[0];");
            sb.WriteLine("var discriminator = firstItem switch");
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

            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
            sb.WriteLineFormat
            (
                "{0}.Write{1}({2}{3}, discriminator);",
                helper,
                typeIdTypeName,
                refOrEmpty,
                target
            );

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
                            if (target == "data")
                            {
                                sb.WriteLine($"var bytesWritten = {typeName}.Serialize(({typeName}){listItemsVar}[i], data);");
                                sb.WriteLine("data = data.Slice(bytesWritten);");
                            }
                            else
                            {
                                sb.WriteLine($"{typeName}.Serialize(({typeName}){listItemsVar}[i], stream);");
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
        string target,
        string helper
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
            target,
            helper
        );
    }

    private static void GenerateStandardCollectionBody
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string target,
        string helper
    )
    {
        if (member.CollectionTypeInfo?.IsArray == true || member.IsList)
        {
            GenerateListOrArraySerialization(sb, member, target, helper);
        }
        else
        {
            GenerateEnumerableSerialization(sb, member, target, helper);
        }
    }

    private static void GenerateEnumerableSerialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string target,
        string helper
    )
    {
        sb.WriteLineFormat("foreach (var item in obj.{0})", member.Name);
        using var __ = sb.BeginBlock();
        if (member.CollectionTypeInfo is not null)
        {
            GenerateCollectionElementSerialization
            (
                sb,
                member.CollectionTypeInfo.Value,
                "item",
                target,
                helper
            );
        }
    }

    private static void GenerateListOrArraySerialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string target,
        string helper
    )
    {
        var loopCountExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
        sb.WriteLineFormat("for (int i = 0; i < {0}; i++)", loopCountExpression);
        using var __ = sb.BeginBlock();
        if (member.ListTypeArgument is not null)
        {
            GenerateListElementSerialization
            (
                sb,
                member.ListTypeArgument.Value,
                $"obj.{member.Name}[i]",
                target,
                helper
            );
        }
        else if (member.CollectionTypeInfo is not null)
        {
            GenerateCollectionElementSerialization
            (
                sb,
                member.CollectionTypeInfo.Value,
                $"obj.{member.Name}[i]",
                target,
                helper
            );
        }
    }

    private static void HandleNullCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string target,
        string helper,
        CollectionInfo collectionInfo,
        bool isNotListOrArray
    )
    {
        var refOrEmpty = target == "data" ? "ref " : "";
        var isPolymorphicSingleTypeId = GeneratorUtilities.ShouldUsePolymorphicSerialization(member) 
            && collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId 
            && string.IsNullOrEmpty(collectionInfo.TypeIdProperty);

        if (isPolymorphicSingleTypeId && !isNotListOrArray)
        {
            var info = member.PolymorphicInfo!.Value;
            var defaultOption = info.Options.FirstOrDefault();
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);

            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4})0);",
                helper,
                countWriteMethod,
                refOrEmpty,
                target,
                countType
            );
            PolymorphicUtilities.GenerateWriteTypeIdCode
            (
                sb,
                defaultOption,
                info,
                target,
                helper
            );
        }
        else if (collectionInfo.CountType != null || collectionInfo.CountSizeReferenceIndex is not null)
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4})0);",
                helper,
                countWriteMethod,
                refOrEmpty,
                target,
                countType
            );
        }
        else
        {
            var countType = TypeHelper.GetDefaultCountType();
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4})0);",
                helper,
                countWriteMethod,
                refOrEmpty,
                target,
                countType
            );
            
            // sb.WriteLineFormat
            //     ("throw new System.NullReferenceException($\"Collection \\\"{0}\\\" cannot be null.\");", member.Name);
        }
    }

    private static void GeneratePolymorphicSerialization
        (IndentedStringBuilder sb, MemberToGenerate member, string target, string helper)
    {
        var info = member.PolymorphicInfo!.Value;

        sb.WriteLineFormat("switch (obj.{0})", member.Name);
        using var _ = sb.BeginBlock();

        GeneratePolymorphicSerializationLogic
        (
            sb,
            target,
            helper,
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
        string target,
        string helper
    )
    {
        var info = member.PolymorphicInfo!.Value;

        sb.WriteLineFormat("switch ({0})", instanceName);
        using var _ = sb.BeginBlock();

        GeneratePolymorphicSerializationLogic
        (
            sb,
            target,
            helper,
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

    private static void GeneratePolymorphicSerializationLogic
    (
        IndentedStringBuilder sb,
        string target,
        string helper,
        PolymorphicInfo info,
        bool isItem
    )
    {
        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.WriteLineFormat("case {0} typedInstance:", typeName);
            using var __ = sb.BeginBlock();
            if (info.TypeIdPropertyIndex is null)
            {
                PolymorphicUtilities.GenerateWriteTypeIdCode
                (
                    sb,
                    option,
                    info,
                    target,
                    helper
                );
            }

            if (target == "data")
            {
                sb.WriteLineFormat("var bytesWritten = {0}.Serialize(typedInstance, data);", typeName);
                sb.WriteLine("data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.WriteLineFormat("{0}.Serialize(typedInstance, stream);", typeName);
            }

            sb.WriteLine("break;");
        }
    }

    private static void GenerateListElementSerialization
    (
        IndentedStringBuilder sb,
        ListTypeArgumentInfo elementInfo,
        string elementAccess,
        string target,
        string helper
    )
    {
        GenerateElementSerialization
        (
            sb,
            elementAccess,
            target,
            helper,
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
        string target,
        string helper
    )
    {
        GenerateElementSerialization
        (
            sb,
            elementAccess,
            target,
            helper,
            elementInfo.ElementTypeName,
            elementInfo.HasElementGenerateSerializerAttribute,
            elementInfo.IsElementUnmanagedType,
            elementInfo.IsElementStringType
        );
    }

    private static void GenerateElementSerialization
    (
        IndentedStringBuilder sb,
        string elementAccess,
        string target,
        string helper,
        string typeName,
        bool hasGenerateSerializerAttribute,
        bool isUnmanagedType,
        bool isStringType
    )
    {
        var refOrEmpty = target == "data" ? "ref " : "";
        if (hasGenerateSerializerAttribute)
        {
            if (target == "data")
            {
                sb.WriteLineFormat
                (
                    "var bytesWritten = {0}.Serialize({1}, data);",
                    TypeHelper.GetSimpleTypeName(typeName),
                    elementAccess
                );
                sb.WriteLine("data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.WriteLineFormat("{0}.Serialize({1}, stream);", TypeHelper.GetSimpleTypeName(typeName), elementAccess);
            }
        }
        else if (isUnmanagedType)
        {
            var friendlyTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeName);
            sb.WriteLineFormat
            (
                "{0}.Write{1}({2}{3}, ({1}){4});",
                helper,
                friendlyTypeName,
                refOrEmpty,
                target,
                elementAccess
            );
        }
        else if (isStringType)
        {
            sb.WriteLineFormat
            (
                "{0}.WriteString({1}{2}, {3});",
                helper,
                refOrEmpty,
                target,
                elementAccess
            );
        }
    }

    private static void GenerateSpanPolymorphicCollectionSerialization
        (IndentedStringBuilder sb, MemberToGenerate member, PolymorphicInfo info, CollectionInfo collectionInfo)
    {
        GeneratePolymorphicCollection
        (
            sb,
            member,
            info,
            "data",
            "SpanWriter"
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
            "stream",
            "StreamWriter"
        );
    }

    private static void GeneratePolymorphicCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        PolymorphicInfo info,
        string target,
        string helper
    )
    {
        var defaultOption = info.Options.FirstOrDefault();
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        var refOrEmpty = target == "data" ? "ref " : "";

        if (target == "stream")
        {
            sb.WriteLine("if (!stream.CanSeek)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("throw new NotSupportedException(\"Stream must be seekable to serialize this collection.\");");
            }

            sb.WriteLine("var countPosition = stream.Position;");
            sb.WriteLine("StreamWriter.WriteInt32(stream, 0); // Placeholder for count");
        }
        else
        {
            sb.WriteLine("var countSpan = data;");
            sb.WriteLine("data = data.Slice(sizeof(int));");
        }

        sb.WriteLine("int count = 0;");
        sb.WriteLine($"using var enumerator = obj.{member.Name}.GetEnumerator();");

        sb.WriteLine("if (!enumerator.MoveNext())");
        using (sb.BeginBlock())
        {
            if (target == "data")
            {
                sb.WriteLine("SpanWriter.WriteInt32(ref countSpan, 0); // Write count as 0");
            }

            PolymorphicUtilities.GenerateWriteTypeIdCode
            (
                sb,
                defaultOption,
                info,
                target,
                helper
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
                ($"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{enumerator.Current.GetType().Name}}\")");
            sb.Unindent();
            sb.WriteLine("};");

            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
            sb.WriteLineFormat($"{helper}.Write{typeIdTypeName}({refOrEmpty}{target}, discriminator);");

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
                            if (target == "data")
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

            if (target == "stream")
            {
                sb.WriteLine("var endPosition = stream.Position;");
                sb.WriteLine("stream.Position = countPosition;");
                sb.WriteLine("StreamWriter.WriteInt32(stream, count);");
                sb.WriteLine("stream.Position = endPosition;");
            }
            else
            {
                sb.WriteLine("SpanWriter.WriteInt32(ref countSpan, count);");
            }
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
}
