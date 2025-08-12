using Serializer.Generator.Models;
using System.Text;
using Serializer.Generator.Helpers;

namespace Serializer.Generator.CodeGenerators;

/// <summary>
/// Generates Deserialize method implementations
/// </summary>
public static class DeserializationGenerator
{
    public static void GenerateDeserialize(StringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.AppendLine($"    public static {typeToGenerate.Name} Deserialize(System.ReadOnlySpan<byte> data, out int bytesRead)");
        sb.AppendLine("    {");
        sb.AppendLine($"        bytesRead = 0;");
        sb.AppendLine($"        var originalData = data;");
        sb.AppendLine($"        var obj = new {typeToGenerate.Name}();");

        foreach (var member in typeToGenerate.Members)
        {
            GenerateMemberDeserialization(sb, member);
        }

        sb.AppendLine("        bytesRead = originalData.Length - data.Length;");
        sb.AppendLine("        return obj;");
        sb.AppendLine("    }");
    }

    private static void GenerateMemberDeserialization(StringBuilder sb, MemberToGenerate member)
    {
        if (member.IsList)
        {
            sb.AppendLine($"        // Collection deserialization for {member.Name} is not implemented.");
            sb.AppendLine($"        throw new System.NotImplementedException(\"Collection deserialization is not implemented for member {member.Name}.\");");
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"        obj.{member.Name} = {GetSimpleTypeName(member.TypeName)}.Deserialize(data, out var nestedBytesRead);");
            sb.AppendLine($"        data = data.Slice(nestedBytesRead);");
        }
        else if (member.IsStringType)
        {
            // Assuming ReadString is an extension method on ReadOnlySpan<byte> provided by a helper
            sb.AppendLine($"        obj.{member.Name} = data.ReadString();");
        }
        else if (member.IsUnmanagedType)
        {
            // Assuming ReadT is an extension method for each unmanaged type
            var readMethod = $"Read{GetSimpleTypeName(member.TypeName)}";
            sb.AppendLine($"        obj.{member.Name} = data.{readMethod}();");
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
