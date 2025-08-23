using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
///     Generates Serialize method implementations
/// </summary>
public static class SerializationGenerator
{
    private enum Sink
    {
        Span,
        Stream
    }

    private static string Ref(Sink s) => s == Sink.Span ? "ref " : "";
    private static string TargetVar(Sink s) => s == Sink.Span ? "data" : "stream";
    private static string WriterType(Sink s) => s == Sink.Span ? "SpanWriter" : "StreamWriter";

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

        GenerateSerializationBody(sb, typeToGenerate, Sink.Span);

        sb.WriteLine("return originalData.Length - data.Length;");
    }

    private static void GenerateStreamSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLineFormat("public static void Serialize({0} obj, System.IO.Stream stream)", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        GenerateSerializationBody(sb, typeToGenerate, Sink.Stream);
    }

    private static void GenerateSerializationBody(
        IndentedStringBuilder sb,
        TypeToGenerate typeToGenerate,
        Sink sink
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
                        WriterType(sink),
                        writeMethod,
                        Ref(sink),
                        TargetVar(sink),
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
                            WriterType(sink),
                            writeMethod,
                            Ref(sink),
                            TargetVar(sink),
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
                            sb.WriteLineFormat("{0} => ({1}){2},", TypeHelper.GetSimpleTypeName(option.Type), typeIdType, key);
                        }

                        sb.WriteLine(
                            $"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{firstItem.GetType().Name}}\")"
                        );
                        sb.Unindent();
                        sb.WriteLine("};");

                        sb.WriteLineFormat
                        (
                            "{0}.{1}({2}{3}, discriminator);",
                            WriterType(sink),
                            writeMethod,
                            Ref(sink),
                            TargetVar(sink)
                        );
                    }
                }
                else
                {
                    GenerateMemberSerialization(sb, member, sink);
                }
            }
            else
            {
                GenerateMemberSerialization(sb, member, sink);
            }
        }
    }

    private static void GenerateMemberSerialization(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink
    )
    {
        if (member.IsList || member.IsCollection)
        {
            GenerateCollectionSerialization(sb, member, sink);
        }
        else if (member.PolymorphicInfo is not null)
        {
            GeneratePolymorphicSerialization(sb, member, sink);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat("throw new System.NullReferenceException($\"Property \\\"{0}\\\" cannot be null.\");", member.Name);
            }

            if (sink == Sink.Span)
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
                WriterType(sink),
                Ref(sink),
                TargetVar(sink),
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
                WriterType(sink),
                writeMethod,
                Ref(sink),
                TargetVar(sink),
                typeName,
                member.Name
            );
        }
    }

    private static void GenerateCollectionSerialization(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink
    )
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
                    sink,
                    collectionInfo
                );
                return;
            }
        }

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
                        sink,
                        collectionInfo
                    );
                }
                else
                {
                    GenerateStandardCollectionBody(sb, member, sink);
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

            GeneratePolymorphicCollection(sb, member, info, sink);
            return;
        }

        if (isNotListOrArray && member.CollectionTypeInfo?.IsPureEnumerable == true && !isByteCollection && !(member.CollectionInfo?.CountSize > 0))
        {
            GenerateEnumerableCollection
            (
                sb,
                member,
                sink,
                collectionInfo
            );
            return;
        }

        if (member.CollectionInfo?.CountSize is > 0)
        {
            sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat("throw new System.ArgumentNullException(nameof(obj.{0}), \"Fixed-size collections cannot be null.\");", member.Name);
            }
            sb.WriteLine("else");
            using (sb.BeginBlock())
            {
                HandleNonNullCollection
                (
                    sb,
                    member,
                    sink,
                    collectionInfo
                );
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
                sink,
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
                sink,
                collectionInfo
            );
        }
    }

    private static void HandleNonNullCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink,
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
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
            var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4}){5});",
                WriterType(sink),
                countWriteMethod,
                Ref(sink),
                TargetVar(sink),
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
                sink,
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
                WriterType(sink),
                Ref(sink),
                TargetVar(sink),
                member.Name
            );
        }
        else
        {
            GenerateStandardCollectionBody(sb, member, sink);
        }
    }

    private static void GeneratePolymorphicCollectionBody(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink,
        CollectionInfo collectionInfo
    )
    {
        if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
        {
            GenerateIndividualTypeIdCollection(sb, member, sink);
        }
        else if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
        {
            GenerateSingleTypeIdPolymorphicCollection
            (
                sb,
                member,
                sink,
                collectionInfo
            );
        }
    }

    private static void GenerateSingleTypeIdPolymorphicCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink,
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
                sink,
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
                sink,
                info
            );
        }
    }

    private static void GenerateSingleTypeIdPolymorphicCollectionWithProperty(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink,
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
                            if (sink == Sink.Span)
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

    private static void GenerateSingleTypeIdPolymorphicCollectionImplicit(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink,
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
        var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;

        sb.WriteLine($"if ({listItemsVar} is null || {listItemsVar}.Count == 0)");
        using (sb.BeginBlock())
        {
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4})0);",
                WriterType(sink),
                countWriteMethod,
                Ref(sink),
                TargetVar(sink),
                countType
            );
            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
            var defaultKey = PolymorphicUtilities.FormatTypeIdKey(defaultOption.Key, info);

            sb.WriteLineFormat(
                "{0}.Write{1}({2}{3}, ({4}){5});",
                WriterType(sink),
                typeIdTypeName,
                Ref(sink),
                TargetVar(sink),
                typeIdType,
                defaultKey
            );
        }

        sb.WriteLine("else");
        using (sb.BeginBlock())
        {
            var countExpression = $"{listItemsVar}.Count";
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4}){5});",
                WriterType(sink),
                countWriteMethod,
                Ref(sink),
                TargetVar(sink),
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
                WriterType(sink),
                typeIdTypeName,
                Ref(sink),
                TargetVar(sink)
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
                            if (sink == Sink.Span)
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

    private static void GenerateIndividualTypeIdCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink
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
            sink
        );
    }

    private static void GenerateStandardCollectionBody(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink
    )
    {
        if (member.CollectionTypeInfo?.IsArray == true || member.IsList)
        {
            GenerateListOrArraySerialization(sb, member, sink);
        }
        else
        {
            GenerateEnumerableSerialization(sb, member, sink);
        }
    }

    private static void GenerateEnumerableSerialization(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink
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
                sink
            );
        }
    }

    private static void GenerateListOrArraySerialization(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink
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
                sink
            );
        }
        else if (member.CollectionTypeInfo is not null)
        {
            GenerateCollectionElementSerialization
            (
                sb,
                member.CollectionTypeInfo.Value,
                $"obj.{member.Name}[i]",
                sink
            );
        }
    }

    private static void HandleNullCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink,
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
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);

            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4})0);",
                WriterType(sink),
                countWriteMethod,
                Ref(sink),
                TargetVar(sink),
                countType
            );
            var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
            var defaultKey = PolymorphicUtilities.FormatTypeIdKey(defaultOption.Key, info);

            sb.WriteLineFormat(
                "{0}.Write{1}({2}{3}, ({4}){5});",
                WriterType(sink),
                typeIdTypeName,
                Ref(sink),
                TargetVar(sink),
                typeIdType,
                defaultKey
            );
        }
        else if (collectionInfo.CountType != null || collectionInfo.CountSizeReferenceIndex is not null)
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
            sb.WriteLineFormat
            (
                "{0}.{1}({2}{3}, ({4})0);",
                WriterType(sink),
                countWriteMethod,
                Ref(sink),
                TargetVar(sink),
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
                WriterType(sink),
                countWriteMethod,
                Ref(sink),
                TargetVar(sink),
                countType
            );
        }
    }

    private static void GeneratePolymorphicSerialization(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink
    )
    {
        var info = member.PolymorphicInfo!.Value;

        sb.WriteLineFormat("switch (obj.{0})", member.Name);
        using var _ = sb.BeginBlock();

        GeneratePolymorphicSerializationLogic
        (
            sb,
            sink,
            info,
            false
        );

        sb.WriteLine("case null:");
        sb.WriteLineFormat("    throw new System.NullReferenceException($\"Property \\\"{0}\\\" cannot be null.\");", member.Name);
        sb.WriteLine("default:");
        sb.WriteLineFormat
        (
            "    throw new System.IO.InvalidDataException($\"Unknown type for {0}: {{obj.{0}?.GetType().FullName}}\");",
            member.Name
        );
    }

    private static void GeneratePolymorphicItemSerialization(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string instanceName,
        Sink sink
    )
    {
        var info = member.PolymorphicInfo!.Value;

        sb.WriteLineFormat("switch ({0})", instanceName);
        using var _ = sb.BeginBlock();

        GeneratePolymorphicSerializationLogic
        (
            sb,
            sink,
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

    private static void GeneratePolymorphicSerializationLogic(
        IndentedStringBuilder sb,
        Sink sink,
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
                var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
                var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);

                sb.WriteLineFormat(
                    "{0}.Write{1}({2}{3}, ({4}){5});",
                    WriterType(sink),
                    typeIdTypeName,
                    Ref(sink),
                    TargetVar(sink),
                    typeIdType,
                    key
                );
            }

            if (sink == Sink.Span)
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

    private static void GenerateListElementSerialization(
        IndentedStringBuilder sb,
        ListTypeArgumentInfo elementInfo,
        string elementAccess,
        Sink sink
    )
    {
        GenerateElementSerialization
        (
            sb,
            elementAccess,
            sink,
            elementInfo.TypeName,
            elementInfo.HasGenerateSerializerAttribute,
            elementInfo.IsUnmanagedType,
            elementInfo.IsStringType
        );
    }

    private static void GenerateCollectionElementSerialization(
        IndentedStringBuilder sb,
        CollectionTypeInfo elementInfo,
        string elementAccess,
        Sink sink
    )
    {
        GenerateElementSerialization
        (
            sb,
            elementAccess,
            sink,
            elementInfo.ElementTypeName,
            elementInfo.HasElementGenerateSerializerAttribute,
            elementInfo.IsElementUnmanagedType,
            elementInfo.IsElementStringType
        );
    }

    private static void GenerateElementSerialization(
        IndentedStringBuilder sb,
        string elementAccess,
        Sink sink,
        string typeName,
        bool hasGenerateSerializerAttribute,
        bool isUnmanagedType,
        bool isStringType
    )
    {
        if (hasGenerateSerializerAttribute)
        {
            if (sink == Sink.Span)
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
                WriterType(sink),
                friendlyTypeName,
                Ref(sink),
                TargetVar(sink),
                elementAccess
            );
        }
        else if (isStringType)
        {
            sb.WriteLineFormat
            (
                "{0}.WriteString({1}{2}, {3});",
                WriterType(sink),
                Ref(sink),
                TargetVar(sink),
                elementAccess
            );
        }
    }

    private static void GenerateEnumerableCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        Sink sink,
        CollectionInfo collectionInfo
    )
    {
        if (collectionInfo.CountSize > 0)
        {
            sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
            using (sb.BeginBlock())
            {
                HandleNullCollection(sb, member, sink, collectionInfo, true);
            }
            sb.WriteLine("else");
            using (sb.BeginBlock())
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

                sb.WriteLineFormat("foreach (var item in obj.{0})", member.Name);
                using (sb.BeginBlock())
                {
                    var elementInfo = member.CollectionTypeInfo!.Value;
                    GenerateElementSerialization
                    (
                        sb,
                        "item",
                        sink,
                        elementInfo.ElementTypeName,
                        elementInfo.HasElementGenerateSerializerAttribute,
                        elementInfo.IsElementUnmanagedType,
                        elementInfo.IsElementStringType
                    );
                }
            }
            return;
        }

        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();

        EmitCountHeader(sb, sink, countType, out var cookieVar);

        sb.WriteLine("int count = 0;");
        sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
        using (sb.BeginBlock())
        {
            sb.WriteLine($"using var enumerator = obj.{member.Name}.GetEnumerator();");
            sb.WriteLine("while (enumerator.MoveNext())");
            using (sb.BeginBlock())
            {
                var elementInfo = member.CollectionTypeInfo!.Value;
                GenerateElementSerialization
                (
                    sb,
                    "enumerator.Current",
                    sink,
                    elementInfo.ElementTypeName,
                    elementInfo.HasElementGenerateSerializerAttribute,
                    elementInfo.IsElementUnmanagedType,
                    elementInfo.IsElementStringType
                );
                sb.WriteLine("count++;");
            }
        }

        EmitCountCommit(sb, sink, countType, "count", cookieVar);
    }

    private static void GeneratePolymorphicCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        PolymorphicInfo info,
        Sink sink
    )
    {
        var defaultOption = info.Options.FirstOrDefault();
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;

        EmitCountHeader(sb, sink, "int", out var cookieVar);

        sb.WriteLine("int count = 0;");
        sb.WriteLine($"using var enumerator = obj.{member.Name}.GetEnumerator();");

        sb.WriteLine("if (!enumerator.MoveNext())");
        using (sb.BeginBlock())
        {
            if (sink == Sink.Span)
            {
                EmitCountCommit(sb, sink, "int", "0", cookieVar);
            }

            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
            var defaultKey = PolymorphicUtilities.FormatTypeIdKey(defaultOption.Key, info);

            sb.WriteLineFormat(
                "{0}.Write{1}({2}{3}, ({4}){5});",
                WriterType(sink),
                typeIdTypeName,
                Ref(sink),
                TargetVar(sink),
                typeIdType,
                defaultKey
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

            sb.WriteLine(
                $"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{enumerator.Current.GetType().Name}}\")"
            );
            sb.Unindent();
            sb.WriteLine("};");

            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
            sb.WriteLineFormat(
                "{0}.Write{1}({2}{3}, discriminator);",
                WriterType(sink),
                typeIdTypeName,
                Ref(sink),
                TargetVar(sink)
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
                        sb.WriteLine("do");
                        using (sb.BeginBlock())
                        {
                            if (sink == Sink.Span)
                            {
                                sb.WriteLine(
                                    $"var bytesWritten = {typeName}.Serialize(({typeName})enumerator.Current, data);"
                                );
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

            EmitCountCommit(sb, sink, "int", "count", cookieVar);
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

    private static void EmitCountHeader(
        IndentedStringBuilder sb,
        Sink sink,
        string countType,
        out string cookieVar
    )
    {
        cookieVar = sink == Sink.Span ? "countSpan" : "countPosition";
        if (sink == Sink.Stream)
        {
            sb.WriteLine("if (!stream.CanSeek)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("throw new NotSupportedException(\"Stream must be seekable to serialize this collection.\");");
            }

            sb.WriteLine($"var {cookieVar} = stream.Position;");
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
            sb.WriteLineFormat(
                "{0}.{1}(stream, ({2})0); // Placeholder for count",
                WriterType(Sink.Stream),
                countWriteMethod,
                countType
            );
        }
        else
        {
            sb.WriteLine($"var {cookieVar} = data;");
            sb.WriteLine($"data = data.Slice(sizeof({countType}));");
        }
    }

    private static void EmitCountCommit(
        IndentedStringBuilder sb,
        Sink sink,
        string countType,
        string countVar,
        string cookieVar
    )
    {
        var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
        if (sink == Sink.Stream)
        {
            sb.WriteLine("var endPosition = stream.Position;");
            sb.WriteLine($"stream.Position = {cookieVar};");
            sb.WriteLineFormat(
                "{0}.{1}(stream, ({2}){3});",
                WriterType(Sink.Stream),
                countWriteMethod,
                countType,
                countVar
            );
            sb.WriteLine("stream.Position = endPosition;");
        }
        else
        {
            sb.WriteLineFormat("{0}.{1}(ref {2}, {3});", WriterType(Sink.Span), countWriteMethod, cookieVar, countVar);
        }
    }
}
