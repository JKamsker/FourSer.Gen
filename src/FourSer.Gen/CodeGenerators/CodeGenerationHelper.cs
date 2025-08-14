using System.Text;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

public static class CodeGenerationHelper
{
    public static void GenerateMemberSerialization(StringBuilder sb, MemberToGenerate member)
    {
        if (member.IsCollection)
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
            sb.AppendLine($"        data.{member.WriteMethodName}(({member.TypeName})obj.{member.Name});");
        }
    }

    public static void GenerateMemberDeserialization(StringBuilder sb, MemberToGenerate member)
    {
        if (member.IsCollection)
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
            sb.AppendLine($"        obj.{member.Name} = data.{member.ReadMethodName}();");
        }
    }

    private static void GenerateCollectionDeserialization(StringBuilder sb, MemberToGenerate member)
    {
        var collectionInfo = member.CollectionTypeInfo!.Value;
        var countType = member.CollectionInfo?.CountType ?? "int";
        var countReadMethod = TypeHelper.GetReadMethodName(countType);

        sb.AppendLine($"        var count = data.{countReadMethod}();");

        // Polymorphic collection deserialization logic will go here

        sb.AppendLine($"        var collection = {collectionInfo.ConcreteTypeInstantiation};");
        sb.AppendLine($"        for (int i = 0; i < count; i++)");
        sb.AppendLine("        {");
        GenerateElementDeserialization(sb, collectionInfo, "collection");
        sb.AppendLine("        }");
        sb.AppendLine($"        obj.{member.Name} = collection;");
    }

    private static void GenerateElementDeserialization(StringBuilder sb, CollectionTypeInfo elementInfo, string collectionVar)
    {
        if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"            {collectionVar}.{elementInfo.AddMethodName}({TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize(data, out var itemBytesRead));");
            sb.AppendLine($"            data = data.Slice(itemBytesRead);");
        }
        else if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.AppendLine($"            {collectionVar}.{elementInfo.AddMethodName}(data.Read{typeName}());");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.AppendLine($"            {collectionVar}.{elementInfo.AddMethodName}(data.ReadString());");
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

    public static void GenerateMemberSizeCalculation(StringBuilder sb, MemberToGenerate member)
    {
        if (member.IsCollection)
        {
            GenerateCollectionSizeCalculation(sb, member);
        }
        else if (member.PolymorphicInfo is not null)
        {
            var info = member.PolymorphicInfo.Value;
            if (string.IsNullOrEmpty(info.TypeIdProperty))
            {
                var typeIdSize = info.EnumUnderlyingType is not null
                    ? $"sizeof({info.EnumUnderlyingType})"
                    : $"sizeof({info.TypeIdType})";
                sb.AppendLine($"        size += {typeIdSize};");
            }
            GeneratePolymorphicSizeCalculation(sb, member);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"        size += {TypeHelper.GetSimpleTypeName(member.TypeName)}.GetPacketSize(obj.{member.Name});");
        }
        else if (member.IsStringType)
        {
            sb.AppendLine($"        size += StringEx.MeasureSize(obj.{member.Name});");
        }
        else if (member.IsUnmanagedType)
        {
            sb.AppendLine($"        size += sizeof({member.TypeName});");
        }
    }

    private static void GenerateCollectionSizeCalculation(StringBuilder sb, MemberToGenerate member)
    {
        var collectionInfo = member.CollectionTypeInfo!.Value;
        var countType = member.CollectionInfo?.CountType ?? "int";
        var countSizeExpression = TypeHelper.GetSizeOfExpression(countType);

        sb.AppendLine($"        size += {countSizeExpression};");

        // Polymorphic collection size calculation logic will go here

        sb.AppendLine($"        foreach(var item in obj.{member.Name})");
        sb.AppendLine("        {");
        GenerateElementSizeCalculation(sb, collectionInfo, "item");
        sb.AppendLine("        }");
    }

    private static void GenerateElementSizeCalculation(StringBuilder sb, CollectionTypeInfo elementInfo, string elementAccess)
    {
        if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"            size += {TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.GetPacketSize({elementAccess});");
        }
        else if (elementInfo.IsElementUnmanagedType)
        {
            sb.AppendLine($"            size += sizeof({elementInfo.ElementTypeName});");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.AppendLine($"            size += StringEx.MeasureSize({elementAccess});");
        }
    }

    private static void GeneratePolymorphicSizeCalculation(StringBuilder sb, MemberToGenerate member, string instanceName = "")
    {
        if (string.IsNullOrEmpty(instanceName))
        {
            instanceName = $"obj.{member.Name}";
        }

        var info = member.PolymorphicInfo!.Value;

        sb.AppendLine($"        switch ({instanceName})");
        sb.AppendLine("        {");

        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.AppendLine($"            case {typeName} typedInstance:");
            sb.AppendLine($"                size += {typeName}.GetPacketSize(typedInstance);");
            sb.AppendLine("                break;");
        }

        sb.AppendLine("            case null: break;");
        sb.AppendLine("            default:");
        sb.AppendLine($"                throw new System.IO.InvalidDataException($\"Unknown type for {member.Name}: {{{instanceName}?.GetType().FullName}}\");");
        sb.AppendLine("        }");
    }

    private static void GenerateCollectionSerialization(StringBuilder sb, MemberToGenerate member)
    {
        var collectionInfo = member.CollectionTypeInfo!.Value;
        var countType = member.CollectionInfo?.CountType ?? "int";
        var countWriteMethod = TypeHelper.GetWriteMethodName(countType);

        sb.AppendLine($"        data.{countWriteMethod}(({countType})obj.{member.Name}.{collectionInfo.CountAccessExpression});");

        // Polymorphic collection serialization logic will go here

        sb.AppendLine($"        foreach (var item in obj.{member.Name})");
        sb.AppendLine("        {");
        GenerateElementSerialization(sb, collectionInfo, "item");
        sb.AppendLine("        }");
    }

    private static void GenerateElementSerialization(StringBuilder sb, CollectionTypeInfo elementInfo, string elementAccess)
    {
        if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"            var bytesWritten = {TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Serialize({elementAccess}, data);");
            sb.AppendLine($"            data = data.Slice(bytesWritten);");
        }
        else if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.AppendLine($"            data.Write{typeName}(({typeName}){elementAccess});");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.AppendLine($"            data.WriteString({elementAccess});");
        }
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
}
