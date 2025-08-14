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
        var countVar = $"{member.Name}Count";
        var collectionVar = $"{member.Name}Collection";

        sb.AppendLine($"        var {countVar} = data.{countReadMethod}();");

        if (ShouldUsePolymorphicSerialization(member))
        {
            GeneratePolymorphicCollectionDeserialization(sb, member, countVar, collectionVar);
            return;
        }

        var instantiation = collectionInfo.IsArray
            ? collectionInfo.ConcreteTypeInstantiation.Replace("count", countVar)
            : collectionInfo.ConcreteTypeInstantiation;

        sb.AppendLine($"        var {collectionVar} = {instantiation};");
        sb.AppendLine($"        for (int i = 0; i < {countVar}; i++)");
        sb.AppendLine("        {");

        if (collectionInfo.IsArray)
        {
            GenerateArrayElementDeserialization(sb, collectionInfo, collectionVar, "i");
        }
        else
        {
            GenerateElementDeserialization(sb, collectionInfo, collectionVar);
        }

        sb.AppendLine("        }");
        sb.AppendLine($"        obj.{member.Name} = {collectionVar};");
    }

    private static void GeneratePolymorphicCollectionDeserialization(StringBuilder sb, MemberToGenerate member, string countVar, string collectionVar)
    {
        var info = member.PolymorphicInfo!.Value;
        var typeIdProperty = info.TypeIdProperty;
        var bytesReadVar = $"{member.Name}BytesRead";

        sb.AppendLine($"        var {collectionVar} = new System.Collections.Generic.List<{member.CollectionTypeInfo.Value.ElementTypeName}>();");
        sb.AppendLine($"        for (int i = 0; i < {countVar}; i++)");
        sb.AppendLine("        {");
        sb.AppendLine($"            int {bytesReadVar};");
        sb.AppendLine($"            {member.CollectionTypeInfo.Value.ElementTypeName} item;");

        string typeIdVar = $"obj.{typeIdProperty}";
        if (string.IsNullOrEmpty(typeIdProperty))
        {
            var typeIdTypeName = GetMethodFriendlyTypeName(info.EnumUnderlyingType ?? info.TypeIdType);
            sb.AppendLine($"            var typeId = data.Read{typeIdTypeName}();");
            typeIdVar = "typeId";
        }

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
            sb.AppendLine($"                    item = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize(data, out {bytesReadVar});");
            sb.AppendLine($"                    data = data.Slice({bytesReadVar});");
            sb.AppendLine("                    break;");
        }

        sb.AppendLine("                default:");
        sb.AppendLine($"                    throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{typeIdVar}}}\");");
        sb.AppendLine("            }");
        sb.AppendLine($"            {collectionVar}.Add(item);");
        sb.AppendLine("        }");
        sb.AppendLine($"        obj.{member.Name} = {collectionVar};");
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

    private static void GenerateElementDeserialization(StringBuilder sb, CollectionTypeInfo elementInfo, string collectionVar)
    {
        var readExpression = GetReadExpression(elementInfo);
        if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"            var item = {readExpression};");
            sb.AppendLine($"            data = data.Slice(itemBytesRead);");
            sb.AppendLine($"            {collectionVar}.{elementInfo.AddMethodName}(item);");
        }
        else
        {
            sb.AppendLine($"            {collectionVar}.{elementInfo.AddMethodName}({readExpression});");
        }
    }

    private static void GenerateArrayElementDeserialization(StringBuilder sb, CollectionTypeInfo elementInfo, string collectionVar, string indexVar)
    {
        var readExpression = GetReadExpression(elementInfo);
        if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"            var item = {readExpression};");
            sb.AppendLine($"            data = data.Slice(itemBytesRead);");
            sb.AppendLine($"            {collectionVar}[{indexVar}] = item;");
        }
        else
        {
            sb.AppendLine($"            {collectionVar}[{indexVar}] = {readExpression};");
        }
    }

    private static string GetReadExpression(CollectionTypeInfo elementInfo)
    {
        if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            return $"{TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize(data, out var itemBytesRead)";
        }
        if (elementInfo.IsElementStringType)
        {
            return "data.ReadString()";
        }
        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            return $"data.Read{typeName}()";
        }
        return ""; // Should not happen
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

        if (ShouldUsePolymorphicSerialization(member))
        {
            sb.AppendLine($"        foreach(var item in obj.{member.Name})");
            sb.AppendLine("        {");
            var info = member.PolymorphicInfo.Value;
            if (string.IsNullOrEmpty(info.TypeIdProperty))
            {
                var typeIdSize = info.EnumUnderlyingType is not null
                    ? $"sizeof({info.EnumUnderlyingType})"
                    : $"sizeof({info.TypeIdType})";
                sb.AppendLine($"            size += {typeIdSize};");
            }
            GeneratePolymorphicSizeCalculation(sb, member, "item");
            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine($"        foreach(var item in obj.{member.Name})");
            sb.AppendLine("        {");
            GenerateElementSizeCalculation(sb, collectionInfo, "item");
            sb.AppendLine("        }");
        }
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
        var countExpression = $"obj.{member.Name}.{collectionInfo.CountAccessExpression}";
        if(collectionInfo.CountAccessExpression == "Count()")
        {
            countExpression = $"obj.{member.Name}.Count()";
        }

        sb.AppendLine($"        data.{countWriteMethod}(({countType}){countExpression});");

        if (ShouldUsePolymorphicSerialization(member))
        {
            var itemBytesWrittenVar = $"{member.Name}ItemBytesWritten";
            sb.AppendLine($"        int {itemBytesWrittenVar};");
            sb.AppendLine($"        foreach(var item in obj.{member.Name})");
            sb.AppendLine("        {");
            GeneratePolymorphicItemSerialization(sb, member, "item", itemBytesWrittenVar);
            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine($"        foreach (var item in obj.{member.Name})");
            sb.AppendLine("        {");
            GenerateElementSerialization(sb, collectionInfo, "item");
            sb.AppendLine("        }");
        }
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
            sb.AppendLine($"            data.Write{typeName}(({elementInfo.ElementTypeName}){elementAccess});");
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
