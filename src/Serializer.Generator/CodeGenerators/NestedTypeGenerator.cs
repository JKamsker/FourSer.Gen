using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Serializer.Generator.CodeGenerators;

/// <summary>
/// Generates serialization code for nested types
/// </summary>
public static class NestedTypeGenerator
{
    public static void GenerateNestedTypes(StringBuilder sb, INamedTypeSymbol parentType, List<INamedTypeSymbol> nestedTypes)
    {
        if (nestedTypes == null || !nestedTypes.Any())
            return;

        foreach (var nestedType in nestedTypes)
        {
            var nestedMembers = TypeAnalyzer.GetSerializableMembers(nestedType);
            var nestedClassToGenerate = new ClassToGenerate(nestedType.Name,
                nestedType.ContainingNamespace.ToDisplayString(), nestedMembers, nestedType.IsValueType);

            sb.AppendLine();
            sb.AppendLine($"    public partial {(nestedType.IsValueType ? "struct" : "class")} {nestedType.Name} : ISerializable<{nestedType.Name}>");
            sb.AppendLine("    {");

            GenerateNestedGetPacketSize(sb, nestedType, nestedMembers);
            sb.AppendLine();
            GenerateNestedDeserialize(sb, nestedType, nestedMembers);
            sb.AppendLine();
            GenerateNestedSerialize(sb, nestedType, nestedMembers);

            sb.AppendLine("    }");
        }
    }

    private static void GenerateNestedGetPacketSize(StringBuilder sb, INamedTypeSymbol nestedType, List<ISymbol> nestedMembers)
    {
        sb.AppendLine($"        public static int GetPacketSize({nestedType.Name} obj)");
        sb.AppendLine("        {");
        sb.AppendLine("            var size = 0;");

        foreach (var member in nestedMembers)
        {
            var memberType = TypeAnalyzer.GetMemberType(member);
            if (memberType.SpecialType == SpecialType.System_String)
            {
                sb.AppendLine($"            size += sizeof(int); // Size for string length");
                sb.AppendLine($"            size += System.Text.Encoding.UTF8.GetByteCount(obj.{member.Name});");
            }
            else if (memberType.IsUnmanagedType)
            {
                sb.AppendLine($"            size += sizeof({memberType.ToDisplayString()}); // Size for unmanaged type {member.Name}");
            }
        }

        sb.AppendLine("            return size;");
        sb.AppendLine("        }");
    }

    private static void GenerateNestedDeserialize(StringBuilder sb, INamedTypeSymbol nestedType, List<ISymbol> nestedMembers)
    {
        sb.AppendLine($"        public static {nestedType.Name} Deserialize(ReadOnlySpan<byte> data, out int bytesRead)");
        sb.AppendLine("        {");
        sb.AppendLine("            bytesRead = 0;");
        sb.AppendLine("            var originalData = data;");
        sb.AppendLine($"            var obj = new {nestedType.Name}();");

        foreach (var member in nestedMembers)
        {
            var memberType = TypeAnalyzer.GetMemberType(member);
            if (memberType.SpecialType == SpecialType.System_String)
            {
                sb.AppendLine($"            obj.{member.Name} = data.ReadString();");
            }
            else if (memberType.IsUnmanagedType)
            {
                sb.AppendLine($"            obj.{member.Name} = data.Read{memberType.Name}();");
            }
        }

        sb.AppendLine("            bytesRead = originalData.Length - data.Length;");
        sb.AppendLine("            return obj;");
        sb.AppendLine("        }");
    }

    private static void GenerateNestedSerialize(StringBuilder sb, INamedTypeSymbol nestedType, List<ISymbol> nestedMembers)
    {
        sb.AppendLine($"        public static int Serialize({nestedType.Name} obj, Span<byte> data)");
        sb.AppendLine("        {");
        sb.AppendLine("            var originalData = data;");

        foreach (var member in nestedMembers)
        {
            var memberType = TypeAnalyzer.GetMemberType(member);
            if (memberType.SpecialType == SpecialType.System_String)
            {
                sb.AppendLine($"            data.WriteString(obj.{member.Name});");
            }
            else if (memberType.IsUnmanagedType)
            {
                sb.AppendLine($"            data.Write{memberType.Name}(obj.{member.Name});");
            }
        }

        sb.AppendLine("            return originalData.Length - data.Length;");
        sb.AppendLine("        }");
    }
}