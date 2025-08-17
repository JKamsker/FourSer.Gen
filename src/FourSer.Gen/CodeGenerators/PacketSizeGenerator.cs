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
        sb.WriteLine($"public static int GetPacketSize({typeToGenerate.Name} obj)");
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
                    sb.WriteLine($"size += {PolymorphicUtilities.GenerateTypeIdSizeExpression(info)};");
                }

                GeneratePolymorphicSizeCalculation(sb, member);
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.WriteLine
                (
                    $"size += {TypeHelper.GetSimpleTypeName(member.TypeName)}.GetPacketSize(obj.{member.Name}); // Size for nested type {member.Name}"
                );
            }
            else if (member.IsStringType)
            {
                sb.WriteLine($"size += StringEx.MeasureSize(obj.{member.Name}); // Size for string {member.Name}");
            }
            else if (member.IsUnmanagedType)
            {
                sb.WriteLine($"size += sizeof({member.TypeName}); // Size for unmanaged type {member.Name}");
            }
        }

        sb.WriteLine("return size;");
    }

    private static void GenerateCollectionSizeCalculation(IndentedStringBuilder sb, MemberToGenerate member)
    {
        if (member.CollectionInfo is null)
        {
            return;
        }

        // Determine the count type to use
        var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
        var countSizeExpression = TypeHelper.GetSizeOfExpression(countType);

        sb.WriteLine($"size += {countSizeExpression}; // Count size for {member.Name}");

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            if (member.PolymorphicInfo is not { } info)
            {
                return;
            }

            sb.WriteLine($"foreach(var item in obj.{member.Name})");
            using var _ = sb.BeginBlock();
            if (string.IsNullOrEmpty(info.TypeIdProperty))
            {
                sb.WriteLine($"size += {PolymorphicUtilities.GenerateTypeIdSizeExpression(info)};");
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
            return;
        }

        // Handle both List<T> and other collection types
        if (member.ListTypeArgument is not null)
        {
            var typeArg = member.ListTypeArgument.Value;
            if (typeArg.HasGenerateSerializerAttribute)
            {
                sb.WriteLine($"foreach(var item in obj.{member.Name})");
                using var _ = sb.BeginBlock();
                sb.WriteLine($"size += {TypeHelper.GetSimpleTypeName(typeArg.TypeName)}.GetPacketSize(item);");
            }
            else if (typeArg.IsUnmanagedType)
            {
                var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
                sb.WriteLine($"size += {countExpression} * sizeof({typeArg.TypeName});");
            }
            else if (typeArg.IsStringType)
            {
                sb.WriteLine($"foreach(var item in obj.{member.Name}) {{ size += StringEx.MeasureSize(item); }}");
            }
        }
        else if (member.CollectionTypeInfo is not null)
        {
            var collectionInfo = member.CollectionTypeInfo.Value;
            if (collectionInfo.IsElementUnmanagedType)
            {
                var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
                sb.WriteLine($"size += {countExpression} * sizeof({collectionInfo.ElementTypeName});");
            }
            else if (collectionInfo.IsElementStringType)
            {
                sb.WriteLine($"foreach(var item in obj.{member.Name}) {{ size += StringEx.MeasureSize(item); }}");
            }
            else if (collectionInfo.HasElementGenerateSerializerAttribute)
            {
                sb.WriteLine($"foreach(var item in obj.{member.Name})");
                using var _ = sb.BeginBlock();
                sb.WriteLine($"size += {TypeHelper.GetSimpleTypeName(collectionInfo.ElementTypeName)}.GetPacketSize(item);");
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

        sb.WriteLine($"switch ({instanceName})");
        using var _ = sb.BeginBlock();
        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.WriteLine($"case {typeName} typedInstance:");
            sb.WriteLine($"    size += {typeName}.GetPacketSize(typedInstance);");
            sb.WriteLine("    break;");
        }

        sb.WriteLine("case null: break;");
        sb.WriteLine("default:");
        sb.WriteLine
        (
            $"    throw new System.IO.InvalidDataException($\"Unknown type for {member.Name}: {{{instanceName}?.GetType().FullName}}\");"
        );
    }
}