using Serializer.Generator.Models;
using System.Text;
using Serializer.Generator.Helpers;

namespace Serializer.Generator.CodeGenerators;

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
            if (member.IsList)
            {
                GenerateCollectionSizeCalculation(sb, member);
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"        size += {GetSimpleTypeName(member.TypeName)}.GetPacketSize(obj.{member.Name}); // Size for nested type {member.Name}");
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
        sb.AppendLine($"        size += sizeof(int); // Default count size for {member.Name}");

        if (member.CollectionInfo is not null && member.CollectionInfo.Value.PolymorphicMode != PolymorphicMode.None)
        {
            sb.AppendLine($"        throw new System.NotImplementedException(\"Polymorphic collection size calculation is not implemented for member {member.Name}.\");");
            return;
        }

        if (member.ListTypeArgument is not null)
        {
            var typeArg = member.ListTypeArgument.Value;
            if (typeArg.IsUnmanagedType)
            {
                sb.AppendLine($"        size += obj.{member.Name}.Count * sizeof({typeArg.TypeName});");
            }
            else if (typeArg.IsStringType)
            {
                sb.AppendLine($"        foreach(var item in obj.{member.Name}) {{ size += StringEx.MeasureSize(item); }}");
            }
            else if (typeArg.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"        foreach(var item in obj.{member.Name})");
                sb.AppendLine("        {");
                sb.AppendLine($"            size += {GetSimpleTypeName(typeArg.TypeName)}.GetPacketSize(item);");
                sb.AppendLine("        }");
            }
        }
    }

    private static string GetSimpleTypeName(string? fullyQualifiedName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedName)) return string.Empty;
        var lastDot = fullyQualifiedName.LastIndexOf('.');
        if (lastDot == -1)
        {
            return fullyQualifiedName;
        }
        return fullyQualifiedName.Substring(lastDot + 1);
    }
}
