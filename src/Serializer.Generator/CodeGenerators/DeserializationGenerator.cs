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
            GenerateCollectionDeserialization(sb, member);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"        obj.{member.Name} = {GetSimpleTypeName(member.TypeName)}.Deserialize(data, out var nestedBytesRead);");
            sb.AppendLine($"        data = data.Slice(nestedBytesRead);");
        }
        else if (member.IsStringType)
        {
            sb.AppendLine($"        obj.{member.Name} = data.ReadString();");
        }
        else if (member.IsUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(member.TypeName);
            var readMethod = $"Read{typeName}";
            sb.AppendLine($"        obj.{member.Name} = data.{readMethod}();");
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
            "double" => "Double",
            _ => GetSimpleTypeName(typeName)
        };
    }

    private static void GenerateCollectionDeserialization(StringBuilder sb, MemberToGenerate member)
    {
        sb.AppendLine($"        var {member.Name}Count = data.ReadInt32();");
        sb.AppendLine($"        obj.{member.Name} = new System.Collections.Generic.List<{member.ListTypeArgument!.Value.TypeName}>({member.Name}Count);");

        if (member.CollectionInfo is not null && member.CollectionInfo.Value.PolymorphicMode != PolymorphicMode.None)
        {
            sb.AppendLine($"        throw new System.NotImplementedException(\"Polymorphic collection deserialization is not implemented for member {member.Name}.\");");
            return;
        }

        sb.AppendLine($"        for (int i = 0; i < {member.Name}Count; i++)");
        sb.AppendLine("        {");

        var typeArg = member.ListTypeArgument.Value;
        if (typeArg.IsUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(typeArg.TypeName);
            sb.AppendLine($"            obj.{member.Name}.Add(data.Read{typeName}());");
        }
        else if (typeArg.IsStringType)
        {
            sb.AppendLine($"            obj.{member.Name}.Add(data.ReadString());");
        }
        else if (typeArg.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"            obj.{member.Name}.Add({GetSimpleTypeName(typeArg.TypeName)}.Deserialize(data, out var itemBytesRead));");
            sb.AppendLine($"            data = data.Slice(itemBytesRead);");
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
