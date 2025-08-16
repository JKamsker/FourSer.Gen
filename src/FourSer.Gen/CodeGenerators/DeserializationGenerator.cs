using System.Collections.Generic;
using System.Linq;
using System.Text;
using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using System;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
/// Generates Deserialize method implementations
/// </summary>
public static class DeserializationGenerator
{
    public static void GenerateDeserialize(StringBuilder sb, TypeToGenerate typeToGenerate)
    {
        var newKeyword = typeToGenerate.HasSerializableBaseType ? "new " : "";
        sb.AppendLine($"    public static {newKeyword}{typeToGenerate.Name} Deserialize(System.ReadOnlySpan<byte> buffer, out int bytesRead)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var originalBuffer = buffer;");

        // Deserialize all members into local variables first
        foreach (var member in typeToGenerate.Members)
        {
            GenerateMemberDeserialization(sb, member, true);
        }

        if (typeToGenerate.Constructor is { } ctor)
        {
            var ctorArgs = string.Join(", ", ctor.Parameters.Select(p => StringExtensions.ToCamelCase(p.Name)));
            sb.AppendLine($"        var obj = new {typeToGenerate.Name}({ctorArgs});");

            var membersInCtor = new HashSet<string>(ctor.Parameters.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            var membersNotInCtor = typeToGenerate.Members.Where(m => !membersInCtor.Contains(m.Name));

            foreach (var member in membersNotInCtor)
            {
                var camelCaseName = StringExtensions.ToCamelCase(member.Name);
                sb.AppendLine($"        obj.{member.Name} = {camelCaseName};");
            }
        }
        else // Fallback for when there is no constructor info. Should not happen.
        {
            sb.AppendLine($"        var obj = new {typeToGenerate.Name}();");
            foreach (var member in typeToGenerate.Members)
            {
                var camelCaseName = StringExtensions.ToCamelCase(member.Name);
                sb.AppendLine($"        obj.{member.Name} = {camelCaseName};");
            }
        }

        sb.AppendLine("        bytesRead = originalBuffer.Length - buffer.Length;");
        sb.AppendLine("        return obj;");
        sb.AppendLine("    }");
    }

    private static void GenerateMemberDeserialization(StringBuilder sb, MemberToGenerate member, bool isCtorParam)
    {
        var target = isCtorParam ? $"var {StringExtensions.ToCamelCase(member.Name)}" : $"obj.{member.Name}";

        if (member.IsList || member.IsCollection)
        {
            GenerateCollectionDeserialization(sb, member, target);
        }
        else if (member.PolymorphicInfo is not null)
        {
            GeneratePolymorphicDeserialization(sb, member);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"        {target} = {TypeHelper.GetSimpleTypeName(member.TypeName)}.Deserialize(buffer, out var nestedBytesRead);");
            sb.AppendLine($"        buffer = buffer.Slice(nestedBytesRead);");
        }
        else if (member.IsStringType)
        {
            sb.AppendLine($"        {target} = FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadString(ref buffer);");
        }
        else if (member.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
            var readMethod = $"Read{typeName}";
            sb.AppendLine($"        {target} = FourSer.Gen.Helpers.RoSpanReaderHelpers.{readMethod}(ref buffer);");
        }
    }

    private static void GenerateCollectionDeserialization(StringBuilder sb, MemberToGenerate member, string target)
    {
        if (member.CollectionInfo is null)
            return;

        if (member.CollectionInfo.Value.Unlimited)
        {
            GenerateUnlimitedCollectionDeserialization(sb, member, target);
            return;
        }

        var memberName = StringExtensions.ToCamelCase(member.Name);
        string countVar;

        // Determine the count type to use
        var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
        var countReadMethod = TypeHelper.GetReadMethodName(countType);

        if (member.CollectionInfo.Value.CountSizeReference is string countSizeReference)
        {
            countVar = StringExtensions.ToCamelCase(countSizeReference);
        }
        else
        {
            countVar = $"{memberName}Count";
            sb.AppendLine($"        var {countVar} = FourSer.Gen.Helpers.RoSpanReaderHelpers.{countReadMethod}(ref buffer);");
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (isByteCollection)
        {
            // Use bulk operations for byte collections, which is much more efficient.
            if (member.CollectionTypeInfo?.IsArray == true)
            {
                sb.AppendLine($"        {target} = new byte[(int){countVar}];");
                sb.AppendLine($"        FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadBytes(ref buffer, {memberName});");
            }
            else if (member.CollectionTypeInfo?.CollectionTypeName == "System.Collections.Generic.List<T>")
            {
                sb.AppendLine($"        {target} = FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadBytes(ref buffer, (int){countVar}).ToList();");
            }
            else
            {
                sb.AppendLine($"        {target} = FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadBytes(ref buffer, (int){countVar});");
            }
            return;
        }

        // For non-byte collections, we instantiate them upfront with capacity if possible.
        var capacityVar = countType is "uint" or "ulong" ? $"(int){countVar}" : countVar;
        sb.AppendLine($"        {CollectionUtilities.GenerateCollectionInstantiation(member, capacityVar, target)}");

        var loopLimitVar = countType is "ulong" or "uint" ? $"(int){countVar}" : countVar;

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            if (member.CollectionInfo.Value.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
            {
                sb.AppendLine($"        for (int i = 0; i < {loopLimitVar}; i++)");
                sb.AppendLine("        {");
                sb.AppendLine($"            {member.ListTypeArgument!.Value.TypeName} item;");
                var itemMember = new MemberToGenerate(
                    "item",
                    member.ListTypeArgument!.Value.TypeName,
                    member.ListTypeArgument.Value.IsUnmanagedType,
                    member.ListTypeArgument.Value.IsStringType,
                    member.ListTypeArgument.Value.HasGenerateSerializerAttribute,
                    false, null, null, member.PolymorphicInfo, false, null, false
                );
                GeneratePolymorphicItemDeserialization(sb, itemMember, "item");
                sb.AppendLine($"            {memberName}.Add(item);");
                sb.AppendLine("        }");
                return;
            }

            if (member.CollectionInfo.Value.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                var info = member.PolymorphicInfo!.Value;
                var typeIdProperty = member.CollectionInfo.Value.TypeIdProperty;
                var typeIdVar = StringExtensions.ToCamelCase(typeIdProperty!);

                sb.AppendLine($"        switch ({typeIdVar})");
                sb.AppendLine("        {");

                foreach (var option in info.Options)
                {
                    var key = option.Key.ToString();
                    if (info.EnumUnderlyingType is not null) { key = $"({info.TypeIdType}){key}"; }
                    else if (info.TypeIdType.EndsWith("Enum"))
                        key = $"{info.TypeIdType}.{key}";

                    sb.AppendLine($"            case {key}:");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                for (int i = 0; i < {loopLimitVar}; i++)");
                    sb.AppendLine("                {");
                    sb.AppendLine($"                    var item = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize(buffer, out var itemBytesRead);");
                    sb.AppendLine($"                    {memberName}.Add(item);");
                    sb.AppendLine($"                    buffer = buffer.Slice(itemBytesRead);");
                    sb.AppendLine("                }");
                    sb.AppendLine("                break;");
                    sb.AppendLine("            }");
                }

                sb.AppendLine("            default:");
                sb.AppendLine($"                throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{typeIdVar}}}\");");
                sb.AppendLine("        }");
                return;
            }
        }

        sb.AppendLine($"        for (int i = 0; i < {loopLimitVar}; i++)");
        sb.AppendLine("        {");

        if (member.CollectionTypeInfo?.IsArray == true)
        {
            GenerateArrayElementDeserialization(sb, member.CollectionTypeInfo.Value, memberName, "i");
        }
        else if (member.ListTypeArgument is not null)
        {
            GenerateListElementDeserialization(sb, member.ListTypeArgument.Value, memberName);
        }
        else if (member.CollectionTypeInfo is not null)
        {
            GenerateCollectionElementDeserialization(sb, member.CollectionTypeInfo.Value, memberName);
        }

        sb.AppendLine("        }");
    }

    private static void GenerateUnlimitedCollectionDeserialization(StringBuilder sb, MemberToGenerate member, string target)
    {
        var elementType = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var tempCollectionVar = $"temp{member.Name}";

        sb.AppendLine($"        var {tempCollectionVar} = new System.Collections.Generic.List<{elementType}>();");

        sb.AppendLine("        while (buffer.Length > 0)");
        sb.AppendLine("        {");

        var listTypeInfo = member.ListTypeArgument ?? new ListTypeArgumentInfo(
            elementType,
            member.CollectionTypeInfo.Value.IsElementUnmanagedType,
            member.CollectionTypeInfo.Value.IsElementStringType,
            member.CollectionTypeInfo.Value.HasElementGenerateSerializerAttribute
        );
        GenerateListElementDeserialization(sb, listTypeInfo, tempCollectionVar);

        sb.AppendLine("        }");

        if (member.CollectionTypeInfo?.IsArray == true)
        {
            sb.AppendLine($"        {target} = {tempCollectionVar}.ToArray();");
        }
        else
        {
            sb.AppendLine($"        {target} = {tempCollectionVar};");
        }
    }

    private static void GeneratePolymorphicDeserialization(StringBuilder sb, MemberToGenerate member)
    {
        var info = member.PolymorphicInfo!.Value;
        var memberName = StringExtensions.ToCamelCase(member.Name);
        var bytesReadVar = $"{memberName}BytesRead";

        sb.AppendLine($"        int {bytesReadVar};");
        sb.AppendLine($"        {member.TypeName} {memberName} = default;");

        string switchVar;
        if (info.TypeIdProperty is not null)
        {
            switchVar = StringExtensions.ToCamelCase(info.TypeIdProperty);
        }
        else
        {
            switchVar = "typeId";
            var typeToRead = info.EnumUnderlyingType ?? info.TypeIdType;
            var typeIdReadMethod = TypeHelper.GetReadMethodName(typeToRead);
            var cast = info.EnumUnderlyingType is not null ? $"({info.TypeIdType})" : "";
            sb.AppendLine($"        var {switchVar} = {cast}FourSer.Gen.Helpers.RoSpanReaderHelpers.{typeIdReadMethod}(ref buffer);");
        }

        sb.AppendLine($"        switch ({switchVar})");
        sb.AppendLine("        {");

        foreach (var option in info.Options)
        {
            var key = option.Key.ToString();
            if (info.EnumUnderlyingType is not null) { key = $"({info.TypeIdType}){key}"; }
            else if (info.TypeIdType.EndsWith("Enum")) { key = $"{info.TypeIdType}.{key}"; }

            sb.AppendLine($"            case {key}:");
            sb.AppendLine("            {");
            sb.AppendLine($"                {memberName} = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize(buffer, out {bytesReadVar});");
            sb.AppendLine($"                buffer = buffer.Slice({bytesReadVar});");
            sb.AppendLine("                break;");
            sb.AppendLine("            }");
        }

        sb.AppendLine("            default:");
        sb.AppendLine($"                throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{switchVar}}}\");");
        sb.AppendLine("        }");
    }

    private static void GeneratePolymorphicItemDeserialization(StringBuilder sb, MemberToGenerate member, string assignmentTarget)
    {
        var info = member.PolymorphicInfo!.Value;
        var bytesReadVar = $"{assignmentTarget}BytesRead";

        sb.AppendLine($"            int {bytesReadVar};");

        var switchVar = "typeId";
        var typeToRead = info.EnumUnderlyingType ?? info.TypeIdType;
        var typeIdReadMethod = TypeHelper.GetReadMethodName(typeToRead);
        var cast = info.EnumUnderlyingType is not null ? $"({info.TypeIdType})" : "";
        sb.AppendLine($"            var {switchVar} = {cast}FourSer.Gen.Helpers.RoSpanReaderHelpers.{typeIdReadMethod}(ref buffer);");

        sb.AppendLine($"            switch ({switchVar})");
        sb.AppendLine("            {");

        foreach (var option in info.Options)
        {
            var key = option.Key.ToString();
            if (info.EnumUnderlyingType is not null) { key = $"({info.TypeIdType}){key}"; }
            else if (info.TypeIdType.EndsWith("Enum")) { key = $"{info.TypeIdType}.{key}"; }

            sb.AppendLine($"                case {key}:");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {assignmentTarget} = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize(buffer, out {bytesReadVar});");
            sb.AppendLine($"                    buffer = buffer.Slice({bytesReadVar});");
            sb.AppendLine("                    break;");
            sb.AppendLine("                }");
        }

        sb.AppendLine("                default:");
        sb.AppendLine($"                    throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{switchVar}}}\");");
        sb.AppendLine("            }");
    }

    private static void GenerateArrayElementDeserialization(StringBuilder sb, CollectionTypeInfo elementInfo, string arrayName, string indexVar)
    {
        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.AppendLine($"            {arrayName}[{indexVar}] = FourSer.Gen.Helpers.RoSpanReaderHelpers.Read{typeName}(ref buffer);");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.AppendLine($"            {arrayName}[{indexVar}] = FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadString(ref buffer);");
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"            {arrayName}[{indexVar}] = {TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize(buffer, out var itemBytesRead);");
            sb.AppendLine($"            buffer = buffer.Slice(itemBytesRead);");
        }
    }

    private static void GenerateListElementDeserialization(StringBuilder sb, ListTypeArgumentInfo elementInfo, string collectionTarget)
    {
        if (elementInfo.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"            {collectionTarget}.Add({TypeHelper.GetSimpleTypeName(elementInfo.TypeName)}.Deserialize(buffer, out var itemBytesRead));");
            sb.AppendLine($"            buffer = buffer.Slice(itemBytesRead);");
        }
        else if (elementInfo.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.TypeName);
            sb.AppendLine($"            {collectionTarget}.Add(FourSer.Gen.Helpers.RoSpanReaderHelpers.Read{typeName}(ref buffer));");
        }
        else if (elementInfo.IsStringType)
        {
            sb.AppendLine($"            {collectionTarget}.Add(FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadString(ref buffer));");
        }
    }

    private static void GenerateCollectionElementDeserialization(StringBuilder sb, CollectionTypeInfo elementInfo, string collectionTarget)
    {
        var addMethod = CollectionUtilities.GetCollectionAddMethod(elementInfo.CollectionTypeName);

        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.AppendLine($"            {collectionTarget}.{addMethod}(FourSer.Gen.Helpers.RoSpanReaderHelpers.Read{typeName}(ref buffer));");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.AppendLine($"            {collectionTarget}.{addMethod}(FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadString(ref buffer));");
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"            {collectionTarget}.{addMethod}({TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize(buffer, out var itemBytesRead));");
            sb.AppendLine($"            buffer = buffer.Slice(itemBytesRead);");
        }
    }
}
