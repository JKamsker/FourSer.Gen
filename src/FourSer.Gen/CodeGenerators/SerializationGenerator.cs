using System.Text;
using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
/// Generates Serialize method implementations
/// </summary>
public static class SerializationGenerator
{
    public static void GenerateSerialize(StringBuilder sb, TypeToGenerate typeToGenerate)
    {
        GenerateSpanSerialize(sb, typeToGenerate);
        sb.AppendLine();
        GenerateStreamSerialize(sb, typeToGenerate);
    }

    private static void GenerateSpanSerialize(StringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.AppendLine($"    public static int Serialize({typeToGenerate.Name} obj, System.Span<byte> data)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var originalData = data;");
        GeneratePrePass(sb, typeToGenerate);
        GenerateSerializeBody(sb, typeToGenerate, "data", "SpanWriterHelpers");
        sb.AppendLine("        return originalData.Length - data.Length;");
        sb.AppendLine("    }");
    }

    private static void GenerateStreamSerialize(StringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.AppendLine($"    public static void Serialize({typeToGenerate.Name} obj, System.IO.Stream stream)");
        sb.AppendLine("    {");
        GeneratePrePass(sb, typeToGenerate);
        GenerateSerializeBody(sb, typeToGenerate, "stream", "StreamWriterHelpers");
        sb.AppendLine("    }");
    }

    private static void GeneratePrePass(StringBuilder sb, TypeToGenerate typeToGenerate)
    {
        foreach (var member in typeToGenerate.Members)
        {
            if (member.PolymorphicInfo is not { } info || string.IsNullOrEmpty(info.TypeIdProperty))
            {
                continue;
            }

            if ((member.IsList || member.IsCollection) &&
                member.CollectionInfo?.PolymorphicMode == PolymorphicMode.SingleTypeId)
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
    }

    private static void GenerateSerializeBody(StringBuilder sb, TypeToGenerate typeToGenerate, string target,
        string helper)
    {
        foreach (var member in typeToGenerate.Members)
        {
            GenerateMemberSerialization(sb, member, target, helper);
        }
    }

    private static void GenerateMemberSerialization(StringBuilder sb, MemberToGenerate member, string target, string helper)
    {
        var refOrEmpty = target == "data" ? "ref " : "";

        if (member.IsList || member.IsCollection)
        {
            GenerateCollectionSerialization(sb, member, target, helper);
        }
        else if (member.PolymorphicInfo is not null)
        {
            GeneratePolymorphicSerialization(sb, member, target, helper);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"        if (obj.{member.Name} is null)");
            sb.AppendLine("        {");
            sb.AppendLine($"            throw new System.NullReferenceException($\"Property \\\"{member.Name}\\\" cannot be null.\");");
            sb.AppendLine("        }");
            if (target == "data")
            {
                sb.AppendLine($"        var bytesWritten = {TypeHelper.GetSimpleTypeName(member.TypeName)}.Serialize(obj.{member.Name}, data);");
                sb.AppendLine($"        data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.AppendLine($"        {TypeHelper.GetSimpleTypeName(member.TypeName)}.Serialize(obj.{member.Name}, stream);");
            }
        }
        else if (member.IsStringType)
        {
            sb.AppendLine($"        FourSer.Gen.Helpers.{helper}.WriteString({refOrEmpty}{target}, obj.{member.Name});");
        }
        else if (member.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
            var writeMethod = $"Write{typeName}";
            sb.AppendLine($"        FourSer.Gen.Helpers.{helper}.{writeMethod}({refOrEmpty}{target}, ({typeName})obj.{member.Name});");
        }
    }

    private static void GenerateCollectionSerialization(StringBuilder sb, MemberToGenerate member, string target, string helper)
    {
        if (member.CollectionInfo is not { } collectionInfo) return;
        var refOrEmpty = target == "data" ? "ref " : "";

        sb.AppendLine($"        if (obj.{member.Name} is null)");
        sb.AppendLine("        {");
        if (collectionInfo.CountType != null || !string.IsNullOrEmpty(collectionInfo.CountSizeReference))
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);
            sb.AppendLine($"            FourSer.Gen.Helpers.{helper}.{countWriteMethod}({refOrEmpty}{target}, ({countType})0);");
        }
        else
        {
            sb.AppendLine($"            throw new System.NullReferenceException($\"Collection \\\"{member.Name}\\\" cannot be null.\");");
        }
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");

        if (collectionInfo is { Unlimited: false })
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countWriteMethod = TypeHelper.GetWriteMethodName(countType);

            var countExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
            sb.AppendLine($"            FourSer.Gen.Helpers.{helper}.{countWriteMethod}({refOrEmpty}{target}, ({countType}){countExpression});");
        }

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
            {
                sb.AppendLine($"            foreach(var item in obj.{member.Name})");
                sb.AppendLine("            {");

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
                    null,
                    false
                );

                GeneratePolymorphicItemSerialization(sb, itemMember, "item", target, helper);
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                return;
            }

            if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                var info = member.PolymorphicInfo!.Value;
                var typeIdProperty = collectionInfo.TypeIdProperty;

                sb.AppendLine($"            switch (obj.{typeIdProperty})");
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
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    foreach(var item in obj.{member.Name})");
                    sb.AppendLine("                    {");
                    if (target == "data")
                    {
                        sb.AppendLine($"                        var bytesWritten = {TypeHelper.GetSimpleTypeName(option.Type)}.Serialize(({TypeHelper.GetSimpleTypeName(option.Type)})item, data);");
                        sb.AppendLine($"                        data = data.Slice(bytesWritten);");
                    }
                    else
                    {
                        sb.AppendLine($"                        {TypeHelper.GetSimpleTypeName(option.Type)}.Serialize(({TypeHelper.GetSimpleTypeName(option.Type)})item, stream);");
                    }
                    sb.AppendLine("                    }");
                    sb.AppendLine("                    break;");
                    sb.AppendLine("                }");
                }

                sb.AppendLine("                default:");
                sb.AppendLine($"                    throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{obj.{typeIdProperty}}}\");");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                return;
            }
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (isByteCollection)
        {
            sb.AppendLine($"            FourSer.Gen.Helpers.{helper}.WriteBytes({refOrEmpty}{target}, obj.{member.Name});");
        }
        else
        {
            if (member.CollectionTypeInfo?.IsArray == true || member.IsList)
            {
                var loopCountExpression = GeneratorUtilities.GetCountExpression(member, member.Name);
                sb.AppendLine($"            for (int i = 0; i < {loopCountExpression}; i++)");
                sb.AppendLine("            {");
                if (member.ListTypeArgument is not null)
                {
                    GenerateListElementSerialization(sb, member.ListTypeArgument.Value, $"obj.{member.Name}[i]", target, helper);
                }
                else if (member.CollectionTypeInfo is not null)
                {
                    GenerateCollectionElementSerialization(sb, member.CollectionTypeInfo.Value, $"obj.{member.Name}[i]", target, helper);
                }
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine($"            foreach (var item in obj.{member.Name})");
                sb.AppendLine("            {");
                if (member.CollectionTypeInfo is not null)
                {
                    GenerateCollectionElementSerialization(sb, member.CollectionTypeInfo.Value, "item", target, helper);
                }
                sb.AppendLine("            }");
            }
        }
        sb.AppendLine("        }");
    }

    private static void GeneratePolymorphicSerialization(StringBuilder sb, MemberToGenerate member, string target, string helper)
    {
        var info = member.PolymorphicInfo!.Value;

        sb.AppendLine($"        switch (obj.{member.Name})");
        sb.AppendLine("        {");

        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.AppendLine($"            case {typeName} typedInstance:");
            sb.AppendLine("            {");
            if (string.IsNullOrEmpty(info.TypeIdProperty))
            {
                sb.AppendLine(PolymorphicUtilities.GenerateWriteTypeIdCode(option, info, "                ", target, helper));
            }

            if (target == "data")
            {
                sb.AppendLine($"                var bytesWritten = {typeName}.Serialize(typedInstance, data);");
                sb.AppendLine($"                data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.AppendLine($"                {typeName}.Serialize(typedInstance, stream);");
            }
            sb.AppendLine("                break;");
            sb.AppendLine("            }");
        }

        sb.AppendLine("            case null:");
        sb.AppendLine($"                throw new System.NullReferenceException($\"Property \\\"{member.Name}\\\" cannot be null.\");");
        sb.AppendLine("            default:");
        sb.AppendLine($"                throw new System.IO.InvalidDataException($\"Unknown type for {member.Name}: {{obj.{member.Name}?.GetType().FullName}}\");");
        sb.AppendLine("        }");
    }

    private static void GeneratePolymorphicItemSerialization(StringBuilder sb, MemberToGenerate member, string instanceName, string target, string helper)
    {
        var info = member.PolymorphicInfo!.Value;

        sb.AppendLine($"            switch ({instanceName})");
        sb.AppendLine("            {");

        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
            sb.AppendLine($"                case {typeName} typedInstance:");
            sb.AppendLine("                {");

            if (string.IsNullOrEmpty(info.TypeIdProperty))
            {
                sb.AppendLine(PolymorphicUtilities.GenerateWriteTypeIdCode(option, info, "                    ", target, helper));
            }

            if (target == "data")
            {
                sb.AppendLine($"                    var bytesWritten = {typeName}.Serialize(typedInstance, data);");
                sb.AppendLine($"                    data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.AppendLine($"                    {typeName}.Serialize(typedInstance, stream);");
            }
            sb.AppendLine("                    break;");
            sb.AppendLine("                }");
        }

        sb.AppendLine("                case null:");
        sb.AppendLine($"                    throw new System.NullReferenceException($\"Item in collection cannot be null.\");");
        sb.AppendLine("                default:");
        sb.AppendLine($"                    throw new System.IO.InvalidDataException($\"Unknown type for {instanceName}: {{{instanceName}?.GetType().FullName}}\");");
        sb.AppendLine("            }");
    }

    private static void GenerateListElementSerialization(StringBuilder sb, ListTypeArgumentInfo elementInfo, string elementAccess, string target, string helper)
    {
        var refOrEmpty = target == "data" ? "ref " : "";
        if (elementInfo.HasGenerateSerializerAttribute)
        {
            if (target == "data")
            {
                sb.AppendLine($"            var bytesWritten = {TypeHelper.GetSimpleTypeName(elementInfo.TypeName)}.Serialize({elementAccess}, data);");
                sb.AppendLine($"            data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.AppendLine($"            {TypeHelper.GetSimpleTypeName(elementInfo.TypeName)}.Serialize({elementAccess}, stream);");
            }
        }
        else if (elementInfo.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.TypeName);
            sb.AppendLine($"            FourSer.Gen.Helpers.{helper}.Write{typeName}({refOrEmpty}{target}, ({typeName}){elementAccess});");
        }
        else if (elementInfo.IsStringType)
        {
            sb.AppendLine($"            FourSer.Gen.Helpers.{helper}.WriteString({refOrEmpty}{target}, {elementAccess});");
        }
    }

    private static void GenerateCollectionElementSerialization(StringBuilder sb, CollectionTypeInfo elementInfo, string elementAccess, string target, string helper)
    {
        var refOrEmpty = target == "data" ? "ref " : "";
        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.AppendLine($"            FourSer.Gen.Helpers.{helper}.Write{typeName}({refOrEmpty}{target}, ({typeName}){elementAccess});");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.AppendLine($"            FourSer.Gen.Helpers.{helper}.WriteString({refOrEmpty}{target}, {elementAccess});");
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            if (target == "data")
            {
                sb.AppendLine($"            var bytesWritten = {TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Serialize({elementAccess}, data);");
                sb.AppendLine($"            data = data.Slice(bytesWritten);");
            }
            else
            {
                sb.AppendLine($"            {TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Serialize({elementAccess}, stream);");
            }
        }
    }
}
