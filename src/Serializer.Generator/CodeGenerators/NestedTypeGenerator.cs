using Serializer.Generator.Models;
using System.Text;

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
            if (member.IsList)
            {
                // Determine the count type to use
                var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
                var countSizeExpression = TypeHelper.GetSizeOfExpression(countType);
                
                sb.AppendLine($"            size += {countSizeExpression}; // Count size for {member.Name}");
                if (member.ListTypeArgument is not null)
                {
                    var typeArg = member.ListTypeArgument.Value;
                    if (typeArg.IsUnmanagedType)
                    {
                        sb.AppendLine($"            size += obj.{member.Name}.Count * sizeof({typeArg.TypeName});");
                    }
                    else if (typeArg.IsStringType)
                    {
                        sb.AppendLine($"            foreach(var item in obj.{member.Name}) {{ size += StringEx.MeasureSize(item); }}");
                    }
                    else if (typeArg.HasGenerateSerializerAttribute)
                    {
                        sb.AppendLine($"            foreach(var item in obj.{member.Name}) {{ size += {TypeHelper.GetSimpleTypeName(typeArg.TypeName)}.GetPacketSize(item); }}");
                    }
                }
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"            size += {TypeHelper.GetSimpleTypeName(member.TypeName)}.GetPacketSize(obj.{member.Name});");
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
                // Determine the count type to use
                var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
                var countReadMethod = TypeHelper.GetReadMethodName(countType);
                
                sb.AppendLine($"            var {member.Name}Count = data.{countReadMethod}();");
                if (member.ListTypeArgument is not null)
                {
                    sb.AppendLine($"            obj.{member.Name} = new System.Collections.Generic.List<{member.ListTypeArgument.Value.TypeName}>({member.Name}Count);");
                    sb.AppendLine($"            for (int i = 0; i < {member.Name}Count; i++)");
                    sb.AppendLine("            {");
                    var typeArg = member.ListTypeArgument.Value;
                    if (typeArg.IsUnmanagedType)
                    {
                        var typeName = GetMethodFriendlyTypeName(typeArg.TypeName);
                        sb.AppendLine($"                obj.{member.Name}.Add(data.Read{typeName}());");
                    }
                    else if (typeArg.IsStringType)
                    {
                        sb.AppendLine($"                obj.{member.Name}.Add(data.ReadString());");
                    }
                    else if (typeArg.HasGenerateSerializerAttribute)
                    {
                        sb.AppendLine($"                obj.{member.Name}.Add({TypeHelper.GetSimpleTypeName(typeArg.TypeName)}.Deserialize(data, out var itemBytesRead));");
                        sb.AppendLine($"                data = data.Slice(itemBytesRead);");
                    }
                    sb.AppendLine("            }");
                }
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"            obj.{member.Name} = {TypeHelper.GetSimpleTypeName(member.TypeName)}.Deserialize(data, out var nestedBytesRead);");
                sb.AppendLine($"            data = data.Slice(nestedBytesRead);");
            }
            else if (member.IsStringType)
            {
                sb.AppendLine($"            obj.{member.Name} = data.ReadString();");
            }
            else if (member.IsUnmanagedType)
            {
                var typeName = GetMethodFriendlyTypeName(member.TypeName);
                var readMethod = $"Read{typeName}";
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
                // Determine the count type to use
                var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
                var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
                
                sb.AppendLine($"            data.{countWriteMethod}(obj.{member.Name}.Count);");
                if (member.ListTypeArgument is not null)
                {
                    sb.AppendLine($"            for (int i = 0; i < obj.{member.Name}.Count; i++)");
                    sb.AppendLine("            {");
                    var typeArg = member.ListTypeArgument.Value;
                    if (typeArg.IsUnmanagedType)
                    {
                        var typeName = GetMethodFriendlyTypeName(typeArg.TypeName);
                        sb.AppendLine($"                data.Write{typeName}(obj.{member.Name}[i]);");
                    }
                    else if (typeArg.IsStringType)
                    {
                        sb.AppendLine($"                data.WriteString(obj.{member.Name}[i]);");
                    }
                    else if (typeArg.HasGenerateSerializerAttribute)
                    {
                        sb.AppendLine($"                var bytesWritten = {TypeHelper.GetSimpleTypeName(typeArg.TypeName)}.Serialize(obj.{member.Name}[i], data);");
                        sb.AppendLine($"                data = data.Slice(bytesWritten);");
                    }
                    sb.AppendLine("            }");
                }
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"            var bytesWritten = {TypeHelper.GetSimpleTypeName(member.TypeName)}.Serialize(obj.{member.Name}, data);");
                sb.AppendLine($"            data = data.Slice(bytesWritten);");
            }
            else if (member.IsStringType)
            {
                sb.AppendLine($"            data.WriteString(obj.{member.Name});");
            }
            else if (member.IsUnmanagedType)
            {
                var typeName = GetMethodFriendlyTypeName(member.TypeName);
                var writeMethod = $"Write{typeName}";
                sb.AppendLine($"            data.{writeMethod}(obj.{member.Name});");
            }
        }
        sb.AppendLine("            return originalData.Length - data.Length;");
        sb.AppendLine("        }");
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
            _ => TypeHelper.GetMethodFriendlyTypeName(typeName)
        };
    }

}
