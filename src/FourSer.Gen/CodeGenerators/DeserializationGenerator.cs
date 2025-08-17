using System.Collections.Generic;
using System.Linq;
using System.Text;
using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using System;

namespace FourSer.Gen.CodeGenerators
{
    /// <summary>
    /// Generates Deserialize method implementations
    /// </summary>
    public static class DeserializationGenerator
    {
        public static void GenerateDeserialize(StringBuilder sb, TypeToGenerate typeToGenerate)
        {
            GenerateDeserializeWithRef(sb, typeToGenerate);
            sb.AppendLine();
            GenerateDeserializeWithSpan(sb, typeToGenerate);
            sb.AppendLine();
            GenerateDeserializeWithStream(sb, typeToGenerate);
        }

        private static void GenerateDeserializeWithSpan(StringBuilder sb, TypeToGenerate typeToGenerate)
        {
            var newKeyword = typeToGenerate.HasSerializableBaseType ? "new " : "";
            sb.AppendLine($"    public static {newKeyword}{typeToGenerate.Name} Deserialize(System.ReadOnlySpan<byte> buffer)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return Deserialize(ref buffer);");
            sb.AppendLine("    }");
        }

        private static void GenerateDeserializeWithStream(StringBuilder sb, TypeToGenerate typeToGenerate)
        {
            var newKeyword = typeToGenerate.HasSerializableBaseType ? "new " : "";
            sb.AppendLine($"    public static {newKeyword}{typeToGenerate.Name} Deserialize(System.IO.Stream stream)");
            sb.AppendLine("    {");

            foreach (var member in typeToGenerate.Members)
            {
                GenerateMemberDeserialization(sb, member, true, "stream", "StreamReaderHelpers");
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
            else
            {
                sb.AppendLine($"        var obj = new {typeToGenerate.Name}();");
                foreach (var member in typeToGenerate.Members)
                {
                    var camelCaseName = StringExtensions.ToCamelCase(member.Name);
                    sb.AppendLine($"        obj.{member.Name} = {camelCaseName};");
                }
            }

            sb.AppendLine("        return obj;");
            sb.AppendLine("    }");
        }

        private static void GenerateDeserializeWithRef(StringBuilder sb, TypeToGenerate typeToGenerate)
        {
            var newKeyword = typeToGenerate.HasSerializableBaseType ? "new " : "";
            sb.AppendLine($"    public static {newKeyword}{typeToGenerate.Name} Deserialize(ref System.ReadOnlySpan<byte> buffer)");
            sb.AppendLine("    {");

            foreach (var member in typeToGenerate.Members)
            {
                GenerateMemberDeserialization(sb, member, true, "buffer", "RoSpanReaderHelpers");
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
            else
            {
                sb.AppendLine($"        var obj = new {typeToGenerate.Name}();");
                foreach (var member in typeToGenerate.Members)
                {
                    var camelCaseName = StringExtensions.ToCamelCase(member.Name);
                    sb.AppendLine($"        obj.{member.Name} = {camelCaseName};");
                }
            }

            sb.AppendLine("        return obj;");
            sb.AppendLine("    }");
        }


        private static void GenerateMemberDeserialization(StringBuilder sb, MemberToGenerate member, bool isCtorParam, string source, string helper)
        {
            var target = isCtorParam ? $"var {StringExtensions.ToCamelCase(member.Name)}" : $"obj.{member.Name}";
            var refOrEmpty = source == "buffer" ? "ref " : "";

            if (member.IsList || member.IsCollection)
            {
                GenerateCollectionDeserialization(sb, member, target, source, helper);
            }
            else if (member.PolymorphicInfo is not null)
            {
                GeneratePolymorphicDeserialization(sb, member, source, helper);
            }
            else if (member.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"        {target} = {TypeHelper.GetSimpleTypeName(member.TypeName)}.Deserialize({refOrEmpty}{source});");
            }
            else if (member.IsStringType)
            {
                sb.AppendLine($"        {target} = FourSer.Gen.Helpers.{helper}.ReadString({refOrEmpty}{source});");
            }
            else if (member.IsUnmanagedType)
            {
                var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(member.TypeName);
                var readMethod = $"Read{typeName}";
                sb.AppendLine($"        {target} = FourSer.Gen.Helpers.{helper}.{readMethod}({refOrEmpty}{source});");
            }
        }

        private static void GenerateCollectionDeserialization(StringBuilder sb, MemberToGenerate member, string target, string source, string helper)
        {
            if (member.CollectionInfo is not { } collectionInfo)
                return;

            var refOrEmpty = source == "buffer" ? "ref " : "";

            if (collectionInfo.Unlimited)
            {
                GenerateUnlimitedCollectionDeserialization(sb, member, target, source, helper);
                return;
            }

            var memberName = StringExtensions.ToCamelCase(member.Name);
            string countVar;

            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countReadMethod = TypeHelper.GetReadMethodName(countType);

            if (collectionInfo.CountSizeReference is string countSizeReference)
            {
                countVar = StringExtensions.ToCamelCase(countSizeReference);
            }
            else
            {
                countVar = $"{memberName}Count";
                sb.AppendLine($"        var {countVar} = FourSer.Gen.Helpers.{helper}.{countReadMethod}({refOrEmpty}{source});");
            }

            var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
            var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);

            if (isByteCollection)
            {
                if (member.CollectionTypeInfo?.IsArray == true)
                {
                sb.AppendLine($"        {target} = FourSer.Gen.Helpers.{helper}.ReadBytes({refOrEmpty}{source}, (int){countVar});");
                }
                else if (member.CollectionTypeInfo?.CollectionTypeName == "System.Collections.Generic.List<T>")
                {
                    sb.AppendLine($"        {target} = FourSer.Gen.Helpers.{helper}.ReadBytes({refOrEmpty}{source}, (int){countVar}).ToList();");
                }
                else
                {
                    sb.AppendLine($"        {target} = FourSer.Gen.Helpers.{helper}.ReadBytes({refOrEmpty}{source}, (int){countVar});");
                }
                return;
            }

            var capacityVar = countType is "uint" or "ulong" ? $"(int){countVar}" : countVar;
            sb.AppendLine($"        {CollectionUtilities.GenerateCollectionInstantiation(member, capacityVar, target)}");

            var loopLimitVar = countType is "ulong" or "uint" ? $"(int){countVar}" : countVar;

            if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
            {
                if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
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
                        false, null, null, member.PolymorphicInfo, false, null, false, false,
                        LocationInfo.None
                    );
                    GeneratePolymorphicItemDeserialization(sb, itemMember, "item", source, helper);
                    sb.AppendLine($"            {memberName}.Add(item);");
                    sb.AppendLine("        }");
                    return;
                }

                if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
                {
                    var info = member.PolymorphicInfo!.Value;
                    var typeIdProperty = collectionInfo.TypeIdProperty;
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
                        sb.AppendLine($"                    var item = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize({refOrEmpty}{source});");
                        sb.AppendLine($"                    {memberName}.Add(item);");
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
                GenerateArrayElementDeserialization(sb, member.CollectionTypeInfo.Value, memberName, "i", source, helper);
            }
            else if (member.ListTypeArgument is not null)
            {
                GenerateListElementDeserialization(sb, member.ListTypeArgument.Value, memberName, source, helper);
            }
            else if (member.CollectionTypeInfo is not null)
            {
                GenerateCollectionElementDeserialization(sb, member.CollectionTypeInfo.Value, memberName, source, helper);
            }

            sb.AppendLine("        }");
        }

        private static void GenerateUnlimitedCollectionDeserialization(StringBuilder sb, MemberToGenerate member, string target, string source, string helper)
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
                listTypeInfo = new ListTypeArgumentInfo(
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
                sb.AppendLine($"        // Fallback for unlimited collection {member.Name} - element type could not be determined.");
                elementType = "object"; // Fallback to object to avoid breaking compilation, though this is not ideal.
                listTypeInfo = new ListTypeArgumentInfo(elementType, false, false, false);
            }


            sb.AppendLine($"        var {tempCollectionVar} = new System.Collections.Generic.List<{elementType}>();");
            sb.AppendLine(source == "buffer" ? "        while (buffer.Length > 0)" : "        while (stream.Position < stream.Length)");
            sb.AppendLine("        {");

            GenerateListElementDeserialization(sb, listTypeInfo, tempCollectionVar, source, helper);

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

        private static void GeneratePolymorphicDeserialization(StringBuilder sb, MemberToGenerate member, string source, string helper)
        {
            var info = member.PolymorphicInfo!.Value;
            var memberName = StringExtensions.ToCamelCase(member.Name);
            sb.AppendLine($"        {member.TypeName} {memberName} = default;");
            var refOrEmpty = source == "buffer" ? "ref " : "";

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
                sb.AppendLine($"        var {switchVar} = {cast}FourSer.Gen.Helpers.{helper}.{typeIdReadMethod}({refOrEmpty}{source});");
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
                sb.AppendLine($"                {memberName} = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize({refOrEmpty}{source});");
                sb.AppendLine("                break;");
                sb.AppendLine("            }");
            }

            sb.AppendLine("            default:");
            sb.AppendLine($"                throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{switchVar}}}\");");
            sb.AppendLine("        }");
        }

        private static void GeneratePolymorphicItemDeserialization(StringBuilder sb, MemberToGenerate member, string assignmentTarget, string source, string helper)
        {
            var info = member.PolymorphicInfo!.Value;
            var refOrEmpty = source == "buffer" ? "ref " : "";

            var switchVar = "typeId";
            var typeToRead = info.EnumUnderlyingType ?? info.TypeIdType;
            var typeIdReadMethod = TypeHelper.GetReadMethodName(typeToRead);
            var cast = info.EnumUnderlyingType is not null ? $"({info.TypeIdType})" : "";
            sb.AppendLine($"            var {switchVar} = {cast}FourSer.Gen.Helpers.{helper}.{typeIdReadMethod}({refOrEmpty}{source});");

            sb.AppendLine($"            switch ({switchVar})");
            sb.AppendLine("            {");

            foreach (var option in info.Options)
            {
                var key = option.Key.ToString();
                if (info.EnumUnderlyingType is not null) { key = $"({info.TypeIdType}){key}"; }
                else if (info.TypeIdType.EndsWith("Enum")) { key = $"{info.TypeIdType}.{key}"; }

                sb.AppendLine($"                case {key}:");
                sb.AppendLine("                {");
                sb.AppendLine($"                    {assignmentTarget} = {TypeHelper.GetSimpleTypeName(option.Type)}.Deserialize({refOrEmpty}{source});");
                sb.AppendLine("                    break;");
                sb.AppendLine("                }");
            }

            sb.AppendLine("                default:");
            sb.AppendLine($"                    throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{switchVar}}}\");");
            sb.AppendLine("            }");
        }

        private static void GenerateArrayElementDeserialization(StringBuilder sb, CollectionTypeInfo elementInfo, string arrayName, string indexVar, string source, string helper)
        {
            var refOrEmpty = source == "buffer" ? "ref " : "";
            if (elementInfo.IsElementUnmanagedType)
            {
                var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
                sb.AppendLine($"            {arrayName}[{indexVar}] = FourSer.Gen.Helpers.{helper}.Read{typeName}({refOrEmpty}{source});");
            }
            else if (elementInfo.IsElementStringType)
            {
                sb.AppendLine($"            {arrayName}[{indexVar}] = FourSer.Gen.Helpers.{helper}.ReadString({refOrEmpty}{source});");
            }
            else if (elementInfo.HasElementGenerateSerializerAttribute)
            {
                sb.AppendLine($"            {arrayName}[{indexVar}] = {TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize({refOrEmpty}{source});");
            }
        }

        private static void GenerateListElementDeserialization(StringBuilder sb, ListTypeArgumentInfo elementInfo, string collectionTarget, string source, string helper)
        {
            var refOrEmpty = source == "buffer" ? "ref " : "";
            if (elementInfo.HasGenerateSerializerAttribute)
            {
                sb.AppendLine($"            {collectionTarget}.Add({TypeHelper.GetSimpleTypeName(elementInfo.TypeName)}.Deserialize({refOrEmpty}{source}));");
            }
            else if (elementInfo.IsUnmanagedType)
            {
                var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.TypeName);
                sb.AppendLine($"            {collectionTarget}.Add(FourSer.Gen.Helpers.{helper}.Read{typeName}({refOrEmpty}{source}));");
            }
            else if (elementInfo.IsStringType)
            {
                sb.AppendLine($"            {collectionTarget}.Add(FourSer.Gen.Helpers.{helper}.ReadString({refOrEmpty}{source}));");
            }
        }

        private static void GenerateCollectionElementDeserialization(StringBuilder sb, CollectionTypeInfo elementInfo, string collectionTarget, string source, string helper)
        {
            var addMethod = CollectionUtilities.GetCollectionAddMethod(elementInfo.CollectionTypeName);
            var refOrEmpty = source == "buffer" ? "ref " : "";

            if (elementInfo.IsElementUnmanagedType)
            {
                var typeName = GeneratorUtilities.GetMethodFriendlyTypeName(elementInfo.ElementTypeName);
                sb.AppendLine($"            {collectionTarget}.{addMethod}(FourSer.Gen.Helpers.{helper}.Read{typeName}({refOrEmpty}{source}));");
            }
            else if (elementInfo.IsElementStringType)
            {
                sb.AppendLine($"            {collectionTarget}.{addMethod}(FourSer.Gen.Helpers.{helper}.ReadString({refOrEmpty}{source}));");
            }
            else if (elementInfo.HasElementGenerateSerializerAttribute)
            {
                sb.AppendLine($"            {collectionTarget}.{addMethod}({TypeHelper.GetSimpleTypeName(elementInfo.ElementTypeName)}.Deserialize({refOrEmpty}{source}));");
            }
        }

    }
}
