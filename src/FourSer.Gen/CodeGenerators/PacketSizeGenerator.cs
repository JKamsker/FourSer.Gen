using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Emission;
using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

/// <summary>
///     Generates GetPacketSize method implementations
/// </summary>
internal static partial class PacketSizeGenerator
{
    internal static MethodPlan GenerateGetSize(
        IndentedStringBuilder sb,
        TypeToGenerate typeToGenerate,
        FourSerGeneratorOptions options,
        TargetCapabilities capabilities)
    {
        var plan = PlanPipeline.BuildPacketSizePlan(typeToGenerate, options, capabilities);
        SizePlanEmitter.Emit(sb, plan);
        return plan;
    }

    private static void GenerateMemberSizeCalculation(IndentedStringBuilder sb, MemberToGenerate member, TypeToGenerate type)
    {
        var resolvedSerializer = GeneratorUtilities.ResolveSerializer(member, type);
        if (resolvedSerializer is { } serializer)
        {
            sb.WriteLineFormat("size += FourSer.Generated.Internal.__FourSer_Generated_Serializers.{0}.GetPacketSize(obj.{1});", serializer.FieldName, member.Name);
            return;
        }

        if (member.IsMemoryOwner)
        {
            GenerateMemoryOwnerSizeCalculation(sb, member, type);
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

    private static bool RequiresCheckedCountValidation(string? countValidationType)
    {
        return countValidationType is not null
            && GeneratorUtilities.ShouldUseCheckedCountConversion(countValidationType);
    }

    private static string? GetCollectionCountValidationType(MemberToGenerate member, TypeToGenerate type)
    {
        if (member.CollectionInfo is not { } collectionInfo || collectionInfo.Unlimited)
        {
            return null;
        }

        if (collectionInfo.CountSizeReferenceIndex is { } countReferenceIndex)
        {
            return type.Members[countReferenceIndex].TypeName;
        }

        return collectionInfo.CountSize is null or < 0
            ? collectionInfo.CountType ?? TypeHelper.GetDefaultCountType()
            : null;
    }

    private static string? DeclareValidatedCountVariable(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string? countValidationType)
    {
        if (!RequiresCheckedCountValidation(countValidationType))
        {
            return null;
        }

        var countVariableName = $"{member.Name.ToCamelCase()}ValidatedCount";
        sb.WriteLineFormat("{0} {1} = 0;", countValidationType, countVariableName);
        return countVariableName;
    }

    private static void EmitValidatedCountIncrement(
        IndentedStringBuilder sb,
        string? countVariableName,
        string? countValidationType)
    {
        if (countVariableName is null || countValidationType is null)
        {
            return;
        }

        sb.WriteLineFormat("{0} = checked(({1})({0} + 1));", countVariableName, countValidationType);
    }

    private static string DeclareCheckedCountLocal(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string countType,
        string countExpression,
        string suffix = "Count")
    {
        var countLocalName = $"{member.Name.ToCamelCase()}{suffix}";
        sb.WriteLineFormat("var {0} = checked(({1})({2}));", countLocalName, countType, countExpression);
        return countLocalName;
    }

    private static void GenerateStandardCollectionSizeCalculation(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        ElementInfo info,
        string collectionAccessExpression,
        string? countValidationType = null)
    {
        var enumerationGuard = collectionAccessExpression == $"obj.{member.Name}"
            ? GeneratorUtilities.GetCollectionIterationGuard(member, collectionAccessExpression)
            : null;

        if (member.CustomSerializer is { } customSerializer)
        {
            var serializerField = global::FourSer.Gen.SerializerGenerator.SanitizeTypeName(customSerializer.SerializerTypeName);
            if (member.CollectionTypeInfo?.CanBeNull == true || enumerationGuard is not null)
            {
                var condition = enumerationGuard ?? $"{collectionAccessExpression} is not null";
                sb.WriteLineFormat("if ({0})", condition);
                using var _ = sb.BeginBlock();
                var validatedCountVariableName = DeclareValidatedCountVariable(sb, member, countValidationType);
                sb.WriteLineFormat("foreach(var item in {0})", collectionAccessExpression);
                using var __ = sb.BeginBlock();
                EmitValidatedCountIncrement(sb, validatedCountVariableName, countValidationType);
                sb.WriteLineFormat("size += FourSer.Generated.Internal.__FourSer_Generated_Serializers.{0}.GetPacketSize(item);", serializerField);
            }
            else
            {
                var validatedCountVariableName = DeclareValidatedCountVariable(sb, member, countValidationType);
                sb.WriteLineFormat("foreach(var item in {0})", collectionAccessExpression);
                using var _ = sb.BeginBlock();
                EmitValidatedCountIncrement(sb, validatedCountVariableName, countValidationType);
                sb.WriteLineFormat("size += FourSer.Generated.Internal.__FourSer_Generated_Serializers.{0}.GetPacketSize(item);", serializerField);
            }
            return;
        }

        if (info.HasSerializer)
        {
            var needsItemNullGuard = !info.IsValueType;
            if (member.CollectionTypeInfo?.CanBeNull == true || enumerationGuard is not null)
            {
                var condition = enumerationGuard ?? $"{collectionAccessExpression} is not null";
                sb.WriteLineFormat("if ({0})", condition);
                using var _ = sb.BeginBlock();
                var validatedCountVariableName = DeclareValidatedCountVariable(sb, member, countValidationType);
                sb.WriteLineFormat("foreach(var item in {0})", collectionAccessExpression);
                using var __ = sb.BeginBlock();
                EmitValidatedCountIncrement(sb, validatedCountVariableName, countValidationType);
                if (needsItemNullGuard)
                {
                    EmitCollectionItemNullGuard(sb);
                }
                sb.WriteLineFormat("size += {0}.GetPacketSize(item);", TypeHelper.GetGlobalTypeName(info.TypeName));
            }
            else
            {
                var validatedCountVariableName = DeclareValidatedCountVariable(sb, member, countValidationType);
                sb.WriteLineFormat("foreach(var item in {0})", collectionAccessExpression);
                using var _ = sb.BeginBlock();
                EmitValidatedCountIncrement(sb, validatedCountVariableName, countValidationType);
                if (needsItemNullGuard)
                {
                    EmitCollectionItemNullGuard(sb);
                }
                sb.WriteLineFormat("size += {0}.GetPacketSize(item);", TypeHelper.GetGlobalTypeName(info.TypeName));
            }
        }
        else if (info.IsUnmanaged)
        {
            if (RequiresCheckedCountValidation(countValidationType))
            {
                if (member.CollectionTypeInfo?.CanBeNull == true || enumerationGuard is not null)
                {
                    var condition = enumerationGuard ?? $"{collectionAccessExpression} is not null";
                    sb.WriteLineFormat("if ({0})", condition);
                    using var _ = sb.BeginBlock();
                    var validatedCountLocalName = DeclareCheckedCountLocal(
                        sb,
                        member,
                        countValidationType!,
                        GeneratorUtilities.GetCountExpressionForAccess(member, collectionAccessExpression));
                    sb.WriteLineFormat("size += {0} * sizeof({1});", validatedCountLocalName, info.TypeName);
                }
                else
                {
                    var validatedCountLocalName = DeclareCheckedCountLocal(
                        sb,
                        member,
                        countValidationType!,
                        GeneratorUtilities.GetCountExpressionForAccess(member, collectionAccessExpression));
                    sb.WriteLineFormat("size += {0} * sizeof({1});", validatedCountLocalName, info.TypeName);
                }

                return;
            }

            var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, collectionAccessExpression, true);
            sb.WriteLineFormat("size += {0} * sizeof({1});", countExpression, info.TypeName);
        }
        else if (info.IsString)
        {
            if (member.CollectionTypeInfo?.CanBeNull == true || enumerationGuard is not null)
            {
                var condition = enumerationGuard ?? $"{collectionAccessExpression} is not null";
                sb.WriteLineFormat("if ({0})", condition);
                using var _ = sb.BeginBlock();
                var validatedCountVariableName = DeclareValidatedCountVariable(sb, member, countValidationType);
                sb.WriteLineFormat("foreach(var item in {0})", collectionAccessExpression);
                using var __ = sb.BeginBlock();
                EmitValidatedCountIncrement(sb, validatedCountVariableName, countValidationType);
                sb.WriteLine("size += StringEx.MeasureSize(item);");
            }
            else
            {
                var validatedCountVariableName = DeclareValidatedCountVariable(sb, member, countValidationType);
                sb.WriteLineFormat("foreach(var item in {0})", collectionAccessExpression);
                using var _ = sb.BeginBlock();
                EmitValidatedCountIncrement(sb, validatedCountVariableName, countValidationType);
                sb.WriteLine("size += StringEx.MeasureSize(item);");
            }
        }
    }

    internal static void GenerateCollectionSizeCalculation(IndentedStringBuilder sb, MemberToGenerate member, TypeToGenerate type)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        if (collectionInfo.CountSize is > 0)
        {
            EmitFixedCollectionValidation(sb, member, collectionInfo.CountSize.Value);
        }

        var deferredCountValidationType = GetDeferredCountValidationType(member, type);
        var countValidationType = GetCollectionCountValidationType(member, type);

        if (
            !collectionInfo.Unlimited
            && (collectionInfo.CountSize is null or < 0)
            && collectionInfo.CountSizeReferenceIndex is null
        )
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
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
            AddPolymorphicSerialization(sb, member, collectionInfo, countValidationType: countValidationType);
            return;
        }

        if (GetElementInfo(member) is { } info)
        {
            GenerateStandardCollectionSizeCalculation(sb, member, info, $"obj.{member.Name}", countValidationType);
        }
    }

    internal static void GenerateMemoryOwnerSizeCalculation(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        TypeToGenerate type)
    {
        if (member.CollectionInfo is not { } collectionInfo)
        {
            return;
        }

        var countValidationType = GetCollectionCountValidationType(member, type);

        if (collectionInfo.CountSize is > 0)
        {
            sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat(
                    "throw new System.ArgumentNullException(nameof(obj.{0}), \"Fixed-size collections cannot be null.\");",
                    member.Name);
            }

            sb.WriteLineFormat("if (obj.{0}.Memory.Length != {1})", member.Name, collectionInfo.CountSize);
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat(
                    "throw new System.InvalidOperationException($\"Collection '{0}' must have a size of {1} but was {{obj.{0}.Memory.Length}}.\");",
                    member.Name,
                    collectionInfo.CountSize);
            }
        }

        if (
            !collectionInfo.Unlimited
            && (collectionInfo.CountSize is null or < 0)
            && collectionInfo.CountSizeReferenceIndex is null
        )
        {
            var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
            var countSizeExpression = TypeHelper.GetSizeOfExpression(countType);
            sb.WriteLineFormat("size += {0}; // Count size for {1}", countSizeExpression, member.Name);
        }

        if (member.CustomSerializer is { } customSerializer)
        {
            var serializerField = global::FourSer.Gen.SerializerGenerator.SanitizeTypeName(customSerializer.SerializerTypeName);
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using var _ = sb.BeginBlock();
            sb.WriteLineFormat("var span_{0} = obj.{0}.Memory.Span;", member.Name);
            var loopCountExpression = $"span_{member.Name}.Length";
            if (RequiresCheckedCountValidation(countValidationType))
            {
                loopCountExpression = DeclareCheckedCountLocal(sb, member, countValidationType!, loopCountExpression);
            }

            sb.WriteLineFormat("for (int i = 0; i < {0}; i++)", loopCountExpression);
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
            var loopCountExpression = $"span_{member.Name}.Length";
            if (RequiresCheckedCountValidation(countValidationType))
            {
                loopCountExpression = DeclareCheckedCountLocal(sb, member, countValidationType!, loopCountExpression);
            }

            sb.WriteLineFormat("for (int i = 0; i < {0}; i++)", loopCountExpression);
            using var __ = sb.BeginBlock();
            sb.WriteLineFormat("size += {0}.GetPacketSize(span_{1}[i]);", TypeHelper.GetGlobalTypeName(info.TypeName), member.Name);
        }
        else if (info.IsUnmanaged)
        {
            if (RequiresCheckedCountValidation(countValidationType))
            {
                var countLocalName = DeclareCheckedCountLocal(
                    sb,
                    member,
                    countValidationType!,
                    $"(obj.{member.Name}?.Memory.Length ?? 0)");
                sb.WriteLineFormat("size += {0} * sizeof({1});", countLocalName, info.TypeName);
            }
            else
            {
                var countExpression = $"(obj.{member.Name}?.Memory.Length ?? 0)";
                sb.WriteLineFormat("size += {0} * sizeof({1});", countExpression, info.TypeName);
            }
        }
        else if (info.IsString)
        {
            sb.WriteLineFormat("if (obj.{0} is not null)", member.Name);
            using var _ = sb.BeginBlock();
            sb.WriteLineFormat("var span_{0} = obj.{0}.Memory.Span;", member.Name);
            var loopCountExpression = $"span_{member.Name}.Length";
            if (RequiresCheckedCountValidation(countValidationType))
            {
                loopCountExpression = DeclareCheckedCountLocal(sb, member, countValidationType!, loopCountExpression);
            }

            sb.WriteLineFormat("for (int i = 0; i < {0}; i++)", loopCountExpression);
            using var __ = sb.BeginBlock();
            sb.WriteLineFormat("size += StringEx.MeasureSize(span_{0}[i]);", member.Name);
        }
    }

    private static void EmitFixedCollectionValidation(IndentedStringBuilder sb, MemberToGenerate member, int expectedCount)
    {
        sb.WriteLineFormat("if (obj.{0} is null)", member.Name);
        using (sb.BeginBlock())
        {
            sb.WriteLineFormat(
                "throw new System.ArgumentNullException(nameof(obj.{0}), \"Fixed-size collections cannot be null.\");",
                member.Name);
        }

        var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, $"obj.{member.Name}");
        sb.WriteLineFormat("if ({0} != {1})", countExpression, expectedCount);
        using (sb.BeginBlock())
        {
            sb.WriteLineFormat(
                "throw new System.InvalidOperationException($\"Collection '{0}' must have a size of {1} but was {{{2}}}.\");",
                member.Name,
                expectedCount,
                countExpression);
        }
    }

    private static void EmitCollectionItemNullGuard(IndentedStringBuilder sb)
    {
        sb.WriteLine("if (item is null)");
        using (sb.BeginBlock())
        {
            sb.WriteLine("throw new System.NullReferenceException(\"Collection item cannot be null.\");");
        }
    }

    private static void AddPolymorphicSerialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        CollectionInfo collectionInfo,
        string? collectionAccessExpression = null,
        string? countValidationType = null
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
            : null;

        if (member.CollectionTypeInfo?.CanBeNull == true || enumerationGuard is not null)
        {
            var condition = collectionAccessExpression == $"obj.{member.Name}"
                ? enumerationGuard ?? $"{collectionAccessExpression} is not null"
                : $"{collectionAccessExpression} is not null";
            sb.WriteLineFormat("if ({0})", condition);
            using var _ = sb.BeginBlock();
            EmitPolymorphicCollectionSizeBody(sb, member, collectionInfo, info, collectionAccessExpression, countValidationType);
            return;
        }

        EmitPolymorphicCollectionSizeBody(sb, member, collectionInfo, info, collectionAccessExpression, countValidationType);
    }

    private static void EmitPolymorphicCollectionSizeBody(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        CollectionInfo collectionInfo,
        PolymorphicInfo info,
        string collectionAccessExpression,
        string? countValidationType)
    {
        var discriminatorType = info.EnumUnderlyingType ?? info.TypeIdType;
        var validatedCountVariableName = DeclareValidatedCountVariable(sb, member, countValidationType);

        if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
        {
            sb.WriteLine("var hasDiscriminator = false;");
            sb.WriteLineFormat("{0} discriminator = default;", discriminatorType);
        }

        sb.WriteLineFormat("foreach (var item in {0})", collectionAccessExpression);
        using var _ = sb.BeginBlock();
        EmitValidatedCountIncrement(sb, validatedCountVariableName, countValidationType);
        EmitPolymorphicItemNullGuard(sb);

        if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
        {
            sb.WriteLineFormat(
                "size += {0}; // Size for polymorphic type id",
                PolymorphicUtilities.GenerateTypeIdSizeExpression(info));
        }
        else
        {
            var discriminatorExpression = BuildPolymorphicDiscriminatorExpression(member, info, discriminatorType);
            sb.WriteLineFormat("var itemDiscriminator = item switch {0}", discriminatorExpression);
            sb.WriteLine("if (!hasDiscriminator)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("discriminator = itemDiscriminator;");
                sb.WriteLine("hasDiscriminator = true;");
            }

            sb.WriteLine("else if (!global::System.Collections.Generic.EqualityComparer<" + discriminatorType + ">.Default.Equals(itemDiscriminator, discriminator))");
            using (sb.BeginBlock())
            {
                sb.WriteLineFormat(
                    "throw new System.IO.InvalidDataException($\"Collection '{0}' contains mixed item types. Expected discriminator {{discriminator}} but found {{itemDiscriminator}}.\");",
                    member.Name);
            }
        }

        sb.WriteLineFormat("size += item switch {0}", BuildPolymorphicSizeSwitchExpression(member, info));
    }

    private static string BuildPolymorphicDiscriminatorExpression(
        MemberToGenerate member,
        PolymorphicInfo info,
        string discriminatorType)
    {
        var arms = info.Options.Array
            .Select(option =>
                $"{PolymorphicUtilities.FormatOptionTypePattern(option)} => {GetPolymorphicDiscriminatorValue(option, info, discriminatorType)}")
            .Concat(
            [
                $"_ => throw new System.IO.InvalidDataException($\"Unknown item type in collection {member.Name}: {{item.GetType().Name}}\")",
            ]);
        return "{ " + string.Join(", ", arms) + " };";
    }

    private static string BuildPolymorphicSizeSwitchExpression(MemberToGenerate member, PolymorphicInfo info)
    {
        var arms = info.Options.Array
            .Select(option =>
            {
                var typeName = TypeHelper.GetGlobalTypeName(option.Type);
                var varName = TypeHelper.GetSimpleTypeName(option.Type).ToCamelCase();
                return $"{typeName} {varName} => {typeName}.GetPacketSize({varName})";
            })
            .Concat(
            [
                $"_ => throw new System.IO.InvalidDataException($\"Unknown item type in collection {member.Name}: {{item.GetType().Name}}\")",
            ]);
        return "{ " + string.Join(", ", arms) + " };";
    }

    private static string GetPolymorphicDiscriminatorValue(
        PolymorphicOption option,
        PolymorphicInfo info,
        string discriminatorType)
    {
        return PolymorphicUtilities.FormatTypedTypeIdValue(option.Key, info, discriminatorType);
    }

    private static void EmitPolymorphicItemNullGuard(IndentedStringBuilder sb)
    {
        sb.WriteLine("if (item is null)");
        using (sb.BeginBlock())
        {
            sb.WriteLine("throw new System.NullReferenceException(\"Item in collection cannot be null.\");");
        }
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
