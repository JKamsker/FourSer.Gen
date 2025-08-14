using System.Text;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

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
            if (member.IsList || member.IsCollection)
            {
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
                else if (member.CollectionTypeInfo is not null)
                {
                    var collectionInfo = member.CollectionTypeInfo.Value;
                    if (collectionInfo.IsElementUnmanagedType)
                    {
                        sb.AppendLine($"            size += obj.{member.Name}.Count * sizeof({collectionInfo.ElementTypeName});");
                    }
                    else if (collectionInfo.IsElementStringType)
                    {
                        sb.AppendLine($"            foreach(var item in obj.{member.Name}) {{ size += StringEx.MeasureSize(item); }}");
                    }
                    else if (collectionInfo.HasElementGenerateSerializerAttribute)
                    {
                        sb.AppendLine($"            foreach(var item in obj.{member.Name}) {{ size += {TypeHelper.GetSimpleTypeName(collectionInfo.ElementTypeName)}.GetPacketSize(item); }}");
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
        var newKeyword = nestedType.HasSerializableBaseType ? "new " : "";
        sb.AppendLine($"        public static {newKeyword}{nestedType.Name} Deserialize(System.ReadOnlySpan<byte> data, out int bytesRead)");
        sb.AppendLine("        {");
        sb.AppendLine("            bytesRead = 0;");
        sb.AppendLine("            var originalData = data;");
        sb.AppendLine($"            var obj = new {nestedType.Name}();");
        foreach (var member in nestedType.Members)
        {
            if (member.IsList || member.IsCollection)
            {
                var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
                var countReadMethod = TypeHelper.GetReadMethodName(countType);
                
                sb.AppendLine($"            var {member.Name}Count = data.{countReadMethod}();");
                if (member.ListTypeArgument is not null)
                {
                    var elementTypeName = member.ListTypeArgument.Value.TypeName;
                    sb.AppendLine($"            obj.{member.Name} = new System.Collections.Generic.List<{elementTypeName}>({member.Name}Count);");
                }
                else if (member.CollectionTypeInfo is not null)
                {
                    var elementTypeName = member.CollectionTypeInfo.Value.ElementTypeName;
                    if (member.CollectionTypeInfo.Value.ConcreteTypeName != null)
                    {
                        var concreteTypeName = member.CollectionTypeInfo.Value.ConcreteTypeName;
                        if (SupportsCapacityConstructor(concreteTypeName))
                        {
                            sb.AppendLine($"            var temp{member.Name} = new {concreteTypeName}<{elementTypeName}>({member.Name}Count);");
                        }
                        else
                        {
                            sb.AppendLine($"            var temp{member.Name} = new {concreteTypeName}<{elementTypeName}>();");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"            obj.{member.Name} = new System.Collections.Generic.List<{elementTypeName}>({member.Name}Count);");
                    }
                }
                
                sb.AppendLine($"            for (int i = 0; i < {member.Name}Count; i++)");
                sb.AppendLine("            {");
                var collectionTarget = member.CollectionTypeInfo?.ConcreteTypeName != null ? $"temp{member.Name}" : $"obj.{member.Name}";
                
                if (member.ListTypeArgument is not null)
                {
                    var typeArg = member.ListTypeArgument.Value;
                    if (typeArg.IsUnmanagedType)
                    {
                        var typeName = GetMethodFriendlyTypeName(typeArg.TypeName);
                        sb.AppendLine($"                {collectionTarget}.Add(data.Read{typeName}());");
                    }
                    else if (typeArg.IsStringType)
                    {
                        sb.AppendLine($"                {collectionTarget}.Add(data.ReadString());");
                    }
                    else if (typeArg.HasGenerateSerializerAttribute)
                    {
                        sb.AppendLine($"                {collectionTarget}.Add({TypeHelper.GetSimpleTypeName(typeArg.TypeName)}.Deserialize(data, out var itemBytesRead));");
                        sb.AppendLine($"                data = data.Slice(itemBytesRead);");
                    }
                }
                else if (member.CollectionTypeInfo is not null)
                {
                    var collectionInfo = member.CollectionTypeInfo.Value;
                    var addMethod = GetCollectionAddMethod(collectionInfo.CollectionTypeName);
                    if (collectionInfo.IsElementUnmanagedType)
                    {
                        var typeName = GetMethodFriendlyTypeName(collectionInfo.ElementTypeName);
                        sb.AppendLine($"                {collectionTarget}.{addMethod}(data.Read{typeName}());");
                    }
                    else if (collectionInfo.IsElementStringType)
                    {
                        sb.AppendLine($"                {collectionTarget}.{addMethod}(data.ReadString());");
                    }
                    else if (collectionInfo.HasElementGenerateSerializerAttribute)
                    {
                        sb.AppendLine($"                {collectionTarget}.{addMethod}({TypeHelper.GetSimpleTypeName(collectionInfo.ElementTypeName)}.Deserialize(data, out var itemBytesRead));");
                        sb.AppendLine($"                data = data.Slice(itemBytesRead);");
                    }
                }
                sb.AppendLine("            }");
                
                if (member.CollectionTypeInfo?.ConcreteTypeName != null)
                {
                    sb.AppendLine($"            obj.{member.Name} = temp{member.Name};");
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
            if (member.IsList || member.IsCollection)
            {
                var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
                var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
                
                sb.AppendLine($"            data.{countWriteMethod}(obj.{member.Name}.Count);");
                if (member.ListTypeArgument is not null)
                {
                    sb.AppendLine($"            for (int i = 0; i < obj.{member.Name}.Count; i++)");
                    sb.AppendLine("            {");
                    GenerateNestedListElementSerialization(sb, member.ListTypeArgument.Value, $"obj.{member.Name}[i]");
                    sb.AppendLine("            }");
                }
                else if (member.CollectionTypeInfo is not null)
                {
                    sb.AppendLine($"            foreach (var item in obj.{member.Name})");
                    sb.AppendLine("            {");
                    GenerateNestedCollectionElementSerialization(sb, member.CollectionTypeInfo.Value, "item");
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

    private static void GenerateNestedListElementSerialization(StringBuilder sb, ListTypeArgumentInfo elementInfo, string elementAccess)
    {
        if (elementInfo.IsUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(elementInfo.TypeName);
            sb.AppendLine($"                data.Write{typeName}({elementAccess});");
        }
        else if (elementInfo.IsStringType)
        {
            sb.AppendLine($"                data.WriteString({elementAccess});");
        }
        else if (elementInfo.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"                var bytesWritten = {TypeHelper.GetSimpleTypeName(elementInfo.TypeName)}.Serialize({elementAccess}, data);");
            sb.AppendLine($"                data = data.Slice(bytesWritten);");
        }
    }

    private static void GenerateNestedCollectionElementSerialization(StringBuilder sb, CollectionTypeInfo elementInfo, string elementAccess)
    {
        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.AppendLine($"                data.Write{typeName}({elementAccess});");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.AppendLine($"                data.WriteString({elementAccess});");
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"                var bytesWritten = {TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Serialize({elementAccess}, data);");
            sb.AppendLine($"                data = data.Slice(bytesWritten);");
        }
    }

    private static string GetCollectionAddMethod(string collectionTypeName)
    {
        return collectionTypeName switch
        {
            "System.Collections.Generic.Queue<T>" => "Enqueue",
            "System.Collections.Generic.Stack<T>" => "Push",
            "System.Collections.Generic.LinkedList<T>" => "AddLast",
            _ => "Add"
        };
    }

    private static bool SupportsCapacityConstructor(string collectionTypeName)
    {
        return collectionTypeName switch
        {
            "System.Collections.Generic.List" => true,
            "System.Collections.Generic.HashSet" => true,
            "System.Collections.Generic.Queue" => true,
            "System.Collections.Generic.Stack" => true,
            "System.Collections.Concurrent.ConcurrentBag" => false, // No capacity constructor
            "System.Collections.Generic.LinkedList" => false, // No capacity constructor
            _ => false
        };
    }
}