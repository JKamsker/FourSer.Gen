using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

public static class PacketSizeGenerator
{
    public static void GenerateGetPacketSize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLineFormat("public static int GetPacketSize({0} obj)", typeToGenerate.Name);
        using (sb.BeginBlock())
        {
            sb.WriteLine("var size = 0;");
            foreach (var member in typeToGenerate.Members)
            {
                GenerateMemberPacketSize(sb, member);
            }

            sb.WriteLine("return size;");
        }
    }

    private static void GenerateMemberPacketSize(IndentedStringBuilder sb, MemberToGenerate member)
    {
        if (member.IsList || member.IsCollection)
        {
            GenerateCollectionPacketSize(sb, member);
        }
        else if (member.PolymorphicInfo is not null)
        {
            GeneratePolymorphicPacketSize(sb, member);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat("size += {0}.GetPacketSize(obj.{1});", member.TypeName, member.Name);
            }
        }
        else if (member.IsStringType)
        {
            sb.WriteLineFormat(
                "size += obj.{0} is null ? sizeof(int) : System.Text.Encoding.UTF8.GetByteCount(obj.{0}) + sizeof(int);",
                member.Name);
        }
        else if (member.IsUnmanagedType)
        {
            sb.WriteLineFormat("size += sizeof({0}); // Size for unmanaged type {1}", member.TypeName, member.Name);
        }
    }

    private static void GenerateCollectionPacketSize(IndentedStringBuilder sb, MemberToGenerate member)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
        using (sb.BeginBlock())
        {
            if (collectionInfo is { Unlimited: false } && string.IsNullOrEmpty(collectionInfo.CountSizeReference))
            {
                var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
                sb.WriteLine($"size += sizeof({countType}); // Count size for {member.Name}");
            }

            if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
            {
                if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
                {
                    sb.WriteLineFormat("foreach (var item in obj.{0})", member.Name);
                    using var _ = sb.BeginBlock();
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
                    GeneratePolymorphicItemPacketSize(sb, itemMember, "item");
                    sb.WriteLine("size += sizeof(byte);");
                    return;
                }

                if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
                {
                    sb.WriteLineFormat("switch (obj.{0})", collectionInfo.TypeIdProperty);
                    using var _ = sb.BeginBlock();
                    if (member.PolymorphicInfo is { } info)
                    {
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
                                sb.WriteLineFormat("foreach (var item in obj.{0})", member.Name);
                                using (sb.BeginBlock())
                                {
                                    sb.WriteLineFormat
                                    (
                                        "size += {0}.GetPacketSize(({0})item);",
                                        TypeHelper.GetSimpleTypeName(option.Type)
                                    );
                                }

                                sb.WriteLine("break;");
                            }
                        }
                    }

                    return;
                }
            }


            var elementTypeName = member.ListTypeArgument?.TypeName ??
                                  member.CollectionTypeInfo?.ElementTypeName;

            var isByteCollection = TypeHelper.IsByteCollection(elementTypeName);
            if (isByteCollection)
            {
                sb.WriteLineFormat("size += obj.{0}.Length;", member.Name);
            }
            else
            {
                var elementTypeNameSimple = TypeHelper.GetSimpleTypeName(elementTypeName);
                var isUnmanaged = TypeHelper.IsUnmanaged(elementTypeName);
                if (isUnmanaged)
                {
                    sb.WriteLineFormat("size += obj.{0}.Count() * sizeof({1});", member.Name, elementTypeName);
                }
                else if (elementTypeName == "string")
                {
                    sb.WriteLineFormat("size += obj.{0}.Sum(s => System.Text.Encoding.UTF8.GetByteCount(s) + sizeof(int));", member.Name);
                }
                else
                {
                    sb.WriteLineFormat("foreach (var item in obj.{0})", member.Name);
                    using (sb.BeginBlock())
                    {
                        sb.WriteLineFormat("size += {0}.GetPacketSize(item);", elementTypeNameSimple);
                    }
                }
            }
        }
    }

    private static void GeneratePolymorphicPacketSize(IndentedStringBuilder sb, MemberToGenerate member)
    {
        var info = member.PolymorphicInfo!.Value;
        sb.WriteLineFormat("switch(obj.{0})", member.Name);
        using (sb.BeginBlock())
        {
            foreach (var option in info.Options)
            {
                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                sb.WriteLineFormat("case {0} typedInstance:", typeName);
                using (sb.BeginBlock())
                {
                    if (string.IsNullOrEmpty(info.TypeIdProperty))
                    {
                        var typeIdType = info.TypeIdType;
                        sb.WriteLine($"size += sizeof({typeIdType});");
                    }

                    sb.WriteLineFormat("size += {0}.GetPacketSize(typedInstance);", typeName);
                    sb.WriteLine("break;");
                }
            }

            sb.WriteLine("case null: break;");
            sb.WriteLine("default:");
            sb.WriteLineFormat(
                "    throw new System.IO.InvalidDataException($\"Unknown type for {0}: {{obj.{0}?.GetType().FullName}}\");",
                member.Name);
        }
    }

    private static void GeneratePolymorphicItemPacketSize
        (IndentedStringBuilder sb, MemberToGenerate member, string instanceName)
    {
        var info = member.PolymorphicInfo!.Value;
        sb.WriteLineFormat("switch({0})", instanceName);
        using (sb.BeginBlock())
        {
            foreach (var option in info.Options)
            {
                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                sb.WriteLineFormat("case {0} typedInstance:", typeName);
                using (sb.BeginBlock())
                {
                    sb.WriteLineFormat("size += {0}.GetPacketSize(typedInstance);", typeName);
                    sb.WriteLine("break;");
                }
            }

            sb.WriteLine("case null: break;");
            sb.WriteLine("default:");
            sb.WriteLineFormat(
                "    throw new System.IO.InvalidDataException($\"Unknown type for item: {{{0}?.GetType().FullName}}\");",
                instanceName);
        }
    }
}
