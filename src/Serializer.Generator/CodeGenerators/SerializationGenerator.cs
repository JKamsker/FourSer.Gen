using Serializer.Generator.Models;
using System.Text;
using Serializer.Generator.Helpers;

namespace Serializer.Generator.CodeGenerators;

/// <summary>
/// Generates Serialize method implementations
/// </summary>
public static class SerializationGenerator
{
    public static void GenerateSerialize(StringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.AppendLine($"    public static int Serialize({typeToGenerate.Name} obj, System.Span<byte> data)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var originalData = data;");

        foreach (var member in typeToGenerate.Members)
        {
            GenerateMemberSerialization(sb, member);
        }

        sb.AppendLine("        return originalData.Length - data.Length;");
        sb.AppendLine("    }");
    }

    private static void GenerateMemberSerialization(StringBuilder sb, MemberToGenerate member)
    {
        if (member.IsList)
        {
            sb.AppendLine($"        // Collection serialization for {member.Name} is not implemented.");
            sb.AppendLine($"        throw new System.NotImplementedException(\"Collection serialization is not implemented for member {member.Name}.\");");
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"        var bytesWritten = {GetSimpleTypeName(member.TypeName)}.Serialize(obj.{member.Name}, data);");
            sb.AppendLine($"        data = data.Slice(bytesWritten);");
        }
        else if (member.IsStringType)
        {
            // Assuming WriteString is an extension method on Span<byte> provided by a helper
            sb.AppendLine($"        data.WriteString(obj.{member.Name});");
        }
        else if (member.IsUnmanagedType)
        {
            // Assuming WriteT is an extension method for each unmanaged type
            var writeMethod = $"Write{GetSimpleTypeName(member.TypeName)}";
            sb.AppendLine($"        data.{writeMethod}(obj.{member.Name});");
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
