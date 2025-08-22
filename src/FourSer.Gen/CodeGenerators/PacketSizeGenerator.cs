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
            else if (member.PolymorphicInfo is { } info)
            {
                if (info.TypeIdPropertyIndex is null)
                {
                    sb.WriteLineFormat("size += {0};", info.TypeIdSizeInBytes);
                }

                GeneratePolymorphicSizeCalculation(sb, member);
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.WriteLineFormat
                (
                    "size += {0}.GetPacketSize(obj.{1}); // Size for nested type {1}",
                    TypeHelper.GetSimpleTypeName(member.TypeName),
                    member.Name
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

        if (collectionInfo.CountSizeReferenceIndex is null)
        {
            sb.WriteLineFormat("size += {0}; // Count size for {1}", collectionInfo.CountTypeSizeInBytes, member.Name);
        }

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            AddPolymorphicSerialization(sb, member, collectionInfo);
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
                sb.WriteLineFormat
                    ("size += {0}.GetPacketSize(item);", TypeHelper.GetSimpleTypeName(collectionTypeValue.ElementTypeName));
            }
        }
    }

    private static void AddPolymorphicSerialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        CollectionInfo collectionInfo
    )
    {
        if (member.PolymorphicInfo is not { } info)
        {
            return;
        }

        if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
        {
            if (info.TypeIdPropertyIndex is null)
            {
                sb.WriteLineFormat
                    ("size += {0}; // Size for polymorphic type id", info.TypeIdSizeInBytes);
            }
        }

        sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
        using var _ = sb.BeginBlock();

        sb.WriteLineFormat("foreach (var item in obj.{0})", member.Name);
        using var __ = sb.BeginBlock();

        if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
        {
            sb.WriteLineFormat
                ("size += {0}; // Size for polymorphic type id", info.TypeIdSizeInBytes);
        }

        sb.WriteLine("size += item switch");
        sb.WriteLine("{");
        sb.Indent();
        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            var varName = typeName.ToCamelCase();
            sb.WriteLineFormat("{0} {1} => {2}.GetPacketSize({1}),", typeName, varName, typeName);
        }

        sb.WriteLineFormat
        (
            "_ => throw new System.IO.InvalidDataException($\"Unknown item type in collection {0}: {{item.GetType().Name}}\")",
            member.Name
        );
        sb.Unindent();
        sb.WriteLine("};");
    }

    private static void GeneratePolymorphicSizeCalculation
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string instanceName = ""
    )
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
            member.Name,
            instanceName
        );
    }
}
