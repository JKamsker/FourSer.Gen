using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
///     Generates GetPacketSize method implementations
/// </summary>
public static class PacketSizeGenerator
{
    public static void GenerateGetPacketSize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLineFormat("public static int GetPacketSize({0} obj)", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        sb.WriteLine("var size = 0;");

        foreach (var member in typeToGenerate.Members)
        {
            if (member.IsList || member.IsCollection)
            {
                GenerateCollectionSizeCalculation(sb, member);
            }
            else if (member.PolymorphicInfo is not null)
            {
                var info = member.PolymorphicInfo.Value;
                if (string.IsNullOrEmpty(info.TypeIdProperty))
                {
                    sb.WriteLineFormat("size += {0};", PolymorphicUtilities.GenerateTypeIdSizeExpression(info));
                }

                GeneratePolymorphicSizeCalculation(sb, member);
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.WriteLineFormat
                (
                    "size += {0}.GetPacketSize(obj.{1}); // Size for nested type {1}",
                    TypeHelper.GetSimpleTypeName(member.TypeName), member.Name
                );
            }
            else if (member.IsStringType)
            {
                sb.WriteLineFormat("size += StringEx.MeasureSize(obj.{0}); // Size for string {0}", member.Name);
            }
            else if (member.IsUnmanagedType)
            {
                sb.WriteLineFormat("size += sizeof({0}); // Size for unmanaged type {1}", member.TypeName, member.Name);
            }
        }

        sb.WriteLine("return size;");
    }

    private static void GenerateCollectionSizeCalculation(IndentedStringBuilder sb, MemberToGenerate member)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        if (string.IsNullOrEmpty(collectionInfo.CountSizeReference))
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countSizeExpression = TypeHelper.GetSizeOfExpression(countType);
            sb.WriteLineFormat("size += {0}; // Count size for {1}", countSizeExpression, member.Name);
        }

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            if (member.PolymorphicInfo is not { } info)
            {
                return;
            }

            if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                if (string.IsNullOrEmpty(info.TypeIdProperty))
                {
                    sb.WriteLineFormat("size += {0}; // Size for polymorphic type id", PolymorphicUtilities.GenerateTypeIdSizeExpression(info));
                }
            }

            if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId && string.IsNullOrEmpty(collectionInfo.TypeIdProperty))
            {
                var isListOrArray = member.IsList || (member.CollectionTypeInfo?.IsArray ?? false);
                if (!isListOrArray)
                {
                    sb.WriteLineFormat("var collectionItems = obj.{0};", member.Name);
                    sb.WriteLine("if (collectionItems is null)");
                    sb.WriteLine("{");
                    sb.WriteLine("    return size;");
                    sb.WriteLine("}");

                    sb.WriteLine("using var enumerator = collectionItems.GetEnumerator();");
                    sb.WriteLine("if (!enumerator.MoveNext())");
                    sb.WriteLine("{");
                    sb.WriteLine("    return size;");
                    sb.WriteLine("}");

                    sb.WriteLine("var firstItem = enumerator.Current;");
                    sb.WriteLine("switch (firstItem)");
                    using (sb.BeginBlock())
                    {
                        foreach (var option in info.Options)
                        {
                            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                            sb.WriteLine($"case {typeName}:");
                            using (sb.BeginBlock())
                            {
                                sb.WriteLine("do");
                                using (sb.BeginBlock())
                                {
                                    sb.WriteLine($"size += {typeName}.GetPacketSize(({typeName})enumerator.Current);");
                                }
                                sb.WriteLine("while (enumerator.MoveNext());");
                                sb.WriteLine("break;");
                            }
                        }
                        sb.WriteLineFormat("default: throw new System.IO.InvalidDataException($\"Unknown item type in collection {0}: {{firstItem.GetType().Name}}\");", member.Name);
                    }
                }
                else
                {
                    var listItemsVar = member.Name.ToCamelCase();
                    if (listItemsVar == "items")
                    {
                        listItemsVar = "collectionItems"; // Avoid conflict
                    }

                    sb.WriteLine($"var {listItemsVar} = obj.{member.Name};");
                    sb.WriteLine($"if ({listItemsVar} is not null && {listItemsVar}.Count > 0)");
                    using (sb.BeginBlock())
                    {
                        sb.WriteLine($"var firstItem = {listItemsVar}[0];");
                        sb.WriteLine("switch(firstItem)");
                        using (sb.BeginBlock())
                        {
                            foreach (var option in info.Options)
                            {
                                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                                sb.WriteLine($"case {typeName}:");
                                using (sb.BeginBlock())
                                {
                                    sb.WriteLine($"foreach(var item in {listItemsVar})");
                                    using (sb.BeginBlock())
                                    {
                                        sb.WriteLine($"size += {typeName}.GetPacketSize(({typeName})item);");
                                    }
                                    sb.WriteLine("break;");
                                }
                            }
                            sb.WriteLineFormat("default: throw new System.IO.InvalidDataException($\"Unknown item type in collection {0}: {{firstItem.GetType().Name}}\");", member.Name);
                        }
                    }
                }
                return;
            }

            // Fallback for IndividualTypeIds or explicit TypeIdProperty
            sb.WriteLineFormat("foreach(var item in obj.{0})", member.Name);
            using (sb.BeginBlock())
            {
                if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
                {
                    sb.WriteLineFormat("size += {0}; // Size for polymorphic type id", PolymorphicUtilities.GenerateTypeIdSizeExpression(info));
                }

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
                GeneratePolymorphicSizeCalculation(sb, itemMember, "item");
            }
            return;
        }

        // Handle both List<T> and other collection types
        if (member.ListTypeArgument is not null)
        {
            var typeArg = member.ListTypeArgument.Value;
            if (typeArg.HasGenerateSerializerAttribute)
            {
                sb.WriteLineFormat("foreach(var item in obj.{0})", member.Name);
                using var _ = sb.BeginBlock();
                sb.WriteLineFormat("size += {0}.GetPacketSize(item);", TypeHelper.GetSimpleTypeName(typeArg.TypeName));
            }
            else if (typeArg.IsUnmanagedType)
            {
                var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
                sb.WriteLineFormat("size += {0} * sizeof({1});", countExpression, typeArg.TypeName);
            }
            else if (typeArg.IsStringType)
            {
                sb.WriteLineFormat("foreach(var item in obj.{0}) {{ size += StringEx.MeasureSize(item); }}", member.Name);
            }
        }
        else if (member.CollectionTypeInfo is not null)
        {
            var collectionTypeValue = member.CollectionTypeInfo.Value;
            if (collectionTypeValue.IsElementUnmanagedType)
            {
                var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
                sb.WriteLineFormat("size += {0} * sizeof({1});", countExpression, collectionTypeValue.ElementTypeName);
            }
            else if (collectionTypeValue.IsElementStringType)
            {
                sb.WriteLineFormat("foreach(var item in obj.{0}) {{ size += StringEx.MeasureSize(item); }}", member.Name);
            }
            else if (collectionTypeValue.HasElementGenerateSerializerAttribute)
            {
                sb.WriteLineFormat("foreach(var item in obj.{0})", member.Name);
                using var _ = sb.BeginBlock();
                sb.WriteLineFormat("size += {0}.GetPacketSize(item);", TypeHelper.GetSimpleTypeName(collectionTypeValue.ElementTypeName));
            }
        }
    }

    private static void GeneratePolymorphicSizeCalculation
        (IndentedStringBuilder sb, MemberToGenerate member, string instanceName = "")
    {
        if (string.IsNullOrEmpty(instanceName))
        {
            instanceName = $"obj.{member.Name}";
        }

        if (member.PolymorphicInfo is not { } info)
        {
            return;
        }

        sb.WriteLineFormat("switch ({0})", instanceName);
        using var _ = sb.BeginBlock();
        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.WriteLineFormat("case {0} typedInstance:", typeName);
            sb.WriteLineFormat("    size += {0}.GetPacketSize(typedInstance);", typeName);
            sb.WriteLine("    break;");
        }

        sb.WriteLine("case null: break;");
        sb.WriteLine("default:");
        sb.WriteLineFormat
        (
            "    throw new System.IO.InvalidDataException($\"Unknown type for {0}: {{{1}?.GetType().FullName}}\");",
            member.Name, instanceName
        );
    }
}