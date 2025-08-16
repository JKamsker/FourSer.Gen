using System.Text;
using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
/// Generates GetPacketSize method implementations
/// </summary>
public static class PacketSizeGenerator
{
    public static void GenerateGetPacketSize(StringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.AppendLine($"    public static int GetPacketSize({typeToGenerate.Name} obj)");
        sb.AppendLine("    {");
        sb.AppendLine("        var size = 0;");

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
                    sb.AppendLine($"        size += {PolymorphicUtilities.GenerateTypeIdSizeExpression(info)};");
                }
                GeneratePolymorphicSizeCalculation(sb, member);
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"        size += {TypeHelper.GetSimpleTypeName(member.TypeName)}.GetPacketSize(obj.{member.Name}); // Size for nested type {member.Name}");
            }
            else if (member.IsStringType)
            {
                sb.AppendLine($"        size += StringEx.MeasureSize(obj.{member.Name}); // Size for string {member.Name}");
            }
            else if (member.IsUnmanagedType)
            {
                sb.AppendLine($"        size += sizeof({member.TypeName}); // Size for unmanaged type {member.Name}");
            }
        }

        sb.AppendLine("        return size;");
        sb.AppendLine("    }");
    }

    private static void GenerateCollectionSizeCalculation(StringBuilder sb, MemberToGenerate member)
    {
        if (member.CollectionInfo is null) return;

        // Determine the count type to use
        var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
        var countSizeExpression = TypeHelper.GetSizeOfExpression(countType);

        sb.AppendLine($"        size += {countSizeExpression}; // Count size for {member.Name}");

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            sb.AppendLine($"        foreach(var item in obj.{member.Name})");
            sb.AppendLine("        {");
            var info = member.PolymorphicInfo.Value;
            if (string.IsNullOrEmpty(info.TypeIdProperty))
            {
                sb.AppendLine($"            size += {PolymorphicUtilities.GenerateTypeIdSizeExpression(info)};");
            }
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
                default
            );
            GeneratePolymorphicSizeCalculation(sb, itemMember, "item");
            sb.AppendLine("        }");
            return;
        }

        // Handle both List<T> and other collection types
        if (member.ListTypeArgument is not null)
        {
            var typeArg = member.ListTypeArgument.Value;
            if (typeArg.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"        foreach(var item in obj.{member.Name})");
                sb.AppendLine("        {");
                sb.AppendLine($"            size += {TypeHelper.GetSimpleTypeName(typeArg.TypeName)}.GetPacketSize(item);");
                sb.AppendLine("        }");
            }
            else if (typeArg.IsUnmanagedType)
            {
                var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
                sb.AppendLine($"        size += {countExpression} * sizeof({typeArg.TypeName});");
            }
            else if (typeArg.IsStringType)
            {
                sb.AppendLine($"        foreach(var item in obj.{member.Name}) {{ size += StringEx.MeasureSize(item); }}");
            }
        }
        else if (member.CollectionTypeInfo is not null)
        {
            var collectionInfo = member.CollectionTypeInfo.Value;
            if (collectionInfo.IsElementUnmanagedType)
            {
                var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
                sb.AppendLine($"        size += {countExpression} * sizeof({collectionInfo.ElementTypeName});");
            }
            else if (collectionInfo.IsElementStringType)
            {
                sb.AppendLine($"        foreach(var item in obj.{member.Name}) {{ size += StringEx.MeasureSize(item); }}");
            }
            else if (collectionInfo.HasElementGenerateSerializerAttribute)
            {
                sb.AppendLine($"        foreach(var item in obj.{member.Name})");
                sb.AppendLine("        {");
                sb.AppendLine($"            size += {TypeHelper.GetSimpleTypeName(collectionInfo.ElementTypeName)}.GetPacketSize(item);");
                sb.AppendLine("        }");
            }
        }
    }

    private static void GeneratePolymorphicSizeCalculation(StringBuilder sb, MemberToGenerate member, string instanceName = "")
    {
        if (string.IsNullOrEmpty(instanceName))
        {
            instanceName = $"obj.{member.Name}";
        }

        var info = member.PolymorphicInfo!.Value;

        sb.AppendLine($"        switch ({instanceName})");
        sb.AppendLine("        {");

        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.AppendLine($"            case {typeName} typedInstance:");
            sb.AppendLine($"                size += {typeName}.GetPacketSize(typedInstance);");
            sb.AppendLine("                break;");
        }

        sb.AppendLine("            case null: break;");
        sb.AppendLine("            default:");
        sb.AppendLine($"                throw new System.IO.InvalidDataException($\"Unknown type for {member.Name}: {{{instanceName}?.GetType().FullName}}\");");
        sb.AppendLine("        }");
    }
}
