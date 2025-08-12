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
            GenerateCollectionSerialization(sb, member);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"        var bytesWritten = {GetSimpleTypeName(member.TypeName)}.Serialize(obj.{member.Name}, data);");
            sb.AppendLine($"        data = data.Slice(bytesWritten);");
        }
        else if (member.IsStringType)
        {
            sb.AppendLine($"        data.WriteString(obj.{member.Name});");
        }
        else if (member.IsUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(member.TypeName);
            var writeMethod = $"Write{typeName}";
            sb.AppendLine($"        data.{writeMethod}(obj.{member.Name});");
        }
    }

    private static string GetMethodFriendlyTypeName(string typeName)
    {
        return typeName switch
        {
            "int" => "Int32",
            "uint" => "UInt32",
            "short" => "Int16",
            "ushort" => "UInt16",
            "long" => "Int64",
            "ulong" => "UInt64",
            "byte" => "Byte",
            "float" => "Single",
            "bool" => "Boolean",
            _ => GetSimpleTypeName(typeName)
        };
    }

    private static void GenerateCollectionSerialization(StringBuilder sb, MemberToGenerate member)
    {
        sb.AppendLine($"        data.WriteInt32(obj.{member.Name}.Count);");

        if (member.CollectionInfo is not null && member.CollectionInfo.Value.PolymorphicMode != PolymorphicMode.None)
        {
            sb.AppendLine($"        throw new System.NotImplementedException(\"Polymorphic collection serialization is not implemented for member {member.Name}.\");");
            return;
        }

        sb.AppendLine($"        for (int i = 0; i < obj.{member.Name}.Count; i++)");
        sb.AppendLine("        {");

        var typeArg = member.ListTypeArgument.Value;
        if (typeArg.IsUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(typeArg.TypeName);
            sb.AppendLine($"            data.Write{typeName}(obj.{member.Name}[i]);");
        }
        else if (typeArg.IsStringType)
        {
            sb.AppendLine($"            data.WriteString(obj.{member.Name}[i]);");
        }
        else if (typeArg.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"            var bytesWritten = {GetSimpleTypeName(typeArg.TypeName)}.Serialize(obj.{member.Name}[i], data);");
            sb.AppendLine($"            data = data.Slice(bytesWritten);");
        }

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
