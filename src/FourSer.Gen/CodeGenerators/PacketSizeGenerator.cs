using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
///     Generates GetPacketSize method implementations
/// </summary>
public static partial class PacketSizeGenerator
{
    public static void GenerateGetSize(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        sb.WriteLineFormat("public static int GetPacketSize({0} obj)", typeToGenerate.Name);
        using var _ = sb.BeginBlock();
        // null check
        if (!typeToGenerate.IsValueType)
        {
            sb.WriteLine("if (obj is null) return 0;");
        }
        
        
        sb.WriteLine("var size = 0;");

        foreach (var member in typeToGenerate.Members)
        {
            GenerateMemberSizeCalculation(sb, member, typeToGenerate);
        }

        sb.WriteLine("return size;");
    }

    private static void GenerateMemberSizeCalculation(IndentedStringBuilder sb, MemberToGenerate member, TypeToGenerate type)
    {
        if (member.IsCountSizeReferenceFor is { } countRefIndex)
        {
            var referencedMember = type.Members[countRefIndex];
            if (!ShouldDeferCollectionCountValidation(referencedMember))
            {
                var countExpression = referencedMember.IsMemoryOwner
                    ? $"(obj.{referencedMember.Name}?.Memory.Length ?? 0)"
                    : GeneratorUtilities.GetCountExpressionForAccess(referencedMember, $"obj.{referencedMember.Name}", true);
                EmitCheckedCountValidation(sb, member.TypeName, countExpression);
            }
        }

        var resolvedSerializer = GeneratorUtilities.ResolveSerializer(member, type);
        if (resolvedSerializer is { } serializer)
        {
            sb.WriteLineFormat("size += FourSer.Generated.Internal.__FourSer_Generated_Serializers.{0}.GetPacketSize(obj.{1});", serializer.FieldName, member.Name);
            return;
        }

        if (member.IsMemoryOwner)
        {
            GenerateMemoryOwnerSizeCalculation(sb, member);
        }
        else if (member.IsList || member.IsCollection)
        {
            GenerateCollectionSizeCalculation(sb, member, type);
        }
        else if (member.PolymorphicInfo is { } info)
        {
            if (info.TypeIdPropertyIndex is null)
            {
                sb.WriteLineFormat("size += {0};", PolymorphicUtilities.GenerateTypeIdSizeExpression(info));
            }

            GeneratePolymorphicSizeCalculation(sb, member);
        }
        else if (member.HasGenerateSerializerAttribute)
        {
            sb.WriteLineFormat
            (
                "size += {0}.GetPacketSize(obj.{1}); // Size for nested type {1}",
                TypeHelper.GetGlobalTypeName(member.TypeName),
                member.Name
            );
        }
        else if (member.IsStringType)
        {
            sb.WriteLineFormat("size += StringEx.MeasureSize(obj.{0}); // Size for string {0}", member.Name);
        }
        else if (member.IsUnmanagedType)
        {
            sb.WriteLineFormat("size += sizeof({0}); // Size for unmanaged type {1}", member.TypeName, member.Name);
        }
    }

    private readonly record struct ElementInfo(
        string TypeName,
        bool IsValueType,
        bool IsUnmanaged,
        bool IsString,
        bool HasSerializer);

    private static ElementInfo? GetElementInfo(MemberToGenerate member)
    {
        if (member.ListTypeArgument is { } listArg)
        {
            return new ElementInfo
            (
                listArg.TypeName,
                listArg.IsValueType,
                listArg.IsUnmanagedType,
                listArg.IsStringType,
                listArg.HasGenerateSerializerAttribute
            );
        }

        if (member.CollectionTypeInfo is { } collInfo)
        {
            return new ElementInfo
            (
                collInfo.ElementTypeName,
                collInfo.IsElementValueType,
                collInfo.IsElementUnmanagedType,
                collInfo.IsElementStringType,
                collInfo.HasElementGenerateSerializerAttribute
            );
        }

        return null;
    }

    private static ElementInfo? GetMemoryOwnerElementInfo(MemberToGenerate member)
    {
        if (member.MemoryOwnerTypeInfo is { } info)
        {
            return new ElementInfo
            (
                info.ElementTypeName,
                false,
                info.IsElementUnmanagedType,
                info.IsElementStringType,
                info.HasElementGenerateSerializerAttribute
            );
        }

        return null;
    }

    private static void EmitCheckedCountValidation(IndentedStringBuilder sb, string countType, string countExpression)
    {
        sb.WriteLineFormat("_ = checked(({0})({1}));", countType, countExpression);
    }

    private static void EmitNullCollectionItemGuard(IndentedStringBuilder sb, string itemExpression)
    {
        sb.WriteLineFormat("if ({0} is null)", itemExpression);
        using var _ = sb.BeginBlock();
        sb.WriteLine("throw new System.NullReferenceException(\"Collection item cannot be null.\");");
    }

    private static void GenerateStandardCollectionSizeCalculation(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        ElementInfo info)
    {
        var collectionAccessExpression = $"obj.{member.Name}";
        var enumerationGuard = GeneratorUtilities.GetCollectionIterationGuard(member, collectionAccessExpression);

        if (member.CustomSerializer is { } customSerializer)
        {
            var serializerField = global::FourSer.Gen.SerializerGenerator.SanitizeTypeName(customSerializer.SerializerTypeName);
            if (member.CollectionTypeInfo?.CanBeNull == true || enumerationGuard is not null)
            {
                var condition = enumerationGuard ?? $"{collectionAccessExpression} is not null";
                sb.WriteLineFormat("if ({0})", condition);
                using var _ = sb.BeginBlock();
                sb.WriteLineFormat("foreach(var item in {0})", collectionAccessExpression);
                using var __ = sb.BeginBlock();
                sb.WriteLineFormat("size += FourSer.Generated.Internal.__FourSer_Generated_Serializers.{0}.GetPacketSize(item);", serializerField);
            }
            else
            {
                sb.WriteLineFormat("foreach(var item in {0})", collectionAccessExpression);
                using var _ = sb.BeginBlock();
                sb.WriteLineFormat("size += FourSer.Generated.Internal.__FourSer_Generated_Serializers.{0}.GetPacketSize(item);", serializerField);
            }
            return;
        }

        if (info.HasSerializer)
        {
            if (member.CollectionTypeInfo?.CanBeNull == true || enumerationGuard is not null)
            {
                var condition = enumerationGuard ?? $"{collectionAccessExpression} is not null";
                sb.WriteLineFormat("if ({0})", condition);
                using var _ = sb.BeginBlock();
                sb.WriteLineFormat("foreach(var item in {0})", collectionAccessExpression);
                using var __ = sb.BeginBlock();
                if (!info.IsValueType)
                {
                    EmitNullCollectionItemGuard(sb, "item");
                }
                sb.WriteLineFormat("size += {0}.GetPacketSize(item);", TypeHelper.GetGlobalTypeName(info.TypeName));
            }
            else
            {
                sb.WriteLineFormat("foreach(var item in {0})", collectionAccessExpression);
                using var _ = sb.BeginBlock();
                if (!info.IsValueType)
                {
                    EmitNullCollectionItemGuard(sb, "item");
                }
                sb.WriteLineFormat("size += {0}.GetPacketSize(item);", TypeHelper.GetGlobalTypeName(info.TypeName));
            }
        }
        else if (info.IsUnmanaged)
        {
            var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, $"obj.{member.Name}", true);
            sb.WriteLineFormat("size += {0} * sizeof({1});", countExpression, info.TypeName);
        }
        else if (info.IsString)
        {
            if (member.CollectionTypeInfo?.CanBeNull == true || enumerationGuard is not null)
            {
                var condition = enumerationGuard ?? $"{collectionAccessExpression} is not null";
                sb.WriteLineFormat("if ({0})", condition);
                using var _ = sb.BeginBlock();
                sb.WriteLineFormat("foreach(var item in {0}) {{ size += StringEx.MeasureSize(item); }}", collectionAccessExpression);
            }
            else
            {
                sb.WriteLineFormat("foreach(var item in {0}) {{ size += StringEx.MeasureSize(item); }}", collectionAccessExpression);
            }
        }
    }

    private static void GenerateCollectionSizeCalculation(IndentedStringBuilder sb, MemberToGenerate member, TypeToGenerate type)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        var deferredCountValidationType = GetDeferredCountValidationType(member, type);

        if (
            !collectionInfo.Unlimited
            && (collectionInfo.CountSize is null or < 0)
            && collectionInfo.CountSizeReferenceIndex is null
        )
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            if (deferredCountValidationType is null)
            {
                var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, $"obj.{member.Name}", true);
                EmitCheckedCountValidation(sb, countType, countExpression);
            }
            var countSizeExpression = TypeHelper.GetSizeOfExpression(countType);
            sb.WriteLineFormat("size += {0}; // Count size for {1}", countSizeExpression, member.Name);
        }

        if (deferredCountValidationType is not null)
        {
            GenerateDeferredCountValidationCollectionSizeCalculation(sb, member, type, deferredCountValidationType);
            return;
        }

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            AddPolymorphicSerialization(sb, member, collectionInfo);
            return;
        }

        if (GetElementInfo(member) is { } info)
        {
            GenerateStandardCollectionSizeCalculation(sb, member, info);   
        }
    }

    private static void GenerateMemoryOwnerSizeCalculation(
        IndentedStringBuilder sb,
        MemberToGenerate member)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        if (
            !collectionInfo.Unlimited
            && (collectionInfo.CountSize is null or < 0)
            && collectionInfo.CountSizeReferenceIndex is null
        )
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countExpression = $"(obj.{member.Name}?.Memory.Length ?? 0)";
            EmitCheckedCountValidation(sb, countType, countExpression);
            var countSizeExpression = TypeHelper.GetSizeOfExpression(countType);
            sb.WriteLineFormat("size += {0}; // Count size for {1}", countSizeExpression, member.Name);
        }

        if (member.CustomSerializer is { } customSerializer)
        {
            var serializerField = global::FourSer.Gen.SerializerGenerator.SanitizeTypeName(customSerializer.SerializerTypeName);
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using var _ = sb.BeginBlock();
            sb.WriteLineFormat("var span_{0} = obj.{0}.Memory.Span;", member.Name);
            sb.WriteLineFormat("for (int i = 0; i < span_{0}.Length; i++)", member.Name);
            using var __ = sb.BeginBlock();
            sb.WriteLineFormat(
                "size += FourSer.Generated.Internal.__FourSer_Generated_Serializers.{0}.GetPacketSize(span_{1}[i]);",
                serializerField,
                member.Name
            );
            return;
        }

        if (GetMemoryOwnerElementInfo(member) is not { } info)
        {
            return;
        }

        if (info.HasSerializer)
        {
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using var _ = sb.BeginBlock();
            sb.WriteLineFormat("var span_{0} = obj.{0}.Memory.Span;", member.Name);
            sb.WriteLineFormat("for (int i = 0; i < span_{0}.Length; i++)", member.Name);
            using var __ = sb.BeginBlock();
            sb.WriteLineFormat("size += {0}.GetPacketSize(span_{1}[i]);", TypeHelper.GetGlobalTypeName(info.TypeName), member.Name);
        }
        else if (info.IsUnmanaged)
        {
            var countExpression = $"(obj.{member.Name}?.Memory.Length ?? 0)";
            sb.WriteLineFormat("size += {0} * sizeof({1});", countExpression, info.TypeName);
        }
        else if (info.IsString)
        {
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using var _ = sb.BeginBlock();
            sb.WriteLineFormat("var span_{0} = obj.{0}.Memory.Span;", member.Name);
            sb.WriteLineFormat("for (int i = 0; i < span_{0}.Length; i++) {{ size += StringEx.MeasureSize(span_{0}[i]); }}", member.Name);
        }
    }

    private static void AddPolymorphicSerialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        CollectionInfo collectionInfo,
        string? collectionAccessExpression = null
    )
    {
        if (member.PolymorphicInfo is not { } info)
        {
            return;
        }

        if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
        {
            if (info.TypeIdPropertyIndex is null)
            {
                sb.WriteLineFormat
                    ("size += {0}; // Size for polymorphic type id", PolymorphicUtilities.GenerateTypeIdSizeExpression(info));
            }
        }

        collectionAccessExpression ??= $"obj.{member.Name}";
        var enumerationGuard = collectionAccessExpression == $"obj.{member.Name}"
            ? GeneratorUtilities.GetCollectionIterationGuard(member, collectionAccessExpression)
            : $"{collectionAccessExpression} is not null";

        if (member.CollectionTypeInfo?.CanBeNull == true || enumerationGuard is not null)
        {
            var condition = enumerationGuard ?? $"{collectionAccessExpression} is not null";
            sb.WriteLineFormat("if ({0})", condition);
            using var _ = sb.BeginBlock();

            sb.WriteLineFormat("foreach (var item in {0})", collectionAccessExpression);
            using var __ = sb.BeginBlock();

            if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
            {
                sb.WriteLineFormat
                    ("size += {0}; // Size for polymorphic type id", PolymorphicUtilities.GenerateTypeIdSizeExpression(info));
            }

            sb.WriteLine("if (item is null)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("throw new System.NullReferenceException(\"Item in collection cannot be null.\");");
            }

            sb.WriteLine("size += item switch");
            sb.WriteLine("{");
            sb.Indent();
            foreach (var option in info.Options)
            {
                var typeName = TypeHelper.GetGlobalTypeName(option.Type);
                var varName = TypeHelper.GetSimpleTypeName(option.Type).ToCamelCase();
                sb.WriteLineFormat("{0} {1} => {2}.GetPacketSize({1}),", typeName, varName, typeName);
            }

            sb.WriteLineFormat
            (
                "_ => throw new System.IO.InvalidDataException($\"Unknown item type in collection {0}: {{item.GetType().Name}}\")",
                member.Name
            );
            sb.Unindent();
            sb.WriteLine("};");
            return;
        }

        sb.WriteLineFormat("foreach (var item in {0})", collectionAccessExpression);
        using var ___ = sb.BeginBlock();

        if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
        {
            sb.WriteLineFormat
                ("size += {0}; // Size for polymorphic type id", PolymorphicUtilities.GenerateTypeIdSizeExpression(info));
        }

        sb.WriteLine("if (item is null)");
        using (sb.BeginBlock())
        {
            sb.WriteLine("throw new System.NullReferenceException(\"Item in collection cannot be null.\");");
        }

        sb.WriteLine("size += item switch");
        sb.WriteLine("{");
        sb.Indent();
        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetGlobalTypeName(option.Type);
            var varName = TypeHelper.GetSimpleTypeName(option.Type).ToCamelCase();
            sb.WriteLineFormat("{0} {1} => {2}.GetPacketSize({1}),", typeName, varName, typeName);
        }

        sb.WriteLineFormat
        (
            "_ => throw new System.IO.InvalidDataException($\"Unknown item type in collection {0}: {{item.GetType().Name}}\")",
            member.Name
        );
        sb.Unindent();
        sb.WriteLine("};");
    }

    private static void GeneratePolymorphicSizeCalculation
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string instanceName = ""
    )
    {
        if (string.IsNullOrEmpty(instanceName))
        {
            instanceName = $"obj.{member.Name}";
        }

        if (member.PolymorphicInfo is not { } info)
        {
            return;
        }

        sb.WriteLineFormat("switch ({0})", instanceName);
        using var _ = sb.BeginBlock();
        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetGlobalTypeName(option.Type);
            sb.WriteLineFormat("case {0} typedInstance:", typeName);
            sb.WriteLineFormat("    size += {0}.GetPacketSize(typedInstance);", typeName);
            sb.WriteLine("    break;");
        }

        sb.WriteLine("case null: break;");
        sb.WriteLine("default:");
        sb.WriteLineFormat
        (
            "    throw new System.IO.InvalidDataException($\"Unknown type for {0}: {{{1}?.GetType().FullName}}\");",
            member.Name,
            instanceName
        );
    }
}
