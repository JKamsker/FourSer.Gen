using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal static class CollectionPlanBuilder
{
    public static CollectionPlan? TryCreate(MemberToGenerate member)
    {
        if (!member.IsCollection || member.CollectionTypeInfo is not { } collectionTypeInfo || member.CollectionInfo is not { } collectionInfo)
        {
            return null;
        }

        return new CollectionPlan(
            MemberName: member.Name,
            CollectionTypeName: member.TypeName,
            ElementTypeName: collectionTypeInfo.ElementTypeName,
            IsMemoryOwner: false,
            CanBeNull: collectionTypeInfo.CanBeNull,
            IsArray: collectionTypeInfo.IsArray,
            IsList: member.IsList,
            SupportsIndexing: collectionTypeInfo.SupportsIndexing,
            IsPureEnumerable: collectionTypeInfo.IsPureEnumerable,
            IsGenericList: collectionTypeInfo.IsGenericList,
            IsReadOnlyInterface: collectionTypeInfo.IsReadOnlyInterface,
            ConcreteTypeName: collectionTypeInfo.ConcreteTypeName,
            RangeFactoryTypeName: collectionTypeInfo.RangeFactoryTypeName,
            CountPropertyName: collectionTypeInfo.CountPropertyName,
            CollectionAddMethod: collectionTypeInfo.CollectionAddMethod,
            ElementIsValueType: collectionTypeInfo.IsElementValueType,
            ElementIsUnmanagedType: collectionTypeInfo.IsElementUnmanagedType,
            ElementIsStringType: collectionTypeInfo.IsElementStringType,
            ElementHasGenerateSerializerAttribute: collectionTypeInfo.HasElementGenerateSerializerAttribute,
            ElementRequiresDisposal: collectionTypeInfo.ElementRequiresDisposal,
            CollectionInfo: collectionInfo,
            CustomSerializer: member.CustomSerializer,
            BulkLayoutMode: BulkLayoutMode.None,
            UsePortableFallback: false,
            UseListSetCount: false,
            CachedCountLocalName: null,
            CachedByteCountLocalName: null,
            CachedDiscriminatorLocalName: null);
    }
}
