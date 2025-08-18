using System.Linq;
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

        // Pre-pass to update TypeId properties
        foreach (var member in typeToGenerate.Members)
        {
            if (member.PolymorphicInfo is not { } info || string.IsNullOrEmpty(info.TypeIdProperty))
            {
                continue;
            }

            // Skip collections with SingleTypeId mode - they use the TypeIdProperty directly
            if ((member.IsList || member.IsCollection) && member.CollectionInfo?.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                continue;
            }

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
                sb.WriteLineFormat("    obj.{0} = {1};", info.TypeIdProperty, key);
                sb.WriteLine("    break;");
            }

            sb.WriteLine("case null:");
            sb.WriteLine("    break;");
        }

        foreach (var member in typeToGenerate.Members)
        {
            GenerateMemberSerialization(sb, member, "data", "SpanWriter");
        }

        sb.WriteLine("return originalData.Length - data.Length;");
    }

    private static void GenerateStreamSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLineFormat("public static void Serialize({0} obj, System.IO.Stream stream)", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        // Pre-pass to update TypeId properties
        foreach (var member in typeToGenerate.Members)
        {
            if (member.PolymorphicInfo is not { } info || string.IsNullOrEmpty(info.TypeIdProperty))
            {
                continue;
            }

            if ((member.IsList || member.IsCollection) && member.CollectionInfo?.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                continue;
            }

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
                sb.WriteLineFormat("    obj.{0} = {1};", info.TypeIdProperty, key);
                sb.WriteLine("    break;");
            }

            sb.WriteLine("case null:");
            sb.WriteLine("    break;");
        }

        foreach (var member in typeToGenerate.Members)
        {
            GenerateMemberSerialization(sb, member, "stream", "StreamWriter");
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
                sb.WriteLineFormat("throw new System.NullReferenceException($\"Property \\\"{0}\\\" cannot be null.\");", member.Name);
            }

            if (target == "data")
            {
                sb.WriteLineFormat
                    ("var bytesWritten = {0}.Serialize(obj.{1}, data);", TypeHelper.GetSimpleTypeName(member.TypeName), member.Name);
                sb.WriteLine("data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.WriteLineFormat("{0}.Serialize(obj.{1}, stream);", TypeHelper.GetSimpleTypeName(member.TypeName), member.Name);
            }
        }
        else if (member.IsStringType)
        {
            sb.WriteLineFormat("{0}.WriteString({1}{2}, obj.{3});", helper, refOrEmpty, target, member.Name);
        }
        else if (member.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
            var writeMethod = $"Write{typeName}";
            sb.WriteLineFormat("{0}.{1}({2}{3}, ({4})obj.{5});", helper, writeMethod, refOrEmpty, target, typeName, member.Name);
        }
    }

    private static void GenerateCollectionSerialization
        (IndentedStringBuilder sb, MemberToGenerate member, string target, string helper)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        var refOrEmpty = target == "data" ? "ref " : "";

        sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
        using (sb.BeginBlock())
        {
            if (collectionInfo.CountType != null || !string.IsNullOrEmpty(collectionInfo.CountSizeReference))
            {
                var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
                var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
                sb.WriteLineFormat("{0}.{1}({2}{3}, ({4})0);", helper, countWriteMethod, refOrEmpty, target, countType);
            }
            else
            {
                sb.WriteLineFormat("throw new System.NullReferenceException($\"Collection \\\"{0}\\\" cannot be null.\");", member.Name);
            }
        }

        sb.WriteLine("else");
        using var _ = sb.BeginBlock();
        var isHandledByPolymorphic = collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId && string.IsNullOrEmpty(collectionInfo.TypeIdProperty);

        if (collectionInfo is { Unlimited: false } && string.IsNullOrEmpty(collectionInfo.CountSizeReference) && !isHandledByPolymorphic)
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);

            var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
            sb.WriteLineFormat
                ("{0}.{1}({2}{3}, ({4}){5});", helper, countWriteMethod, refOrEmpty, target, countType, countExpression);
        }

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
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
                    LocationInfo.None
                );

                GeneratePolymorphicItemSerialization
                (
                    sb,
                    itemMember,
                    "item",
                    target,
                    helper
                );
                return;
            }

            if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                if (member.PolymorphicInfo is not { } info)
                {
                    return;
                }

                var typeIdProperty = collectionInfo.TypeIdProperty;

                if (string.IsNullOrEmpty(typeIdProperty))
                {
                    // New logic: derive type from the first element
                    var memberName = $"obj.{member.Name}";
                    var itemsVar = member.Name.ToCamelCase();
                    if (itemsVar == "items")
                    {
                        itemsVar = "collectionItems"; // Avoid conflict with loop variable
                    }

                    var isListOrArray = member.IsList || (member.CollectionTypeInfo?.IsArray ?? false);
                    if (isListOrArray)
                    {
                        sb.WriteLine($"var {itemsVar} = obj.{member.Name};");
                    }
                    else
                    {
                        sb.WriteLine($"var {itemsVar} = obj.{member.Name}.ToList();");
                    }

                    // Handle null or empty list
                    var defaultOption = info.Options.FirstOrDefault();
                    if (defaultOption.Equals(default(PolymorphicOption)))
                    {
                        // No options to serialize, this is an error case but we should handle it gracefully
                        return;
                    }

                    var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
                    var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
                    var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;

                    sb.WriteLine($"if ({itemsVar} is null || {itemsVar}.Count == 0)");
                    using (sb.BeginBlock())
                    {
                        sb.WriteLineFormat("{0}.{1}({2}{3}, ({4})0);", helper, countWriteMethod, refOrEmpty, target, countType);
                        PolymorphicUtilities.GenerateWriteTypeIdCode(sb, defaultOption, info, target, helper);
                    }
                    sb.WriteLine("else");
                    using (sb.BeginBlock())
                    {
                        // Write count
                        var countExpression = $"{itemsVar}.Count";
                        sb.WriteLineFormat("{0}.{1}({2}{3}, ({4}){5});", helper, countWriteMethod, refOrEmpty, target, countType, countExpression);

                        // Determine and write discriminator from the first item
                        sb.WriteLine($"var firstItem = {itemsVar}[0];");
                        sb.WriteLine($"var discriminator = firstItem switch");
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
                        sb.WriteLineFormat("{0}.Write{1}({2}{3}, discriminator);", helper, typeIdTypeName, refOrEmpty, target);

                        // Serialize items using a for loop
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
                                            sb.WriteLine($"var bytesWritten = {typeName}.Serialize(({typeName}){itemsVar}[i], data);");
                                            sb.WriteLine("data = data.Slice(bytesWritten);");
                                        }
                                        else
                                        {
                                            sb.WriteLine($"{typeName}.Serialize(({typeName}){itemsVar}[i], stream);");
                                        }
                                    }
                                    sb.WriteLine("break;");
                                }
                            }
                        }
                    }
                }
                else
                {
                    sb.WriteLineFormat("switch (obj.{0})", typeIdProperty);
                    using var __ = sb.BeginBlock();
                    foreach (var option in info.Options)
                    {
                        var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);

                        sb.WriteLineFormat("case {0}:", key);
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
                                        TypeHelper.GetSimpleTypeName(option.Type)
                                    );
                                    sb.WriteLine("data = data.Slice(bytesWritten);");
                                }
                                else
                                {
                                    sb.WriteLineFormat
                                    (
                                        "{0}.Serialize(({0})item, stream);",
                                        TypeHelper.GetSimpleTypeName(option.Type)
                                    );
                                }
                            }

                            sb.WriteLine("break;");
                        }
                    }

                    sb.WriteLine("default:");
                    var localTypeIdProperty = typeIdProperty;
                    sb.WriteLineFormat
                    (
                        "    throw new System.IO.InvalidDataException($\"Unknown type id for {0}: {{obj.{1}}}\");",
                        member.Name, localTypeIdProperty
                    );
                }
                return;
            }
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (isByteCollection)
        {
            sb.WriteLineFormat("{0}.WriteBytes({1}{2}, obj.{3});", helper, refOrEmpty, target, member.Name);
        }
        else
        {
            if (member.CollectionTypeInfo?.IsArray == true || member.IsList)
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
            else
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
        }
    }

    private static void GeneratePolymorphicSerialization
        (IndentedStringBuilder sb, MemberToGenerate member, string target, string helper)
    {
        var info = member.PolymorphicInfo!.Value;

        sb.WriteLineFormat("switch (obj.{0})", member.Name);
        using var _ = sb.BeginBlock();
        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.WriteLineFormat("case {0} typedInstance:", typeName);
            using var __ = sb.BeginBlock();
            if (string.IsNullOrEmpty(info.TypeIdProperty))
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

        sb.WriteLine("case null:");
        sb.WriteLineFormat("    throw new System.NullReferenceException($\"Property \\\"{0}\\\" cannot be null.\");", member.Name);
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
        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.WriteLineFormat("case {0} typedInstance:", typeName);
            using var __ = sb.BeginBlock();
            if (string.IsNullOrEmpty(info.TypeIdProperty))
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

        sb.WriteLine("case null:");
        sb.WriteLine("    throw new System.NullReferenceException($\"Item in collection cannot be null.\");");
        sb.WriteLine("default:");
        sb.WriteLine
        (
            $"    throw new System.IO.InvalidDataException($\"Unknown type for {instanceName}: {{{instanceName}?.GetType().FullName}}\");"
        );
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
        var refOrEmpty = target == "data" ? "ref " : "";
        if (elementInfo.HasGenerateSerializerAttribute)
        {
            if (target == "data")
            {
                sb.WriteLineFormat
                (
                    "var bytesWritten = {0}.Serialize({1}, data);",
                    TypeHelper.GetSimpleTypeName(elementInfo.TypeName), elementAccess
                );
                sb.WriteLine("data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.WriteLineFormat("{0}.Serialize({1}, stream);", TypeHelper.GetSimpleTypeName(elementInfo.TypeName), elementAccess);
            }
        }
        else if (elementInfo.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.TypeName);
            sb.WriteLineFormat("{0}.Write{1}({2}{3}, ({1}){4});", helper, typeName, refOrEmpty, target, elementAccess);
        }
        else if (elementInfo.IsStringType)
        {
            sb.WriteLineFormat("{0}.WriteString({1}{2}, {3});", helper, refOrEmpty, target, elementAccess);
        }
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
        var refOrEmpty = target == "data" ? "ref " : "";
        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.WriteLineFormat("{0}.Write{1}({2}{3}, ({1}){4});", helper, typeName, refOrEmpty, target, elementAccess);
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.WriteLineFormat("{0}.WriteString({1}{2}, {3});", helper, refOrEmpty, target, elementAccess);
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            if (target == "data")
            {
                sb.WriteLineFormat
                (
                    "var bytesWritten = {0}.Serialize({1}, data);",
                    TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName), elementAccess
                );
                sb.WriteLine("data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.WriteLineFormat("{0}.Serialize({1}, stream);", TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName), elementAccess);
            }
        }
    }
}