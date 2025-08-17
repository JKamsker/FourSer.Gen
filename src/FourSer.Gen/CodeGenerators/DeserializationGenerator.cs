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
        sb.WriteLineFormat("public static {0}{1} Deserialize(System.ReadOnlySpan<byte> buffer)", newKeyword, typeToGenerate.Name);
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
            sb.WriteLineFormat("var obj = new {0}({1});", typeToGenerate.Name, ctorArgs);

            var membersInCtor = new HashSet<string>(ctor.Parameters.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            var membersNotInCtor = typeToGenerate.Members.Where(m => !membersInCtor.Contains(m.Name));

            foreach (var member in membersNotInCtor)
            {
                var camelCaseName = member.Name.ToCamelCase();
                sb.WriteLineFormat("obj.{0} = {1};", member.Name, camelCaseName);
            }
        }
        else
        {
            sb.WriteLineFormat("var obj = new {0}();", typeToGenerate.Name);
            foreach (var member in typeToGenerate.Members)
            {
                var camelCaseName = member.Name.ToCamelCase();
                sb.WriteLineFormat("obj.{0} = {1};", member.Name, camelCaseName);
            }
        }

        sb.WriteLine("return obj;");
    }

    private static void GenerateDeserializeWithRef(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        var newKeyword = typeToGenerate.HasSerializableBaseType ? "new " : "";
        sb.WriteLineFormat("public static {0}{1} Deserialize(ref System.ReadOnlySpan<byte> buffer)", newKeyword, typeToGenerate.Name);
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
            sb.WriteLineFormat("var obj = new {0}({1});", typeToGenerate.Name, ctorArgs);

            var membersInCtor = new HashSet<string>(ctor.Parameters.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            var membersNotInCtor = typeToGenerate.Members.Where(m => !membersInCtor.Contains(m.Name));

            foreach (var member in membersNotInCtor)
            {
                var camelCaseName = member.Name.ToCamelCase();
                sb.WriteLineFormat("obj.{0} = {1};", member.Name, camelCaseName);
            }
        }
        else
        {
            sb.WriteLineFormat("var obj = new {0}();", typeToGenerate.Name);
            foreach (var member in typeToGenerate.Members)
            {
                var camelCaseName = member.Name.ToCamelCase();
                sb.WriteLineFormat("obj.{0} = {1};", member.Name, camelCaseName);
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
            sb.WriteLineFormat("{0} = {1}.Deserialize({2}{3});", target, TypeHelper.GetSimpleTypeName(member.TypeName), refOrEmpty, source);
        }
        else if (member.IsStringType)
        {
            sb.WriteLineFormat("{0} = FourSer.Gen.Helpers.{1}.ReadString({2}{3});", target, helper, refOrEmpty, source);
        }
        else if (member.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
            var readMethod = $"Read{typeName}";
            sb.WriteLineFormat("{0} = FourSer.Gen.Helpers.{1}.{2}({3}{4});", target, helper, readMethod, refOrEmpty, source);
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
            sb.WriteLineFormat("var {0} = FourSer.Gen.Helpers.{1}.{2}({3}{4});", countVar, helper, countReadMethod, refOrEmpty, source);
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

        if (isByteCollection)
        {
            if (member.CollectionTypeInfo?.IsArray == true)
            {
                sb.WriteLineFormat("{0} = FourSer.Gen.Helpers.{1}.ReadBytes({2}{3}, (int){4});", target, helper, refOrEmpty, source, countVar);
            }
            else if (member.CollectionTypeInfo?.CollectionTypeName == "System.Collections.Generic.List<T>")
            {
                sb.WriteLineFormat
                    ("{0} = FourSer.Gen.Helpers.{1}.ReadBytes({2}{3}, (int){4}).ToList();", target, helper, refOrEmpty, source, countVar);
            }
            else
            {
                sb.WriteLineFormat("{0} = FourSer.Gen.Helpers.{1}.ReadBytes({2}{3}, (int){4});", target, helper, refOrEmpty, source, countVar);
            }

            return;
        }

        var capacityVar = countType is "uint" or "ulong" ? $"(int){countVar}" : countVar;
        sb.WriteLine(CollectionUtilities.GenerateCollectionInstantiation(member, capacityVar, target));

        var loopLimitVar = countType is "ulong" or "uint" ? $"(int){countVar}" : countVar;

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
            {
                sb.WriteLineFormat("for (int i = 0; i < {0}; i++)", loopLimitVar);
                using var _ = sb.BeginBlock();
                sb.WriteLineFormat("{0} item;", member.ListTypeArgument!.Value.TypeName);
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
                sb.WriteLineFormat("{0}.Add(item);", memberName);
                return;
            }

            if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                var info = member.PolymorphicInfo!.Value;
                var typeIdProperty = collectionInfo.TypeIdProperty;
                var typeIdVar = typeIdProperty!.ToCamelCase();

                sb.WriteLineFormat("switch ({0})", typeIdVar);
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

                    sb.WriteLineFormat("case {0}:", key);
                    using (sb.BeginBlock())
                    {
                        sb.WriteLineFormat("for (int i = 0; i < {0}; i++)", loopLimitVar);
                        using (sb.BeginBlock())
                        {
                            sb.WriteLineFormat
                                ("var item = {0}.Deserialize({1}{2});", TypeHelper.GetSimpleTypeName(option.Type), refOrEmpty, source);
                            sb.WriteLineFormat("{0}.Add(item);", memberName);
                        }

                        sb.WriteLine("break;");
                    }
                }

                sb.WriteLine("default:");
                sb.WriteLineFormat
                    ("    throw new System.IO.InvalidDataException($\"Unknown type id for {0}: {{{1}}}\");", member.Name, typeIdVar);
                return;
            }
        }

        sb.WriteLineFormat("for (int i = 0; i < {0}; i++)", loopLimitVar);
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
            sb.WriteLineFormat("// Fallback for unlimited collection {0} - element type could not be determined.", member.Name);
            elementType = "object"; // Fallback to object to avoid breaking compilation, though this is not ideal.
            listTypeInfo = new(elementType, false, false, false);
        }


        sb.WriteLineFormat("var {0} = new System.Collections.Generic.List<{1}>();", tempCollectionVar, elementType);
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
            sb.WriteLineFormat("{0} = {1}.ToArray();", target, tempCollectionVar);
        }
        else
        {
            sb.WriteLineFormat("{0} = {1};", target, tempCollectionVar);
        }
    }

    private static void GeneratePolymorphicDeserialization
        (IndentedStringBuilder sb, MemberToGenerate member, string source, string helper)
    {
        var info = member.PolymorphicInfo!.Value;
        var memberName = member.Name.ToCamelCase();
        sb.WriteLineFormat("{0} {1} = default;", member.TypeName, memberName);
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
            sb.WriteLineFormat("var {0} = {1}FourSer.Gen.Helpers.{2}.{3}({4}{5});", switchVar, cast, helper, typeIdReadMethod, refOrEmpty, source);
        }

        sb.WriteLineFormat("switch ({0})", switchVar);
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

            sb.WriteLineFormat("case {0}:", key);

            using var __ = sb.BeginBlock();
            sb.WriteLineFormat("{0} = {1}.Deserialize({2}{3});", memberName, TypeHelper.GetSimpleTypeName(option.Type), refOrEmpty, source);
            sb.WriteLine("break;");
        }

        sb.WriteLine("default:");
        sb.WriteLineFormat("    throw new System.IO.InvalidDataException($\"Unknown type id for {0}: {{{1}}}\");", member.Name, switchVar);
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
        sb.WriteLineFormat("var {0} = {1}FourSer.Gen.Helpers.{2}.{3}({4}{5});", switchVar, cast, helper, typeIdReadMethod, refOrEmpty, source);

        sb.WriteLineFormat("switch ({0})", switchVar);
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

            sb.WriteLineFormat("case {0}:", key);
            using var __ = sb.BeginBlock();
            sb.WriteLineFormat("{0} = {1}.Deserialize({2}{3});", assignmentTarget, TypeHelper.GetSimpleTypeName(option.Type), refOrEmpty, source);
            sb.WriteLine("break;");
        }

        sb.WriteLine("default:");
        sb.WriteLineFormat("    throw new System.IO.InvalidDataException($\"Unknown type id for {0}: {{{1}}}\");", member.Name, switchVar);
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
            sb.WriteLineFormat("{0}[{1}] = FourSer.Gen.Helpers.{2}.Read{3}({4}{5});", arrayName, indexVar, helper, typeName, refOrEmpty, source);
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.WriteLineFormat("{0}[{1}] = FourSer.Gen.Helpers.{2}.ReadString({3}{4});", arrayName, indexVar, helper, refOrEmpty, source);
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.WriteLineFormat
            (
                "{0}[{1}] = {2}.Deserialize({3}{4});", arrayName, indexVar, TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName), refOrEmpty, source
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
            sb.WriteLineFormat
            (
                "{0}.Add({1}.Deserialize({2}{3}));", collectionTarget, TypeHelper.GetSimpleTypeName(elementInfo.TypeName), refOrEmpty, source
            );
        }
        else if (elementInfo.IsUnmanagedType)
        {
            var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.TypeName);
            sb.WriteLineFormat("{0}.Add(FourSer.Gen.Helpers.{1}.Read{2}({3}{4}));", collectionTarget, helper, typeName, refOrEmpty, source);
        }
        else if (elementInfo.IsStringType)
        {
            sb.WriteLineFormat("{0}.Add(FourSer.Gen.Helpers.{1}.ReadString({2}{3}));", collectionTarget, helper, refOrEmpty, source);
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
            sb.WriteLineFormat("{0}.{1}(FourSer.Gen.Helpers.{2}.Read{3}({4}{5}));", collectionTarget, addMethod, helper, typeName, refOrEmpty, source);
        }
        else if (elementInfo.IsElementStringType)
        {
            sb.WriteLineFormat("{0}.{1}(FourSer.Gen.Helpers.{2}.ReadString({3}{4}));", collectionTarget, addMethod, helper, refOrEmpty, source);
        }
        else if (elementInfo.HasElementGenerateSerializerAttribute)
        {
            sb.WriteLineFormat
            (
                "{0}.{1}({2}.Deserialize({3}{4}));", collectionTarget, addMethod, TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName), refOrEmpty, source
            );
        }
    }
}