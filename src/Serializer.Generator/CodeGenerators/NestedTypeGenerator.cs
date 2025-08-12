using Serializer.Generator.Models;
using System.Text;
using Serializer.Generator.Helpers;

namespace Serializer.Generator.CodeGenerators;

/// <summary>
/// Generates serialization code for nested types
/// </summary>
public static class NestedTypeGenerator
{
    public static void GenerateNestedTypes(StringBuilder sb, EquatableArray<TypeToGenerate> nestedTypes)
    {
        if (nestedTypes.IsEmpty)
        {
            return;
        }

        foreach (var nestedType in nestedTypes)
        {
            sb.AppendLine();
            var typeKeyword = nestedType.IsValueType ? "struct" : "class";
            sb.AppendLine($"    public partial {typeKeyword} {nestedType.Name} : ISerializable<{nestedType.Name}>");
            sb.AppendLine("    {");

            // We can reuse the main generators by wrapping the nested type in a new TypeToGenerate,
            // but for now, we'll use simplified, self-contained logic similar to the original.

            GenerateNestedGetPacketSize(sb, nestedType);
            sb.AppendLine();
            GenerateNestedDeserialize(sb, nestedType);
            sb.AppendLine();
            GenerateNestedSerialize(sb, nestedType);

            // Handle even deeper nested types
            if (!nestedType.NestedTypes.IsEmpty)
            {
                GenerateNestedTypes(sb, nestedType.NestedTypes);
            }

            sb.AppendLine("    }");
        }
    }

    private static void GenerateNestedGetPacketSize(StringBuilder sb, TypeToGenerate nestedType)
    {
        sb.AppendLine($"        public static int GetPacketSize({nestedType.Name} obj)");
        sb.AppendLine("        {");
        sb.AppendLine("            var size = 0;");
        foreach (var member in nestedType.Members)
        {
            // Simplified logic, similar to the main PacketSizeGenerator
            if (member.IsList)
            {
                sb.AppendLine($"            throw new System.NotImplementedException(\"Collection size calculation is not implemented for member {member.Name}.\");");
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"            size += {GetSimpleTypeName(member.TypeName)}.GetPacketSize(obj.{member.Name});");
            }
            else if (member.IsStringType)
            {
                sb.AppendLine($"            size += StringEx.MeasureSize(obj.{member.Name});");
            }
            else if (member.IsUnmanagedType)
            {
                sb.AppendLine($"            size += sizeof({member.TypeName});");
            }
        }
        sb.AppendLine("            return size;");
        sb.AppendLine("        }");
    }

    private static void GenerateNestedDeserialize(StringBuilder sb, TypeToGenerate nestedType)
    {
        sb.AppendLine($"        public static {nestedType.Name} Deserialize(System.ReadOnlySpan<byte> data, out int bytesRead)");
        sb.AppendLine("        {");
        sb.AppendLine("            bytesRead = 0;");
        sb.AppendLine("            var originalData = data;");
        sb.AppendLine($"            var obj = new {nestedType.Name}();");
        foreach (var member in nestedType.Members)
        {
            if (member.IsList)
            {
                sb.AppendLine($"            throw new System.NotImplementedException(\"Collection deserialization is not implemented for member {member.Name}.\");");
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"            obj.{member.Name} = {GetSimpleTypeName(member.TypeName)}.Deserialize(data, out var nestedBytesRead);");
                sb.AppendLine($"            data = data.Slice(nestedBytesRead);");
            }
            else if (member.IsStringType)
            {
                sb.AppendLine($"            obj.{member.Name} = data.ReadString();");
            }
            else if (member.IsUnmanagedType)
            {
                var readMethod = $"Read{GetSimpleTypeName(member.TypeName)}";
                sb.AppendLine($"            obj.{member.Name} = data.{readMethod}();");
            }
        }
        sb.AppendLine("            bytesRead = originalData.Length - data.Length;");
        sb.AppendLine("            return obj;");
        sb.AppendLine("        }");
    }

    private static void GenerateNestedSerialize(StringBuilder sb, TypeToGenerate nestedType)
    {
        sb.AppendLine($"        public static int Serialize({nestedType.Name} obj, System.Span<byte> data)");
        sb.AppendLine("        {");
        sb.AppendLine("            var originalData = data;");
        foreach (var member in nestedType.Members)
        {
            if (member.IsList)
            {
                sb.AppendLine($"            throw new System.NotImplementedException(\"Collection serialization is not implemented for member {member.Name}.\");");
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"            var bytesWritten = {GetSimpleTypeName(member.TypeName)}.Serialize(obj.{member.Name}, data);");
                sb.AppendLine($"            data = data.Slice(bytesWritten);");
            }
            else if (member.IsStringType)
            {
                sb.AppendLine($"            data.WriteString(obj.{member.Name});");
            }
            else if (member.IsUnmanagedType)
            {
                var writeMethod = $"Write{GetSimpleTypeName(member.TypeName)}";
                sb.AppendLine($"            data.{writeMethod}(obj.{member.Name});");
            }
        }
        sb.AppendLine("            return originalData.Length - data.Length;");
        sb.AppendLine("        }");
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
