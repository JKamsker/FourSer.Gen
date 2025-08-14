using System.Text;
using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

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
            sb.AppendLine($"        obj.{member.Name} = FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadString(ref data);");
        }
        else if (member.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
            var readMethod = $"Read{typeName}";
            sb.AppendLine($"        obj.{member.Name} = FourSer.Gen.Helpers.RoSpanReaderHelpers.{readMethod}(ref data);");
        }
    }

    private static void GenerateCollectionDeserialization(StringBuilder sb, MemberToGenerate member)
    {
        if (member.CollectionInfo is null) return;

        // Determine the count type to use
        var countType = member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
        var countReadMethod = TypeHelper.GetReadMethodName(countType);
        var countVar = $"{member.Name}Count";

        sb.AppendLine($"        var {countVar} = FourSer.Gen.Helpers.RoSpanReaderHelpers.{countReadMethod}(ref data);");

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (isByteCollection)
        {
            // Use bulk operations for byte collections, which is much more efficient.
            if (member.CollectionTypeInfo?.IsArray == true)
            {
                // If the target is already a byte array, read directly into it.
                sb.AppendLine($"        obj.{member.Name} = new byte[{countVar}];");
                sb.AppendLine($"        FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadBytes(ref data, obj.{member.Name});");
            }
            else if (member.CollectionTypeInfo?.CollectionTypeName == "System.Collections.Generic.List<T>")
            {
                sb.AppendLine($"        obj.{member.Name} = FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadBytes(ref data, {countVar}).ToList();");
            }
            else
            {
                // For any other collection type (IEnumerable<byte>, List<byte>, etc.),
                // creating a byte[] is the most efficient concrete type.
                sb.AppendLine($"        obj.{member.Name} = FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadBytes(ref data, {countVar});");
            }
            return; // We're done with this member.
        }

        // For non-byte collections, we instantiate them upfront with capacity if possible.
        sb.AppendLine($"        {CollectionUtilities.GenerateCollectionInstantiation(member, countVar)}");

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            if (member.CollectionInfo.Value.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
            {
                sb.AppendLine($"        for (int i = 0; i < {countVar}; i++)");
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
                    sb.AppendLine($"                for (int i = 0; i < {countVar}; i++)");
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

        sb.AppendLine($"        for (int i = 0; i < {countVar}; i++)");
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
        var assignment = CollectionUtilities.GenerateCollectionAssignment(member, $"temp{member.Name}");
        if (!string.IsNullOrEmpty(assignment))
        {
            sb.AppendLine($"        {assignment}");
        }
    }

    private static void GeneratePolymorphicDeserialization(StringBuilder sb, MemberToGenerate member)
    {
        var info = member.PolymorphicInfo!.Value;
        var bytesReadVar = $"{member.Name}BytesRead";

        sb.AppendLine($"        int {bytesReadVar};");
        sb.AppendLine($"        obj.{member.Name} = default;");

        var switchVar = PolymorphicUtilities.GenerateTypeIdVariable(sb, info, info.TypeIdProperty, isDeserialization: true);

        PolymorphicUtilities.GeneratePolymorphicSwitch(sb, info, switchVar,
            caseHandler: (option, key) =>
            {
                sb.AppendLine($"                obj.{member.Name} = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize(data, out {bytesReadVar});");
                sb.AppendLine($"                data = data.Slice({bytesReadVar});");
                sb.AppendLine("                break;");
            },
            defaultCaseHandler: () =>
            {
                sb.AppendLine($"                throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{switchVar}}}\");");
            },
            indent: "        ");
    }

    private static void GeneratePolymorphicItemDeserialization(StringBuilder sb, MemberToGenerate member, string assignmentTarget)
    {
        var info = member.PolymorphicInfo!.Value;
        var bytesReadVar = $"{assignmentTarget}BytesRead";

        sb.AppendLine($"            int {bytesReadVar};");

        var switchVar = PolymorphicUtilities.GenerateTypeIdVariable(sb, info, null, isDeserialization: true, indent: "            ");

        PolymorphicUtilities.GeneratePolymorphicSwitch(sb, info, switchVar,
            caseHandler: (option, key) =>
            {
                sb.AppendLine($"                    {assignmentTarget} = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize(data, out {bytesReadVar});");
                sb.AppendLine($"                    data = data.Slice({bytesReadVar});");
                sb.AppendLine("                    break;");
            },
            defaultCaseHandler: () =>
            {
                sb.AppendLine($"                    throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{switchVar}}}\");");
            },
            indent: "            ");
    }

    private static void GenerateArrayElementDeserialization(StringBuilder sb, CollectionTypeInfo elementInfo, string arrayName, string indexVar)
    {
        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.AppendLine($"            obj.{arrayName}[{indexVar}] = FourSer.Gen.Helpers.RoSpanReaderHelpers.Read{typeName}(ref data);");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.AppendLine($"            obj.{arrayName}[{indexVar}] = FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadString(ref data);");
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"            obj.{arrayName}[{indexVar}] = {TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize(data, out var itemBytesRead);");
            sb.AppendLine($"            data = data.Slice(itemBytesRead);");
        }
    }

    private static void GenerateListElementDeserialization(StringBuilder sb, ListTypeArgumentInfo elementInfo, string collectionTarget)
    {
        if (elementInfo.HasGenerateSerializerAttribute)
        {
            sb.AppendLine($"            {collectionTarget}.Add({TypeHelper.GetSimpleTypeName(elementInfo.TypeName)}.Deserialize(data, out var itemBytesRead));");
            sb.AppendLine($"            data = data.Slice(itemBytesRead);");
        }
        else if (elementInfo.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.TypeName);
            sb.AppendLine($"            {collectionTarget}.Add(FourSer.Gen.Helpers.RoSpanReaderHelpers.Read{typeName}(ref data));");
        }
        else if (elementInfo.IsStringType)
        {
            sb.AppendLine($"            {collectionTarget}.Add(FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadString(ref data));");
        }
    }

    private static void GenerateCollectionElementDeserialization(StringBuilder sb, CollectionTypeInfo elementInfo, string collectionTarget)
    {
        var addMethod = CollectionUtilities.GetCollectionAddMethod(elementInfo.CollectionTypeName);

        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.AppendLine($"            {collectionTarget}.{addMethod}(FourSer.Gen.Helpers.RoSpanReaderHelpers.Read{typeName}(ref data));");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.AppendLine($"            {collectionTarget}.{addMethod}(FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadString(ref data));");
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.AppendLine($"            {collectionTarget}.{addMethod}({TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize(data, out var itemBytesRead));");
            sb.AppendLine($"            data = data.Slice(itemBytesRead);");
        }
    }
}
