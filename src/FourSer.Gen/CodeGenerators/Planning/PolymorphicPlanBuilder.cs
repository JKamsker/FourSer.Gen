using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal static class PolymorphicPlanBuilder
{
    public static PolymorphicPlan? TryCreate(MemberToGenerate member)
    {
        if (member.PolymorphicInfo is not { } info)
        {
            return null;
        }

        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        var typeIdSizeInBytes = info.TypeIdSizeInBytes ?? TypeHelper.GetSizeOf(typeIdType);
        var collectionMode = member.CollectionInfo?.PolymorphicMode ?? FourSer.Gen.PolymorphicMode.None;

        return new PolymorphicPlan(
            MemberName: member.Name,
            TypeIdType: info.TypeIdType,
            EnumUnderlyingType: info.EnumUnderlyingType,
            TypeIdProperty: info.TypeIdProperty,
            TypeIdPropertyIndex: info.TypeIdPropertyIndex,
            TypeIdSizeInBytes: typeIdSizeInBytes,
            PolymorphicMode: collectionMode,
            Options: info.Options,
            CachedDiscriminatorLocalName: null,
            HoistSingleTypeCollectionSwitch: false,
            BatchFixedHeaders: false);
    }
}
