using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
/// Generates Serialize method implementations
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
        sb.WriteLine($"public static int Serialize({typeToGenerate.Name} obj, System.Span<byte> data)");
        using (sb.BeginBlock())
        {
            sb.WriteLine($"var originalData = data;");

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

                sb.WriteLine($"switch (obj.{member.Name})");
                using (sb.BeginBlock())
                {
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
                        sb.WriteLine($"case {typeName}:");
                        sb.WriteLine($"    obj.{info.TypeIdProperty} = {key};");
                        sb.WriteLine("    break;");
                    }
                    sb.WriteLine("case null:");
                    sb.WriteLine("    break;");
                }
            }

            foreach (var member in typeToGenerate.Members)
            {
                GenerateMemberSerialization(sb, member, "data", "SpanWriterHelpers");
            }

            sb.WriteLine("return originalData.Length - data.Length;");
        }
    }

    private static void GenerateStreamSerialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLine($"public static void Serialize({typeToGenerate.Name} obj, System.IO.Stream stream)");
        using (sb.BeginBlock())
        {
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

                sb.WriteLine($"switch (obj.{member.Name})");
                using (sb.BeginBlock())
                {
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
                        sb.WriteLine($"case {typeName}:");
                        sb.WriteLine($"    obj.{info.TypeIdProperty} = {key};");
                        sb.WriteLine("    break;");
                    }
                    sb.WriteLine("case null:");
                    sb.WriteLine("    break;");
                }
            }

            foreach (var member in typeToGenerate.Members)
            {
                GenerateMemberSerialization(sb, member, "stream", "StreamWriterHelpers");
            }
        }
    }

    private static void GenerateMemberSerialization(IndentedStringBuilder sb, MemberToGenerate member, string target, string helper)
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
            sb.WriteLine($"if (obj.{member.Name} is null)");
            using (sb.BeginBlock())
            {
                sb.WriteLine($"throw new System.NullReferenceException($\"Property \\\"{member.Name}\\\" cannot be null.\");");
            }
            if (target == "data")
            {
                sb.WriteLine($"var bytesWritten = {TypeHelper.GetSimpleTypeName(member.TypeName)}.Serialize(obj.{member.Name}, data);");
                sb.WriteLine($"data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.WriteLine($"{TypeHelper.GetSimpleTypeName(member.TypeName)}.Serialize(obj.{member.Name}, stream);");
            }
        }
        else if (member.IsStringType)
        {
            sb.WriteLine($"FourSer.Gen.Helpers.{helper}.WriteString({refOrEmpty}{target}, obj.{member.Name});");
        }
        else if (member.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
            var writeMethod = $"Write{typeName}";
            sb.WriteLine($"FourSer.Gen.Helpers.{helper}.{writeMethod}({refOrEmpty}{target}, ({typeName})obj.{member.Name});");
        }
    }

    private static void GenerateCollectionSerialization(IndentedStringBuilder sb, MemberToGenerate member, string target, string helper)
    {
        if (member.CollectionInfo is not { } collectionInfo) return;
        var refOrEmpty = target == "data" ? "ref " : "";

        sb.WriteLine($"if (obj.{member.Name} is null)");
        using (sb.BeginBlock())
        {
            if (collectionInfo.CountType != null || !string.IsNullOrEmpty(collectionInfo.CountSizeReference))
            {
                var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
                var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
                sb.WriteLine($"FourSer.Gen.Helpers.{helper}.{countWriteMethod}({refOrEmpty}{target}, ({countType})0);");
            }
            else
            {
                sb.WriteLine($"throw new System.NullReferenceException($\"Collection \\\"{member.Name}\\\" cannot be null.\");");
            }
        }
        sb.WriteLine("else");
        using (sb.BeginBlock())
        {
            if (collectionInfo is { Unlimited: false })
            {
                var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
                var countWriteMethod = TypeHelper.GetWriteMethodName(countType);

                var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
                sb.WriteLine($"FourSer.Gen.Helpers.{helper}.{countWriteMethod}({refOrEmpty}{target}, ({countType}){countExpression});");
            }

            if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
            {
                if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
                {
                    sb.WriteLine($"foreach(var item in obj.{member.Name})");
                    using (sb.BeginBlock())
                    {
                        var itemMember = new MemberToGenerate(
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

                        GeneratePolymorphicItemSerialization(sb, itemMember, "item", target, helper);
                    }
                    return;
                }

                if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
                {
                    var info = member.PolymorphicInfo!.Value;
                    var typeIdProperty = collectionInfo.TypeIdProperty;

                    sb.WriteLine($"switch (obj.{typeIdProperty})");
                    using (sb.BeginBlock())
                    {
                        foreach (var option in info.Options)
                        {
                            var key = option.Key.ToString();
                            if (info.EnumUnderlyingType is not null)
                            {
                                key = $"({info.TypeIdType}){key}";
                            }
                            else if (info.TypeIdType.EndsWith("Enum"))
                            {
                                key = $"{info.TypeIdType}.{key}";
                            }

                            sb.WriteLine($"case {key}:");
                            using (sb.BeginBlock())
                            {
                                sb.WriteLine($"foreach(var item in obj.{member.Name})");
                                using (sb.BeginBlock())
                                {
                                    if (target == "data")
                                    {
                                        sb.WriteLine($"var bytesWritten = {TypeHelper.GetSimpleTypeName(option.Type)}.Serialize(({TypeHelper.GetSimpleTypeName(option.Type)})item, data);");
                                        sb.WriteLine($"data = data.Slice(bytesWritten);");
                                    }
                                    else
                                    {
                                        sb.WriteLine($"{TypeHelper.GetSimpleTypeName(option.Type)}.Serialize(({TypeHelper.GetSimpleTypeName(option.Type)})item, stream);");
                                    }
                                }
                                sb.WriteLine("break;");
                            }
                        }

                        sb.WriteLine("default:");
                        sb.WriteLine($"    throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{obj.{typeIdProperty}}}\");");
                    }
                    return;
                }
            }

            var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
            var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

            if (isByteCollection)
            {
                sb.WriteLine($"FourSer.Gen.Helpers.{helper}.WriteBytes({refOrEmpty}{target}, obj.{member.Name});");
            }
            else
            {
                if (member.CollectionTypeInfo?.IsArray == true || member.IsList)
                {
                    var loopCountExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
                    sb.WriteLine($"for (int i = 0; i < {loopCountExpression}; i++)");
                    using (sb.BeginBlock())
                    {
                        if (member.ListTypeArgument is not null)
                        {
                            GenerateListElementSerialization(sb, member.ListTypeArgument.Value, $"obj.{member.Name}[i]", target, helper);
                        }
                        else if (member.CollectionTypeInfo is not null)
                        {
                            GenerateCollectionElementSerialization(sb, member.CollectionTypeInfo.Value, $"obj.{member.Name}[i]", target, helper);
                        }
                    }
                }
                else
                {
                    sb.WriteLine($"foreach (var item in obj.{member.Name})");
                    using (sb.BeginBlock())
                    {
                        if (member.CollectionTypeInfo is not null)
                        {
                            GenerateCollectionElementSerialization(sb, member.CollectionTypeInfo.Value, "item", target, helper);
                        }
                    }
                }
            }
        }
    }

    private static void GeneratePolymorphicSerialization(IndentedStringBuilder sb, MemberToGenerate member, string target, string helper)
    {
        var info = member.PolymorphicInfo!.Value;

        sb.WriteLine($"switch (obj.{member.Name})");
        using (sb.BeginBlock())
        {
            foreach (var option in info.Options)
            {
                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                sb.WriteLine($"case {typeName} typedInstance:");
                using (sb.BeginBlock())
                {
                    if (string.IsNullOrEmpty(info.TypeIdProperty))
                    {
                        sb.WriteLine(PolymorphicUtilities.GenerateWriteTypeIdCode(option, info, "    ", target, helper));
                    }

                    if (target == "data")
                    {
                        sb.WriteLine($"var bytesWritten = {typeName}.Serialize(typedInstance, data);");
                        sb.WriteLine($"data = data.Slice(bytesWritten);");
                    }
                    else
                    {
                        sb.WriteLine($"{typeName}.Serialize(typedInstance, stream);");
                    }
                    sb.WriteLine("break;");
                }
            }

            sb.WriteLine("case null:");
            sb.WriteLine($"    throw new System.NullReferenceException($\"Property \\\"{member.Name}\\\" cannot be null.\");");
            sb.WriteLine("default:");
            sb.WriteLine($"    throw new System.IO.InvalidDataException($\"Unknown type for {member.Name}: {{obj.{member.Name}?.GetType().FullName}}\");");
        }
    }

    private static void GeneratePolymorphicItemSerialization(IndentedStringBuilder sb, MemberToGenerate member, string instanceName, string target, string helper)
    {
        var info = member.PolymorphicInfo!.Value;

        sb.WriteLine($"switch ({instanceName})");
        using (sb.BeginBlock())
        {
            foreach (var option in info.Options)
            {
                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                sb.WriteLine($"case {typeName} typedInstance:");
                using (sb.BeginBlock())
                {
                    if (string.IsNullOrEmpty(info.TypeIdProperty))
                    {
                        sb.WriteLine(PolymorphicUtilities.GenerateWriteTypeIdCode(option, info, "        ", target, helper));
                    }

                    if (target == "data")
                    {
                        sb.WriteLine($"var bytesWritten = {typeName}.Serialize(typedInstance, data);");
                        sb.WriteLine($"data = data.Slice(bytesWritten);");
                    }
                    else
                    {
                        sb.WriteLine($"{typeName}.Serialize(typedInstance, stream);");
                    }
                    sb.WriteLine("break;");
                }
            }

            sb.WriteLine("case null:");
            sb.WriteLine($"    throw new System.NullReferenceException($\"Item in collection cannot be null.\");");
            sb.WriteLine("default:");
            sb.WriteLine($"    throw new System.IO.InvalidDataException($\"Unknown type for {instanceName}: {{{instanceName}?.GetType().FullName}}\");");
        }
    }

    private static void GenerateListElementSerialization(IndentedStringBuilder sb, ListTypeArgumentInfo elementInfo, string elementAccess, string target, string helper)
    {
        var refOrEmpty = target == "data" ? "ref " : "";
        if (elementInfo.HasGenerateSerializerAttribute)
        {
            if (target == "data")
            {
                sb.WriteLine($"var bytesWritten = {TypeHelper.GetSimpleTypeName(elementInfo.TypeName)}.Serialize({elementAccess}, data);");
                sb.WriteLine($"data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.WriteLine($"{TypeHelper.GetSimpleTypeName(elementInfo.TypeName)}.Serialize({elementAccess}, stream);");
            }
        }
        else if (elementInfo.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.TypeName);
            sb.WriteLine($"FourSer.Gen.Helpers.{helper}.Write{typeName}({refOrEmpty}{target}, ({typeName}){elementAccess});");
        }
        else if (elementInfo.IsStringType)
        {
            sb.WriteLine($"FourSer.Gen.Helpers.{helper}.WriteString({refOrEmpty}{target}, {elementAccess});");
        }
    }

    private static void GenerateCollectionElementSerialization(IndentedStringBuilder sb, CollectionTypeInfo elementInfo, string elementAccess, string target, string helper)
    {
        var refOrEmpty = target == "data" ? "ref " : "";
        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.WriteLine($"FourSer.Gen.Helpers.{helper}.Write{typeName}({refOrEmpty}{target}, ({typeName}){elementAccess});");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.WriteLine($"FourSer.Gen.Helpers.{helper}.WriteString({refOrEmpty}{target}, {elementAccess});");
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            if (target == "data")
            {
                sb.WriteLine($"var bytesWritten = {TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Serialize({elementAccess}, data);");
                sb.WriteLine($"data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.WriteLine($"{TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Serialize({elementAccess}, stream);");
            }
        }
    }
}
