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
                sb.AppendLine($"        // Collection size calculation for {member.Name} is not fully implemented in this version.");
                sb.AppendLine($"        throw new System.NotImplementedException(\"Collection size calculation is not implemented for member {member.Name}.\");");
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"        size += {GetSimpleTypeName(member.TypeName)}.GetPacketSize(obj.{member.Name});");
            }
            else if (member.IsStringType)
            {
                sb.AppendLine($"        size += StringEx.MeasureSize(obj.{member.Name});");
            }
            else if (member.IsUnmanagedType)
            {
                sb.AppendLine($"        size += sizeof({member.TypeName});");
            }
        }

        sb.AppendLine("        return size;");
        sb.AppendLine("    }");
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
