using Serializer.Generator.Models;
using System.Text;

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

        // Pre-pass to update TypeId properties
        foreach (var member in typeToGenerate.Members)
        {
            if (member.PolymorphicInfo is not { } info || string.IsNullOrEmpty(info.TypeIdProperty))
            {
                continue;
            }

            // Skip collections with SingleTypeId mode - they use the TypeIdProperty directly
            if (member.IsList && member.CollectionInfo?.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                continue;
            }

            sb.AppendLine($"        switch (obj.{member.Name})");
            sb.AppendLine("        {");
            foreach (var option in info.Options)
            {
                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                var key = option.Key.ToString();
                if (info.EnumUnderlyingType is not null)
                {
                    key = $"({info.TypeIdType}){key}";
                }
                else if (info.TypeIdType.EndsWith("Enum"))
                {
                    key = $"{info.TypeIdType}.{key}";
                }
                sb.AppendLine($"            case {typeName}:");
                sb.AppendLine($"                obj.{info.TypeIdProperty} = {key};");
                sb.AppendLine("                break;");
            }
            sb.AppendLine("            case null:");
            sb.AppendLine("                break;");
            sb.AppendLine("        }");
        }

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
        else if (member.PolymorphicInfo is not null)
        {
            GeneratePolymorphicSerialization(sb, member);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"        var bytesWritten = {TypeHelper.GetSimpleTypeName(member.TypeName)}.Serialize(obj.{member.Name}, data);");
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
            sb.AppendLine($"        data.{writeMethod}(({typeName})obj.{member.Name});");
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

    private static void GenerateCollectionSerialization(StringBuilder sb, MemberToGenerate member)
    {
        // Determine the count type to use
        var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
        var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
        
        sb.AppendLine($"        data.{countWriteMethod}(({countType})obj.{member.Name}.Count);");

        if (member.CollectionInfo is not null && member.PolymorphicInfo is not null)
        {
            if (member.CollectionInfo.Value.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
            {
                var itemBytesWrittenVar = $"{member.Name}ItemBytesWritten";
                sb.AppendLine($"        int {itemBytesWrittenVar};");
                sb.AppendLine($"        foreach(var item in obj.{member.Name})");
                sb.AppendLine("        {");

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

                GeneratePolymorphicItemSerialization(sb, itemMember, "item", itemBytesWrittenVar);
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
                    sb.AppendLine($"                foreach(var item in obj.{member.Name})");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    var bytesWritten = {TypeHelper.GetSimpleTypeName(option.Type)}.Serialize(({TypeHelper.GetSimpleTypeName(option.Type)})item, data);");
                    sb.AppendLine($"                    data = data.Slice(bytesWritten);");
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

        sb.AppendLine($"        for (int i = 0; i < obj.{member.Name}.Count; i++)");
        sb.AppendLine("        {");

        var typeArg = member.ListTypeArgument.Value;
        if (typeArg.IsUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(typeArg.TypeName);
            sb.AppendLine($"            data.Write{typeName}(({typeName})obj.{member.Name}[i]);");
        }
        else if (typeArg.IsStringType)
        {
            sb.AppendLine($"            data.WriteString(obj.{member.Name}[i]);");
        }
        else if (typeArg.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"            var bytesWritten = {TypeHelper.GetSimpleTypeName(typeArg.TypeName)}.Serialize(obj.{member.Name}[i], data);");
            sb.AppendLine($"            data = data.Slice(bytesWritten);");
        }

        sb.AppendLine("        }");
    }

    private static void GeneratePolymorphicSerialization(StringBuilder sb, MemberToGenerate member)
    {
        var info = member.PolymorphicInfo!.Value;
        var bytesWrittenVar = $"{member.Name}BytesWritten";

        sb.AppendLine($"        int {bytesWrittenVar};");
        sb.AppendLine($"        switch (obj.{member.Name})");
        sb.AppendLine("        {");

        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.AppendLine($"            case {typeName} typedInstance:");

            if (string.IsNullOrEmpty(info.TypeIdProperty))
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
                
                var underlyingType = info.EnumUnderlyingType ?? info.TypeIdType;
                var typeIdTypeName = GetMethodFriendlyTypeName(underlyingType);
                sb.AppendLine($"                data.Write{typeIdTypeName}(({underlyingType}){key});");
            }

            sb.AppendLine($"                {bytesWrittenVar} = {typeName}.Serialize(typedInstance, data);");
            sb.AppendLine($"                data = data.Slice({bytesWrittenVar});");
            sb.AppendLine("                break;");
        }

        sb.AppendLine("            case null:");
        sb.AppendLine("                break;");
        sb.AppendLine("            default:");
        sb.AppendLine($"                throw new System.IO.InvalidDataException($\"Unknown type for {member.Name}: {{obj.{member.Name}?.GetType().FullName}}\");");
        sb.AppendLine("        }");
    }

    private static void GeneratePolymorphicItemSerialization(StringBuilder sb, MemberToGenerate member, string instanceName, string bytesWrittenVar)
    {
        var info = member.PolymorphicInfo!.Value;

        sb.AppendLine($"            switch ({instanceName})");
        sb.AppendLine("            {");

        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.AppendLine($"                case {typeName} typedInstance:");

            if (string.IsNullOrEmpty(info.TypeIdProperty))
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
                var underlyingType = info.EnumUnderlyingType ?? info.TypeIdType;
                var typeIdTypeName = GetMethodFriendlyTypeName(underlyingType);
                sb.AppendLine($"                    data.Write{typeIdTypeName}(({underlyingType}){key});");
            }

            sb.AppendLine($"                    {bytesWrittenVar} = {typeName}.Serialize(typedInstance, data);");
            sb.AppendLine($"                    data = data.Slice({bytesWrittenVar});");
            sb.AppendLine("                    break;");
        }

        sb.AppendLine("                case null:");
        sb.AppendLine("                    break;");
        sb.AppendLine("                default:");
        sb.AppendLine($"                    throw new System.IO.InvalidDataException($\"Unknown type for {instanceName}: {{{instanceName}?.GetType().FullName}}\");");
        sb.AppendLine("            }");
    }
}
