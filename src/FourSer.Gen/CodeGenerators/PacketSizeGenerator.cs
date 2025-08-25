using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
///     Generates GetPacketSize method implementations
/// </summary>
public static class PacketSizeGenerator
{
    public static void GenerateGetSize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLineFormat("public static int GetPacketSize({0} obj)", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        // null check
        if (!typeToGenerate.IsValueType)
        {
            sb.WriteLine("if (obj is null) return 0;");
        }
        
        
        sb.WriteLine("var size = 0;");

        foreach (var member in typeToGenerate.Members)
        {
            GenerateMemberSizeCalculation(sb, member, typeToGenerate);
        }

        sb.WriteLine("return size;");
    }

    private static void GenerateMemberSizeCalculation(IndentedStringBuilder sb, MemberToGenerate member, TypeToGenerate type)
    {
        var resolvedSerializer = GeneratorUtilities.ResolveSerializer(member, type);
        if (resolvedSerializer is { } serializer)
        {
            sb.WriteLineFormat("size += FourSer.Generated.Internal.__FourSer_Generated_Serializers.{0}.GetPacketSize(obj.{1});", serializer.FieldName, member.Name);
            return;
        }

        if (member.IsList || member.IsCollection)
        {
            GenerateCollectionSizeCalculation(sb, member);
        }
        else if (member.PolymorphicInfo is { } info)
        {
            if (info.TypeIdPropertyIndex is null)
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

    private readonly record struct ElementInfo(
        string TypeName,
        bool IsUnmanaged,
        bool IsString,
        bool HasSerializer);

    private static ElementInfo? GetElementInfo(MemberToGenerate member)
    {
        if (member.ListTypeArgument is { } listArg)
        {
            return new ElementInfo
            (
                listArg.TypeName,
                listArg.IsUnmanagedType,
                listArg.IsStringType,
                listArg.HasGenerateSerializerAttribute
            );
        }

        if (member.CollectionTypeInfo is { } collInfo)
        {
            return new ElementInfo
            (
                collInfo.ElementTypeName,
                collInfo.IsElementUnmanagedType,
                collInfo.IsElementStringType,
                collInfo.HasElementGenerateSerializerAttribute
            );
        }

        return null;
    }

    private static void GenerateStandardCollectionSizeCalculation(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        ElementInfo info)
    {
        if (info.HasSerializer)
        {
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using var _ = sb.BeginBlock();
            sb.WriteLineFormat("foreach(var item in obj.{0})", member.Name);
            using var __ = sb.BeginBlock();
            sb.WriteLineFormat("size += {0}.GetPacketSize(item);", TypeHelper.GetSimpleTypeName(info.TypeName));
        }
        else if (info.IsUnmanaged)
        {
            var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name, true);
            sb.WriteLineFormat("size += {0} * sizeof({1});", countExpression, info.TypeName);
        }
        else if (info.IsString)
        {
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using var _ = sb.BeginBlock();
            sb.WriteLineFormat("foreach(var item in obj.{0}) {{ size += StringEx.MeasureSize(item); }}", member.Name);
        }
    }

    private static void GenerateCollectionSizeCalculation(IndentedStringBuilder sb, MemberToGenerate member)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        if ((collectionInfo.CountSize is null or < 0) && collectionInfo.CountSizeReferenceIndex is null)
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countSizeExpression = TypeHelper.GetSizeOfExpression(countType);
            sb.WriteLineFormat("size += {0}; // Count size for {1}", countSizeExpression, member.Name);
        }

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            AddPolymorphicSerialization(sb, member, collectionInfo);
            return;
        }

        if (GetElementInfo(member) is { } info)
        {
            GenerateStandardCollectionSizeCalculation(sb, member, info);
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
                    ("size += {0}; // Size for polymorphic type id", PolymorphicUtilities.GenerateTypeIdSizeExpression(info));
            }
        }

        sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
        using var _ = sb.BeginBlock();

        sb.WriteLineFormat("foreach (var item in obj.{0})", member.Name);
        using var __ = sb.BeginBlock();

        if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
        {
            sb.WriteLineFormat
                ("size += {0}; // Size for polymorphic type id", PolymorphicUtilities.GenerateTypeIdSizeExpression(info));
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
