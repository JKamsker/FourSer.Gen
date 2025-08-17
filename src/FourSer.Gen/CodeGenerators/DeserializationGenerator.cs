using System;
using System.Collections.Generic;
using System.Linq;
using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
///     Generates Deserialize method implementations
/// </summary>
public static class DeserializationGenerator
{
    public static void GenerateDeserialize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        GenerateDeserializeWithRef(sb, typeToGenerate);
        sb.WriteLine();
        GenerateDeserializeWithSpan(sb, typeToGenerate);
        sb.WriteLine();
        GenerateDeserializeWithStream(sb, typeToGenerate);
    }

    private static void GenerateDeserializeWithSpan(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        var newKeyword = typeToGenerate.HasSerializableBaseType ? "new " : "";
        sb.WriteLine($"public static {newKeyword}{typeToGenerate.Name} Deserialize(System.ReadOnlySpan<byte> buffer)");
        using var _ = sb.BeginBlock();
        sb.WriteLine("return Deserialize(ref buffer);");
    }

    private static void GenerateDeserializeWithStream(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        var newKeyword = typeToGenerate.HasSerializableBaseType ? "new " : "";
        sb.WriteLineFormat("public static {0}{1} Deserialize(System.IO.Stream stream)", newKeyword, typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        foreach (var member in typeToGenerate.Members)
        {
            GenerateMemberDeserialization
            (
                sb,
                member,
                true,
                "stream",
                "StreamReaderHelpers"
            );
        }

        if (typeToGenerate.Constructor is { } ctor)
        {
            var ctorArgs = string.Join(", ", ctor.Parameters.Select(p => p.Name.ToCamelCase()));
            sb.WriteLine($"var obj = new {typeToGenerate.Name}({ctorArgs});");

            var membersInCtor = new HashSet<string>(ctor.Parameters.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            var membersNotInCtor = typeToGenerate.Members.Where(m => !membersInCtor.Contains(m.Name));

            foreach (var member in membersNotInCtor)
            {
                var camelCaseName = member.Name.ToCamelCase();
                sb.WriteLine($"obj.{member.Name} = {camelCaseName};");
            }
        }
        else
        {
            sb.WriteLine($"var obj = new {typeToGenerate.Name}();");
            foreach (var member in typeToGenerate.Members)
            {
                var camelCaseName = member.Name.ToCamelCase();
                sb.WriteLine($"obj.{member.Name} = {camelCaseName};");
            }
        }

        sb.WriteLine("return obj;");
    }

    private static void GenerateDeserializeWithRef(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        var newKeyword = typeToGenerate.HasSerializableBaseType ? "new " : "";
        sb.WriteLine($"public static {newKeyword}{typeToGenerate.Name} Deserialize(ref System.ReadOnlySpan<byte> buffer)");
        using var _ = sb.BeginBlock();
        foreach (var member in typeToGenerate.Members)
        {
            GenerateMemberDeserialization
            (
                sb,
                member,
                true,
                "buffer",
                "RoSpanReaderHelpers"
            );
        }

        if (typeToGenerate.Constructor is { } ctor)
        {
            var ctorArgs = string.Join(", ", ctor.Parameters.Select(p => p.Name.ToCamelCase()));
            sb.WriteLine($"var obj = new {typeToGenerate.Name}({ctorArgs});");

            var membersInCtor = new HashSet<string>(ctor.Parameters.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            var membersNotInCtor = typeToGenerate.Members.Where(m => !membersInCtor.Contains(m.Name));

            foreach (var member in membersNotInCtor)
            {
                var camelCaseName = member.Name.ToCamelCase();
                sb.WriteLine($"obj.{member.Name} = {camelCaseName};");
            }
        }
        else
        {
            sb.WriteLine($"var obj = new {typeToGenerate.Name}();");
            foreach (var member in typeToGenerate.Members)
            {
                var camelCaseName = member.Name.ToCamelCase();
                sb.WriteLine($"obj.{member.Name} = {camelCaseName};");
            }
        }

        sb.WriteLine("return obj;");
    }


    private static void GenerateMemberDeserialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        bool isCtorParam,
        string source,
        string helper
    )
    {
        var target = isCtorParam ? $"var {member.Name.ToCamelCase()}" : $"obj.{member.Name}";
        var refOrEmpty = source == "buffer" ? "ref " : "";

        if (member.IsList || member.IsCollection)
        {
            GenerateCollectionDeserialization
            (
                sb,
                member,
                target,
                source,
                helper
            );
        }
        else if (member.PolymorphicInfo is not null)
        {
            GeneratePolymorphicDeserialization(sb, member, source, helper);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.WriteLine($"{target} = {TypeHelper.GetSimpleTypeName(member.TypeName)}.Deserialize({refOrEmpty}{source});");
        }
        else if (member.IsStringType)
        {
            sb.WriteLine($"{target} = FourSer.Gen.Helpers.{helper}.ReadString({refOrEmpty}{source});");
        }
        else if (member.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
            var readMethod = $"Read{typeName}";
            sb.WriteLine($"{target} = FourSer.Gen.Helpers.{helper}.{readMethod}({refOrEmpty}{source});");
        }
    }

    private static void GenerateCollectionDeserialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string target,
        string source,
        string helper
    )
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        var refOrEmpty = source == "buffer" ? "ref " : "";

        if (collectionInfo.Unlimited)
        {
            GenerateUnlimitedCollectionDeserialization
            (
                sb,
                member,
                target,
                source,
                helper
            );
            return;
        }

        var memberName = member.Name.ToCamelCase();
        string countVar;

        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        var countReadMethod = TypeHelper.GetReadMethodName(countType);

        if (collectionInfo.CountSizeReference is string countSizeReference)
        {
            countVar = countSizeReference.ToCamelCase();
        }
        else
        {
            countVar = $"{memberName}Count";
            sb.WriteLine($"var {countVar} = FourSer.Gen.Helpers.{helper}.{countReadMethod}({refOrEmpty}{source});");
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (isByteCollection)
        {
            if (member.CollectionTypeInfo?.IsArray == true)
            {
                sb.WriteLine($"{target} = FourSer.Gen.Helpers.{helper}.ReadBytes({refOrEmpty}{source}, (int){countVar});");
            }
            else if (member.CollectionTypeInfo?.CollectionTypeName == "System.Collections.Generic.List<T>")
            {
                sb.WriteLine
                    ($"{target} = FourSer.Gen.Helpers.{helper}.ReadBytes({refOrEmpty}{source}, (int){countVar}).ToList();");
            }
            else
            {
                sb.WriteLine($"{target} = FourSer.Gen.Helpers.{helper}.ReadBytes({refOrEmpty}{source}, (int){countVar});");
            }

            return;
        }

        var capacityVar = countType is "uint" or "ulong" ? $"(int){countVar}" : countVar;
        sb.WriteLine($"{CollectionUtilities.GenerateCollectionInstantiation(member, capacityVar, target)}");

        var loopLimitVar = countType is "ulong" or "uint" ? $"(int){countVar}" : countVar;

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
            {
                sb.WriteLine($"for (int i = 0; i < {loopLimitVar}; i++)");
                using var _ = sb.BeginBlock();
                sb.WriteLine($"{member.ListTypeArgument!.Value.TypeName} item;");
                var itemMember = new MemberToGenerate
                (
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
                    false,
                    false,
                    LocationInfo.None
                );
                GeneratePolymorphicItemDeserialization
                (
                    sb,
                    itemMember,
                    "item",
                    source,
                    helper
                );
                sb.WriteLine($"{memberName}.Add(item);");
                return;
            }

            if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                var info = member.PolymorphicInfo!.Value;
                var typeIdProperty = collectionInfo.TypeIdProperty;
                var typeIdVar = typeIdProperty!.ToCamelCase();

                sb.WriteLine($"switch ({typeIdVar})");
                using var _ = sb.BeginBlock();
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

                    sb.WriteLine($"case {key}:");
                    using (sb.BeginBlock())
                    {
                        sb.WriteLine($"for (int i = 0; i < {loopLimitVar}; i++)");
                        using (sb.BeginBlock())
                        {
                            sb.WriteLine
                                ($"var item = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize({refOrEmpty}{source});");
                            sb.WriteLine($"{memberName}.Add(item);");
                        }

                        sb.WriteLine("break;");
                    }
                }

                sb.WriteLine("default:");
                sb.WriteLine
                    ($"    throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{typeIdVar}}}\");");
                return;
            }
        }

        sb.WriteLine($"for (int i = 0; i < {loopLimitVar}; i++)");
        using var block = sb.BeginBlock();
        if (member.CollectionTypeInfo?.IsArray == true)
        {
            GenerateArrayElementDeserialization
            (
                sb,
                member.CollectionTypeInfo.Value,
                memberName,
                "i",
                source,
                helper
            );
        }
        else if (member.ListTypeArgument is not null)
        {
            GenerateListElementDeserialization
            (
                sb,
                member.ListTypeArgument.Value,
                memberName,
                source,
                helper
            );
        }
        else if (member.CollectionTypeInfo is not null)
        {
            GenerateCollectionElementDeserialization
            (
                sb,
                member.CollectionTypeInfo.Value,
                memberName,
                source,
                helper
            );
        }
    }

    private static void GenerateUnlimitedCollectionDeserialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string target,
        string source,
        string helper
    )
    {
        var tempCollectionVar = $"temp{member.Name}";
        var refOrEmpty = source == "buffer" ? "ref " : "";

        ListTypeArgumentInfo listTypeInfo;
        string elementType;

        if (member.ListTypeArgument is not null)
        {
            listTypeInfo = member.ListTypeArgument.Value;
            elementType = listTypeInfo.TypeName;
        }
        else if (member.CollectionTypeInfo is not null)
        {
            var collectionTypeInfo = member.CollectionTypeInfo.Value;
            elementType = collectionTypeInfo.ElementTypeName;
            listTypeInfo = new
            (
                elementType,
                collectionTypeInfo.IsElementUnmanagedType,
                collectionTypeInfo.IsElementStringType,
                collectionTypeInfo.HasElementGenerateSerializerAttribute
            );
        }
        else
        {
            // This path should ideally not be hit for a collection.
            // Adding a comment to reflect that this is an undesirable state.
            sb.WriteLine($"// Fallback for unlimited collection {member.Name} - element type could not be determined.");
            elementType = "object"; // Fallback to object to avoid breaking compilation, though this is not ideal.
            listTypeInfo = new(elementType, false, false, false);
        }


        sb.WriteLine($"var {tempCollectionVar} = new System.Collections.Generic.List<{elementType}>();");
        sb.WriteLine(source == "buffer" ? "while (buffer.Length > 0)" : "while (stream.Position < stream.Length)");
        using (sb.BeginBlock())
        {
            GenerateListElementDeserialization
            (
                sb,
                listTypeInfo,
                tempCollectionVar,
                source,
                helper
            );
        }

        if (member.CollectionTypeInfo?.IsArray == true)
        {
            sb.WriteLine($"{target} = {tempCollectionVar}.ToArray();");
        }
        else
        {
            sb.WriteLine($"{target} = {tempCollectionVar};");
        }
    }

    private static void GeneratePolymorphicDeserialization
        (IndentedStringBuilder sb, MemberToGenerate member, string source, string helper)
    {
        var info = member.PolymorphicInfo!.Value;
        var memberName = member.Name.ToCamelCase();
        sb.WriteLine($"{member.TypeName} {memberName} = default;");
        var refOrEmpty = source == "buffer" ? "ref " : "";

        string switchVar;
        if (info.TypeIdProperty is not null)
        {
            switchVar = info.TypeIdProperty.ToCamelCase();
        }
        else
        {
            switchVar = "typeId";
            var typeToRead = info.EnumUnderlyingType ?? info.TypeIdType;
            var typeIdReadMethod = TypeHelper.GetReadMethodName(typeToRead);
            var cast = info.EnumUnderlyingType is not null ? $"({info.TypeIdType})" : "";
            sb.WriteLine($"var {switchVar} = {cast}FourSer.Gen.Helpers.{helper}.{typeIdReadMethod}({refOrEmpty}{source});");
        }

        sb.WriteLine($"switch ({switchVar})");
        using var _ = sb.BeginBlock();
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

            sb.WriteLine($"case {key}:");

            using var __ = sb.BeginBlock();
            sb.WriteLine($"{memberName} = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize({refOrEmpty}{source});");
            sb.WriteLine("break;");
        }

        sb.WriteLine("default:");
        sb.WriteLine($"    throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{switchVar}}}\");");
    }

    private static void GeneratePolymorphicItemDeserialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string assignmentTarget,
        string source,
        string helper
    )
    {
        var info = member.PolymorphicInfo!.Value;
        var refOrEmpty = source == "buffer" ? "ref " : "";

        var switchVar = "typeId";
        var typeToRead = info.EnumUnderlyingType ?? info.TypeIdType;
        var typeIdReadMethod = TypeHelper.GetReadMethodName(typeToRead);
        var cast = info.EnumUnderlyingType is not null ? $"({info.TypeIdType})" : "";
        sb.WriteLine($"var {switchVar} = {cast}FourSer.Gen.Helpers.{helper}.{typeIdReadMethod}({refOrEmpty}{source});");

        sb.WriteLine($"switch ({switchVar})");
        using var _ = sb.BeginBlock();
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

            sb.WriteLine($"case {key}:");
            using var __ = sb.BeginBlock();
            sb.WriteLine($"{assignmentTarget} = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize({refOrEmpty}{source});");
            sb.WriteLine("break;");
        }

        sb.WriteLine("default:");
        sb.WriteLine($"    throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{switchVar}}}\");");
    }

    private static void GenerateArrayElementDeserialization
    (
        IndentedStringBuilder sb,
        CollectionTypeInfo elementInfo,
        string arrayName,
        string indexVar,
        string source,
        string helper
    )
    {
        var refOrEmpty = source == "buffer" ? "ref " : "";
        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.WriteLine($"{arrayName}[{indexVar}] = FourSer.Gen.Helpers.{helper}.Read{typeName}({refOrEmpty}{source});");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.WriteLine($"{arrayName}[{indexVar}] = FourSer.Gen.Helpers.{helper}.ReadString({refOrEmpty}{source});");
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.WriteLine
            (
                $"{arrayName}[{indexVar}] = {TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize({refOrEmpty}{source});"
            );
        }
    }

    private static void GenerateListElementDeserialization
    (
        IndentedStringBuilder sb,
        ListTypeArgumentInfo elementInfo,
        string collectionTarget,
        string source,
        string helper
    )
    {
        var refOrEmpty = source == "buffer" ? "ref " : "";
        if (elementInfo.HasGenerateSerializerAttribute)
        {
            sb.WriteLine
            (
                $"{collectionTarget}.Add({TypeHelper.GetSimpleTypeName(elementInfo.TypeName)}.Deserialize({refOrEmpty}{source}));"
            );
        }
        else if (elementInfo.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.TypeName);
            sb.WriteLine($"{collectionTarget}.Add(FourSer.Gen.Helpers.{helper}.Read{typeName}({refOrEmpty}{source}));");
        }
        else if (elementInfo.IsStringType)
        {
            sb.WriteLine($"{collectionTarget}.Add(FourSer.Gen.Helpers.{helper}.ReadString({refOrEmpty}{source}));");
        }
    }

    private static void GenerateCollectionElementDeserialization
    (
        IndentedStringBuilder sb,
        CollectionTypeInfo elementInfo,
        string collectionTarget,
        string source,
        string helper
    )
    {
        var addMethod = CollectionUtilities.GetCollectionAddMethod(elementInfo.CollectionTypeName);
        var refOrEmpty = source == "buffer" ? "ref " : "";

        if (elementInfo.IsElementUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
            sb.WriteLine($"{collectionTarget}.{addMethod}(FourSer.Gen.Helpers.{helper}.Read{typeName}({refOrEmpty}{source}));");
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.WriteLine($"{collectionTarget}.{addMethod}(FourSer.Gen.Helpers.{helper}.ReadString({refOrEmpty}{source}));");
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.WriteLine
            (
                $"{collectionTarget}.{addMethod}({TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize({refOrEmpty}{source}));"
            );
        }
    }
}