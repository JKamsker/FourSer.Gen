using System.Collections.Immutable;
using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal static class PlanFactFactory
{
    public static PlanFacts Create(
        TypeToGenerate type,
        FourSerGeneratorOptions options,
        TargetCapabilities capabilities)
    {
        var constructionPlan = ConstructionPlanner.CreatePlan(type);
        var memberFacts = type.Members
            .Select((member, index) => CreateMemberFacts(member, index, type))
            .ToImmutableArray();

        return new PlanFacts(
            type,
            options,
            capabilities,
            constructionPlan,
            memberFacts);
    }

    private static MemberPlanFacts CreateMemberFacts(
        MemberToGenerate member,
        int index,
        TypeToGenerate type)
    {
        var resolvedSerializer = GeneratorUtilities.ResolveSerializer(member, type);
        var collectionPlan = member.IsMemoryOwner
            ? MemoryOwnerPlanBuilder.TryCreate(member)
            : CollectionPlanBuilder.TryCreate(member);
        var polymorphicPlan = PolymorphicPlanBuilder.TryCreate(member);

        var isScalarFixedSize =
            !member.IsCollection
            && !member.IsList
            && !member.IsMemoryOwner
            && member.IsUnmanagedType
            && !member.IsStringType
            && !member.HasGenerateSerializerAttribute
            && member.PolymorphicInfo is null
            && resolvedSerializer is null;

        var fixedSizeBytes = isScalarFixedSize
            ? TypeHelper.GetSizeOf(member.TypeName)
            : (int?)null;

        var countTypeName = GetCountTypeName(member, type);
        var countHeaderSizeBytes = countTypeName is not null
            ? TypeHelper.GetSizeOf(countTypeName)
            : (int?)null;
        var fixedCount = member.CollectionInfo?.CountSize;

        return new MemberPlanFacts(
            Index: index,
            Name: member.Name,
            TypeName: member.TypeName,
            SimpleTypeName: TypeHelper.GetSimpleTypeName(member.TypeName),
            IsScalarFixedSize: isScalarFixedSize,
            FixedSizeBytes: fixedSizeBytes,
            ResolvedSerializerTypeName: resolvedSerializer?.TypeName,
            ResolvedSerializerFieldName: resolvedSerializer?.FieldName,
            HasCountReference: member.CollectionInfo?.CountSizeReferenceIndex is not null,
            HasEncodedCount: HasEncodedCount(member),
            HasFixedCount: fixedCount is not null && fixedCount >= 0,
            CountTypeName: countTypeName,
            CountHeaderSizeBytes: countHeaderSizeBytes,
            FixedCount: fixedCount,
            ByteExactEligible: IsByteExactEligible(member, collectionPlan),
            PortablePrimitiveEligible: IsPortablePrimitiveEligible(member, collectionPlan),
            NativeLayoutEligible: IsNativeLayoutEligible(member, collectionPlan),
            CollectionPlan: collectionPlan,
            PolymorphicPlan: polymorphicPlan);
    }

    private static bool HasEncodedCount(MemberToGenerate member)
    {
        if (member.CollectionInfo is not { } collectionInfo || collectionInfo.Unlimited)
        {
            return false;
        }

        return collectionInfo.CountSize is null or < 0
            && collectionInfo.CountSizeReferenceIndex is null;
    }

    private static string? GetCountTypeName(MemberToGenerate member, TypeToGenerate type)
    {
        if (member.CollectionInfo is not { } collectionInfo || collectionInfo.Unlimited)
        {
            return null;
        }

        if (collectionInfo.CountSizeReferenceIndex is { } countReferenceIndex)
        {
            return type.Members[countReferenceIndex].TypeName;
        }

        if (collectionInfo.CountSize is not null && collectionInfo.CountSize >= 0)
        {
            return null;
        }

        return collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
    }

    private static bool IsByteExactEligible(MemberToGenerate member, CollectionPlan? collectionPlan)
    {
        if (collectionPlan is not { } plan)
        {
            return member.TypeName == "byte";
        }

        return TypeHelper.IsByteCollection(plan.ElementTypeName)
            && plan.CustomSerializer is null
            && !plan.ElementHasGenerateSerializerAttribute;
    }

    private static bool IsPortablePrimitiveEligible(MemberToGenerate member, CollectionPlan? collectionPlan)
    {
        if (collectionPlan is not { } plan)
        {
            return false;
        }

        return plan.CustomSerializer is null
            && (plan.ElementIsUnmanagedType || plan.ElementBulkLayoutSafe)
            && !plan.ElementIsStringType
            && (!plan.ElementHasGenerateSerializerAttribute || plan.ElementBulkLayoutSafe)
            && plan.ElementFixedSizeBytes is not null
            && plan.ElementTypeName is not "bool" and not "decimal";
    }

    private static bool IsNativeLayoutEligible(MemberToGenerate member, CollectionPlan? collectionPlan)
    {
        if (collectionPlan is not { } plan)
        {
            return false;
        }

        return IsPortablePrimitiveEligible(member, collectionPlan)
            && plan.ElementTypeName is not "bool" and not "decimal";
    }
}
