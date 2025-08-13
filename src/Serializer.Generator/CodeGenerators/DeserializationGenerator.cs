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
        var newKeyword = typeToGenerate.HasSerializableBaseType ? "new " : "";
        sb.AppendLine($"    public static {newKeyword}{typeToGenerate.Name} Deserialize(System.ReadOnlySpan<byte> data, out int bytesRead)");
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
        if (member.IsList || member.IsCollection)
        {
            GenerateCollectionDeserialization(sb, member);
        }
        else if (member.PolymorphicInfo is not null)
        {
            GeneratePolymorphicDeserialization(sb, member);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"        obj.{member.Name} = {TypeHelper.GetSimpleTypeName(member.TypeName)}.Deserialize(data, out var nestedBytesRead);");
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
            _ => TypeHelper.GetMethodFriendlyTypeName(typeName)
        };
    }

    private static bool ShouldUsePolymorphicSerialization(MemberToGenerate member)
    {
        // Only use polymorphic logic if explicitly configured
        if (member.CollectionInfo?.PolymorphicMode != PolymorphicMode.None)
            return true;
            
        // Or if SerializePolymorphic attribute is present with actual options
        if (member.PolymorphicInfo?.Options.IsEmpty == false)
            return true;
            
        return false;
    }

    private static void GenerateCollectionDeserialization(StringBuilder sb, MemberToGenerate member)
    {
        // Determine the count type to use
        var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
        var countReadMethod = TypeHelper.GetReadMethodName(countType);
        
        sb.AppendLine($"        var {member.Name}Count = data.{countReadMethod}();");
        
        // Generate appropriate collection instantiation
        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        if (member.CollectionTypeInfo?.IsArray == true)
        {
            sb.AppendLine($"        obj.{member.Name} = new {elementTypeName}[{member.Name}Count];");
        }
        else if (member.CollectionTypeInfo?.ConcreteTypeName != null)
        {
            var concreteTypeName = member.CollectionTypeInfo.Value.ConcreteTypeName;
            if (SupportsCapacityConstructor(concreteTypeName))
            {
                sb.AppendLine($"        var temp{member.Name} = new {concreteTypeName}<{elementTypeName}>({member.Name}Count);");
            }
            else
            {
                sb.AppendLine($"        var temp{member.Name} = new {concreteTypeName}<{elementTypeName}>();");
            }
        }
        else
        {
            sb.AppendLine($"        obj.{member.Name} = new System.Collections.Generic.List<{elementTypeName}>({member.Name}Count);");
        }

        if (ShouldUsePolymorphicSerialization(member))
        {
            if (member.CollectionInfo.Value.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
            {
                sb.AppendLine($"        for (int i = 0; i < {member.Name}Count; i++)");
                sb.AppendLine("        {");
                sb.AppendLine($"            {member.ListTypeArgument!.Value.TypeName} item;");
                var itemMember = new MemberToGenerate(
                    "item",
                    member.ListTypeArgument!.Value.TypeName,
                    member.ListTypeArgument.Value.IsUnmanagedType,
                    member.ListTypeArgument.Value.IsStringType,
                    member.ListTypeArgument.Value.HasGenerateSerializerAttribute,
                    false,
                    null,
                    null,
                    member.PolymorphicInfo,
                    false,
                    null
                );
                GeneratePolymorphicItemDeserialization(sb, itemMember, "item");
                sb.AppendLine($"            obj.{member.Name}.Add(item);");
                sb.AppendLine("        }");
                return;
            }

            if (member.CollectionInfo.Value.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                var info = member.PolymorphicInfo!.Value;
                var typeIdProperty = member.CollectionInfo.Value.TypeIdProperty;

                sb.AppendLine($"        switch (obj.{typeIdProperty})");
                sb.AppendLine("        {");

                foreach (var option in info.Options)
                {
                    var key = option.Key.ToString();
                    if (info.EnumUnderlyingType is not null)
                    {
                        key = $"({info.TypeIdType}){key}";
                    }
                    else if (info.TypeIdType.EndsWith("Enum"))
                    {
                        key = $"{info.TypeIdType}.{key}";
                    }
                    
                    sb.AppendLine($"            case {key}:");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                for (int i = 0; i < {member.Name}Count; i++)");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    var item = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize(data, out var itemBytesRead);");
                    sb.AppendLine($"                    obj.{member.Name}.Add(item);");
                    sb.AppendLine($"                    data = data.Slice(itemBytesRead);");
                    sb.AppendLine("                }");
                    sb.AppendLine("                break;");
                    sb.AppendLine("            }");
                }

                sb.AppendLine("            default:");
                sb.AppendLine($"                throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{obj.{typeIdProperty}}}\");");
                sb.AppendLine("        }");
                return;
            }
        }

        sb.AppendLine($"        for (int i = 0; i < {member.Name}Count; i++)");
        sb.AppendLine("        {");

        var collectionTarget = member.CollectionTypeInfo?.ConcreteTypeName != null ? $"temp{member.Name}" : $"obj.{member.Name}";
        
        if (member.CollectionTypeInfo?.IsArray == true)
        {
            GenerateArrayElementDeserialization(sb, member.CollectionTypeInfo.Value, member.Name, "i");
        }
        else if (member.ListTypeArgument is not null)
        {
            GenerateListElementDeserialization(sb, member.ListTypeArgument.Value, collectionTarget);
        }
        else if (member.CollectionTypeInfo is not null)
        {
            GenerateCollectionElementDeserialization(sb, member.CollectionTypeInfo.Value, collectionTarget);
        }

        sb.AppendLine("        }");
        
        // Assign temporary collection to the actual property if needed
        if (member.CollectionTypeInfo?.ConcreteTypeName != null)
        {
            sb.AppendLine($"        obj.{member.Name} = temp{member.Name};");
        }
    }

    private static void GeneratePolymorphicDeserialization(StringBuilder sb, MemberToGenerate member)
    {
        var info = member.PolymorphicInfo!.Value;
        var typeIdProperty = info.TypeIdProperty;
        var bytesReadVar = $"{member.Name}BytesRead";

        sb.AppendLine($"        int {bytesReadVar};");
        sb.AppendLine($"        obj.{member.Name} = default;");

        string typeIdVar = $"obj.{typeIdProperty}";
        if (string.IsNullOrEmpty(typeIdProperty))
        {
            var typeIdTypeName = GetMethodFriendlyTypeName(info.EnumUnderlyingType ?? info.TypeIdType);
            sb.AppendLine($"        var typeId = data.Read{typeIdTypeName}();");
            typeIdVar = "typeId";
        }
        
        sb.AppendLine($"        switch (({info.TypeIdType}){typeIdVar})");
        sb.AppendLine("        {");

        foreach (var option in info.Options)
        {
            var key = option.Key.ToString();
            if (info.EnumUnderlyingType is not null)
            {
                key = $"({info.TypeIdType}){key}";
            }
            else if (info.TypeIdType.EndsWith("Enum"))
            {
                key = $"{info.TypeIdType}.{key}";
            }


            sb.AppendLine($"            case {key}:");
            sb.AppendLine($"                obj.{member.Name} = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize(data, out {bytesReadVar});");
            sb.AppendLine($"                data = data.Slice({bytesReadVar});");
            sb.AppendLine("                break;");
        }

        sb.AppendLine("            default:");
        sb.AppendLine($"                throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{typeIdVar}}}\");");
        sb.AppendLine("        }");
    }
    private static void GeneratePolymorphicItemDeserialization(StringBuilder sb, MemberToGenerate member, string assignmentTarget)
    {
        var info = member.PolymorphicInfo!.Value;
        var bytesReadVar = $"{assignmentTarget}BytesRead";

        sb.AppendLine($"            int {bytesReadVar};");

        var typeIdTypeName = GetMethodFriendlyTypeName(info.EnumUnderlyingType ?? info.TypeIdType);
        sb.AppendLine($"            var typeId = data.Read{typeIdTypeName}();");
        var typeIdVar = "typeId";

        sb.AppendLine($"            switch (({info.TypeIdType}){typeIdVar})");
        sb.AppendLine("            {");

        foreach (var option in info.Options)
        {
            var key = option.Key.ToString();
            if (info.EnumUnderlyingType is not null)
            {
                key = $"({info.TypeIdType}){key}";
            }
            else if (info.TypeIdType.EndsWith("Enum"))
            {
                key = $"{info.TypeIdType}.{key}";
            }

            sb.AppendLine($"                case {key}:");
            sb.AppendLine($"                    {assignmentTarget} = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize(data, out {bytesReadVar});");
            sb.AppendLine($"                    data = data.Slice({bytesReadVar});");
            sb.AppendLine("                    break;");
        }

        sb.AppendLine("                default:");
        sb.AppendLine($"                    throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{typeIdVar}}}\");");
        sb.AppendLine("            }");
    }

    private static void GenerateArrayElementDeserialization(StringBuilder sb, CollectionTypeInfo elementInfo, string arrayName, string indexVar)
    {
        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.AppendLine($"            obj.{arrayName}[{indexVar}] = data.Read{typeName}();");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.AppendLine($"            obj.{arrayName}[{indexVar}] = data.ReadString();");
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"            obj.{arrayName}[{indexVar}] = {TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize(data, out var itemBytesRead);");
            sb.AppendLine($"            data = data.Slice(itemBytesRead);");
        }
    }

    private static void GenerateListElementDeserialization(StringBuilder sb, ListTypeArgumentInfo elementInfo, string collectionTarget)
    {
        if (elementInfo.IsUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(elementInfo.TypeName);
            sb.AppendLine($"            {collectionTarget}.Add(data.Read{typeName}());");
        }
        else if (elementInfo.IsStringType)
        {
            sb.AppendLine($"            {collectionTarget}.Add(data.ReadString());");
        }
        else if (elementInfo.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"            {collectionTarget}.Add({TypeHelper.GetSimpleTypeName(elementInfo.TypeName)}.Deserialize(data, out var itemBytesRead));");
            sb.AppendLine($"            data = data.Slice(itemBytesRead);");
        }
    }

    private static void GenerateCollectionElementDeserialization(StringBuilder sb, CollectionTypeInfo elementInfo, string collectionTarget)
    {
        var addMethod = GetCollectionAddMethod(elementInfo.CollectionTypeName);
        
        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.AppendLine($"            {collectionTarget}.{addMethod}(data.Read{typeName}());");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.AppendLine($"            {collectionTarget}.{addMethod}(data.ReadString());");
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"            {collectionTarget}.{addMethod}({TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize(data, out var itemBytesRead));");
            sb.AppendLine($"            data = data.Slice(itemBytesRead);");
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
