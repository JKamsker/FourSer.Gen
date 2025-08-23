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

    private enum CollMode { Bytes, PureEnum, FixedSize, CountRef, PolySingleId, PolyPerItemId, Normal }

    private static string Ref(Sink s) => s == Sink.Span ? "ref " : "";
    private static string TargetVar(Sink s) => s == Sink.Span ? "data" : "stream";
    private static string WriterType(Sink s) => s == Sink.Span ? "SpanWriter" : "StreamWriter";

    private static void EmitSerializeNestedOrThrow(
    IndentedStringBuilder sb, Sink s, string typeName, string access, bool nullCheck)
    {
        if (nullCheck)
        {
            sb.WriteLineFormat("if ({0} is null)", access);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat("throw new System.NullReferenceException($\"Property \\\"{0}\\\" cannot be null.\");", access.Split('.').Last());
            }
        }
        if (s == Sink.Span)
        {
            sb.WriteLineFormat("var bytesWritten = {0}.Serialize({1}, data);", typeName, access);
            sb.WriteLine("data = data.Slice(bytesWritten);");
        }
        else
        {
            sb.WriteLineFormat("{0}.Serialize({1}, stream);", typeName, access);
        }
    }

    public static void GenerateSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        GenerateSpanSerialize(sb, typeToGenerate);
        sb.WriteLine();
        GenerateStreamSerialize(sb, typeToGenerate);
    }

    private static void GenerateSpanSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        // Emits: public static int Serialize(MyType obj, System.Span<byte> data)
        sb.WriteLineFormat("public static int Serialize({0} obj, System.Span<byte> data)", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        sb.WriteLine("var originalData = data;");

        GenerateSerializationBody(sb, typeToGenerate, Sink.Span);

        sb.WriteLine("return originalData.Length - data.Length;");
    }

    private static void GenerateStreamSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        // Emits: public static void Serialize(MyType obj, System.IO.Stream stream)
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
        PolymorphicUtilities.GenerateTypeIdPrePass(sb, typeToGenerate);

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
                    // Emits: SpanWriter.WriteInt32(ref data, (Int32)(obj.MyCollection?.Count ?? 0));
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

                    // Emits: if (obj.MyItems is null || obj.MyItems.Count == 0)
                    sb.WriteLineFormat("if (obj.{0} is null || obj.{0}.Count == 0)", collectionName);
                    using (sb.BeginBlock())
                    {
                        // Emits: SpanWriter.WriteByte(ref data, (byte)10);
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
                        // Emits: var firstItem = obj.MyItems[0];
                        sb.WriteLineFormat("var firstItem = obj.{0}[0];", collectionName);
                        sb.WriteLine("var discriminator = firstItem switch");
                        sb.WriteLine("{");
                        sb.Indent();
                        foreach (var option in info.Options)
                        {
                            var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                            // Emits: Sword => (byte)10,
                            sb.WriteLineFormat("{0} => ({1}){2},", TypeHelper.GetSimpleTypeName(option.Type), typeIdType, key);
                        }

                        // Emits: _ => throw new System.IO.InvalidDataException($"Unknown item type: {firstItem.GetType().Name}")
                        sb.WriteLine(
                            "_ => throw new System.IO.InvalidDataException($\"Unknown item type: {firstItem.GetType().Name}\")"
                        );
                        sb.Unindent();
                        sb.WriteLine("};");

                        // Emits: SpanWriter.WriteByte(ref data, discriminator);
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
            EmitSerializeNestedOrThrow(sb, sink, TypeHelper.GetSimpleTypeName(member.TypeName), $"obj.{member.Name}", true);
        }
        else if (member.IsStringType)
        {
            // Emits: SpanWriter.WriteString(ref data, obj.MyString);
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
            // Emits: SpanWriter.WriteInt32(ref data, (Int32)obj.MyInt);
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

    private static CollMode GetCollectionMode(MemberToGenerate member)
    {
        var collectionInfo = member.CollectionInfo!.Value;
        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);
        if (isByteCollection) return CollMode.Bytes;

        var isNotListOrArray = !member.IsList && member.CollectionTypeInfo?.IsArray != true;
        if (member.CollectionTypeInfo?.IsPureEnumerable == true && !GeneratorUtilities.ShouldUsePolymorphicSerialization(member)) return CollMode.PureEnum;
        if (collectionInfo.CountSize > 0) return CollMode.FixedSize;
        if (collectionInfo.CountSizeReferenceIndex is not null) return CollMode.CountRef;

        var useSingleTypeIdPoly = isNotListOrArray
            && GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            && collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId
            && string.IsNullOrEmpty(collectionInfo.TypeIdProperty);
        if (useSingleTypeIdPoly) return CollMode.PolySingleId;

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member) && collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds) return CollMode.PolyPerItemId;

        return CollMode.Normal;
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

        var collectionMode = GetCollectionMode(member);

        switch (collectionMode)
        {
            case CollMode.Bytes:
                sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
                using (sb.BeginBlock())
                {
                    HandleNullCollection(sb, member, sink, collectionInfo, !member.IsList && member.CollectionTypeInfo?.IsArray != true);
                }
                sb.WriteLine("else");
                using (sb.BeginBlock())
                {
                    HandleNonNullCollection(sb, member, sink, collectionInfo);
                }
                break;
            case CollMode.PureEnum:
                GenerateEnumerableCollection(sb, member, sink, collectionInfo);
                break;
            case CollMode.FixedSize:
                // Emits: if (obj.MyItems is null)
                sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
                using (sb.BeginBlock())
                {
                    // Emits: throw new System.ArgumentNullException(nameof(obj.MyItems), "Fixed-size collections cannot be null.");
                    sb.WriteLineFormat("throw new System.ArgumentNullException(nameof(obj.{0}), \"Fixed-size collections cannot be null.\");", member.Name);
                }
                sb.WriteLine("else");
                using (sb.BeginBlock())
                {
                    HandleNonNullCollection(sb, member, sink, collectionInfo);
                }
                break;
            case CollMode.CountRef:
                // Emits: if (obj.MyItems is not null)
                sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
                using (sb.BeginBlock())
                {
                    if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
                    {
                        GeneratePolymorphicCollectionBody(sb, member, sink, collectionInfo);
                    }
                    else
                    {
                        GenerateStandardCollectionBody(sb, member, sink);
                    }
                }
                break;
            case CollMode.PolySingleId:
                if (member.PolymorphicInfo is { } info)
                {
                    GeneratePolymorphicCollection(sb, member, info, sink);
                }
                break;
            case CollMode.Normal:
            case CollMode.PolyPerItemId:
                // Emits: if (obj.MyItems is null)
                sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
                using (sb.BeginBlock())
                {
                    HandleNullCollection(sb, member, sink, collectionInfo, !member.IsList && member.CollectionTypeInfo?.IsArray != true);
                }
                sb.WriteLine("else");
                using (sb.BeginBlock())
                {
                    HandleNonNullCollection(sb, member, sink, collectionInfo);
                }
                break;
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
            // Emits: if (obj.MyItems.Count != 5)
            sb.WriteLineFormat("if ({0} != {1})", countExpression, collectionInfo.CountSize);
            using (sb.BeginBlock())
            {
                // Emits: throw new System.InvalidOperationException($"Collection 'MyItems' must have a size of 5 but was {obj.MyItems.Count}.");
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
            // Emits: SpanWriter.WriteInt32(ref data, (Int32)obj.MyItems.Count);
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
            // Emits: SpanWriter.WriteBytes(ref data, obj.MyBytes);
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
        // Emits: if (obj.MyItems.Count > 0)
        sb.WriteLineFormat("if (obj.{0}.Count > 0)", member.Name);
        using (sb.BeginBlock())
        {
            // Emits: switch (obj.MyItems[0])
            sb.WriteLineFormat("switch (obj.{0}[0])", member.Name);
            using (sb.BeginBlock())
            {
                foreach (var option in info.Options)
                {
                    var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                    // Emits: case Sword:
                    sb.WriteLineFormat("case {0}:", typeName);
                    using (sb.BeginBlock())
                    {
                        // Emits: foreach(var item in obj.MyItems)
                        sb.WriteLineFormat("foreach(var item in obj.{0})", member.Name);
                        using (sb.BeginBlock())
                        {
                            if (sink == Sink.Span)
                            {
                                // Emits: var bytesWritten = Sword.Serialize((Sword)item, data);
                                sb.WriteLineFormat
                                (
                                    "var bytesWritten = {0}.Serialize(({0})item, data);",
                                    typeName
                                );
                                sb.WriteLine("data = data.Slice(bytesWritten);");
                            }
                            else
                            {
                                // Emits: Sword.Serialize((Sword)item, stream);
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
                // Emits: throw new System.IO.InvalidDataException($"Unknown type for item in MyItems: {obj.MyItems[0].GetType().Name}");
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

        // Emits: var myItems = obj.MyItems;
        sb.WriteLineFormat("var {0} = obj.{1};", listItemsVar, member.Name);

        var defaultOption = info.Options.FirstOrDefault();
        if (defaultOption.Equals(default(PolymorphicOption)))
        {
            return;
        }

        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;

        // Emits: if (myItems is null || myItems.Count == 0)
        sb.WriteLineFormat("if ({0} is null || {0}.Count == 0)", listItemsVar);
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

            // Emits: SpanWriter.WriteByte(ref data, (byte)10);
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
            // Emits: SpanWriter.WriteInt32(ref data, (Int32)myItems.Count);
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

            // Emits: var firstItem = myItems[0];
            sb.WriteLineFormat("var firstItem = {0}[0];", listItemsVar);
            sb.WriteLine("var discriminator = firstItem switch");
            sb.WriteLine("{");
            sb.Indent();
            foreach (var option in info.Options)
            {
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                // Emits: Sword => (byte)10,
                sb.WriteLineFormat("{0} => ({1}){2},", TypeHelper.GetSimpleTypeName(option.Type), typeIdType, key);
            }

            // Emits: _ => throw new System.IO.InvalidDataException($"Unknown item type: {firstItem.GetType().Name}")
            sb.WriteLine("_ => throw new System.IO.InvalidDataException($\"Unknown item type: {firstItem.GetType().Name}\")");
            sb.Unindent();
            sb.WriteLine("};");

            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
            // Emits: SpanWriter.WriteByte(ref data, discriminator);
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
                    // Emits: case 10:
                    sb.WriteLineFormat("case {0}:", key);
                    using (sb.BeginBlock())
                    {
                        // Emits: for (int i = 0; i < myItems.Count; i++)
                        sb.WriteLineFormat("for (int i = 0; i < {0}; i++)", countExpression);
                        using (sb.BeginBlock())
                        {
                            if (sink == Sink.Span)
                            {
                                // Emits: var bytesWritten = Sword.Serialize((Sword)myItems[i], data);
                                sb.WriteLineFormat("var bytesWritten = {0}.Serialize(({0}){1}[i], data);", typeName, listItemsVar);
                                sb.WriteLine("data = data.Slice(bytesWritten);");
                            }
                            else
                            {
                                // Emits: Sword.Serialize((Sword)myItems[i], stream);
                                sb.WriteLineFormat("{0}.Serialize(({0}){1}[i], stream);", typeName, listItemsVar);
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
        // Emits: foreach(var item in obj.MyItems)
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
        // Emits: foreach (var item in obj.MyItems)
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
        // Emits: for (int i = 0; i < obj.MyItems.Count; i++)
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

            // Emits: SpanWriter.WriteByte(ref data, (byte)10);
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
            // Emits: SpanWriter.WriteInt32(ref data, (Int32)0);
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
            // Emits: SpanWriter.WriteInt32(ref data, (Int32)0);
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

        // Emits: switch (obj.MyProperty)
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
        // Emits:     throw new System.NullReferenceException($"Property \"MyProperty\" cannot be null.");
        sb.WriteLineFormat("    throw new System.NullReferenceException($\"Property \\\"{0}\\\" cannot be null.\");", member.Name);
        sb.WriteLine("default:");
        // Emits:     throw new System.IO.InvalidDataException($"Unknown type for MyProperty: {obj.MyProperty?.GetType().FullName}");
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

        // Emits: switch (item)
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
        // Emits:     throw new System.IO.InvalidDataException($"Unknown type for item: {item?.GetType().FullName}");
        sb.WriteLineFormat
        (
            "    throw new System.IO.InvalidDataException($\"Unknown type for {0}: {{{0}?.GetType().FullName}}\");",
            instanceName
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
            // Emits: case Sword typedInstance:
            sb.WriteLineFormat("case {0} typedInstance:", typeName);
            using var __ = sb.BeginBlock();
            if (info.TypeIdPropertyIndex is null)
            {
                var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
                var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);

                // Emits: SpanWriter.WriteByte(ref data, (byte)10);
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
                // Emits: var bytesWritten = Sword.Serialize(typedInstance, data);
                sb.WriteLineFormat("var bytesWritten = {0}.Serialize(typedInstance, data);", typeName);
                sb.WriteLine("data = data.Slice(bytesWritten);");
            }
            else
            {
                // Emits: Sword.Serialize(typedInstance, stream);
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
            EmitSerializeNestedOrThrow(sb, sink, TypeHelper.GetSimpleTypeName(typeName), elementAccess, false);
        }
        else if (isUnmanagedType)
        {
            var friendlyTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeName);
            // Emits: SpanWriter.WriteInt32(ref data, (Int32)item);
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
            // Emits: SpanWriter.WriteString(ref data, item);
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
            // Emits: if (obj.MyItems is null)
            sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
            using (sb.BeginBlock())
            {
                HandleNullCollection(sb, member, sink, collectionInfo, true);
            }
            sb.WriteLine("else");
            using (sb.BeginBlock())
            {
                var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
                // Emits: if (obj.MyItems.Count() != 5)
                sb.WriteLineFormat("if ({0} != {1})", countExpression, collectionInfo.CountSize);
                using (sb.BeginBlock())
                {
                    // Emits: throw new System.InvalidOperationException($"Collection 'MyItems' must have a size of 5 but was {obj.MyItems.Count()}.");
                    sb.WriteLineFormat(
                        "throw new System.InvalidOperationException($\"Collection '{0}' must have a size of {1} but was {{{2}}}.\");",
                        member.Name,
                        collectionInfo.CountSize,
                        countExpression
                    );
                }

                // Emits: foreach (var item in obj.MyItems)
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
        // Emits: if (obj.MyItems is not null)
        sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
        using (sb.BeginBlock())
        {
            // Emits: using var enumerator = obj.MyItems.GetEnumerator();
        sb.WriteLineFormat("using var enumerator = obj.{0}.GetEnumerator();", member.Name);
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
        // Emits: using var enumerator = obj.MyItems.GetEnumerator();
        sb.WriteLineFormat("using var enumerator = obj.{0}.GetEnumerator();", member.Name);

        // Emits: if (!enumerator.MoveNext())
        sb.WriteLine("if (!enumerator.MoveNext())");
        using (sb.BeginBlock())
        {
            if (sink == Sink.Span)
            {
                EmitCountCommit(sb, sink, "int", "0", cookieVar);
            }

            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
            var defaultKey = PolymorphicUtilities.FormatTypeIdKey(defaultOption.Key, info);

            // Emits: SpanWriter.WriteByte(ref data, (byte)10);
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
            // Emits: var discriminator = enumerator.Current switch
            sb.WriteLine("var discriminator = enumerator.Current switch");
            sb.WriteLine("{");
            sb.Indent();
            foreach (var option in info.Options)
            {
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                // Emits: Sword => (byte)10,
                sb.WriteLineFormat("{0} => ({1}){2},", TypeHelper.GetSimpleTypeName(option.Type), typeIdType, key);
            }

            // Emits: _ => throw new System.IO.InvalidDataException($"Unknown item type: {enumerator.Current.GetType().Name}")
            sb.WriteLine(
                "_ => throw new System.IO.InvalidDataException($\"Unknown item type: {enumerator.Current.GetType().Name}\")"
            );
            sb.Unindent();
            sb.WriteLine("};");

            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(typeIdType);
            // Emits: SpanWriter.WriteByte(ref data, discriminator);
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
                    // Emits: case 10:
                    sb.WriteLineFormat("case {0}:", key);
                    using (sb.BeginBlock())
                    {
                        sb.WriteLine("do");
                        using (sb.BeginBlock())
                        {
                            if (sink == Sink.Span)
                            {
                                // Emits: var bytesWritten = Sword.Serialize((Sword)enumerator.Current, data);
                                sb.WriteLineFormat
                                (
                                    "var bytesWritten = {0}.Serialize(({0})enumerator.Current, data);",
                                    typeName
                                );
                                sb.WriteLine("data = data.Slice(bytesWritten);");
                            }
                            else
                            {
                                // Emits: Sword.Serialize((Sword)enumerator.Current, stream);
                                sb.WriteLineFormat("{0}.Serialize(({0})enumerator.Current, stream);", typeName);
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

            // Emits: var countPosition = stream.Position;
            sb.WriteLineFormat("var {0} = stream.Position;", cookieVar);
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
            // Emits: StreamWriter.WriteInt32(stream, (Int32)0); // Placeholder for count
            sb.WriteLineFormat(
                "{0}.{1}(stream, ({2})0); // Placeholder for count",
                WriterType(Sink.Stream),
                countWriteMethod,
                countType
            );
        }
        else
        {
            // Emits: var countSpan = data;
            sb.WriteLineFormat("var {0} = data;", cookieVar);
            // Emits: data = data.Slice(sizeof(int));
            sb.WriteLineFormat("data = data.Slice(sizeof({0}));", GeneratorUtilities.NormalizeToKeyword(countType));
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
            sb.WriteLineFormat("stream.Position = {0};", cookieVar);
            // Emits: StreamWriter.WriteInt32(stream, (Int32)count);
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
            // Emits: SpanWriter.WriteInt32(ref countSpan, count);
            sb.WriteLineFormat("{0}.{1}(ref {2}, {3});", WriterType(Sink.Span), countWriteMethod, cookieVar, countVar);
        }
    }
}
