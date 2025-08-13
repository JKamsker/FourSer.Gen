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
        if (member.IsList)
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

    private static void GenerateCollectionDeserialization(StringBuilder sb, MemberToGenerate member)
    {
        // Determine the count type to use
        var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
        var countReadMethod = TypeHelper.GetReadMethodName(countType);
        
        sb.AppendLine($"        var {member.Name}Count = data.{countReadMethod}();");
        sb.AppendLine($"        obj.{member.Name} = new System.Collections.Generic.List<{member.ListTypeArgument!.Value.TypeName}>({member.Name}Count);");

        if (member.CollectionInfo is not null && member.PolymorphicInfo is not null)
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
                    member.PolymorphicInfo
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
            sb.AppendLine($"            obj.{member.Name}.Add({TypeHelper.GetSimpleTypeName(typeArg.TypeName)}.Deserialize(data, out var itemBytesRead));");
            sb.AppendLine($"            data = data.Slice(itemBytesRead);");
        }

        sb.AppendLine("        }");
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
}
