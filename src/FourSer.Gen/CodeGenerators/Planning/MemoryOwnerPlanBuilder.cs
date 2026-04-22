using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal static class MemoryOwnerPlanBuilder
{
    public static CollectionPlan? TryCreate(MemberToGenerate member)
    {
        if (!member.IsMemoryOwner || member.MemoryOwnerTypeInfo is not { } memoryOwnerTypeInfo || member.CollectionInfo is not { } collectionInfo)
        {
            return null;
        }

        return new CollectionPlan(
            MemberName: member.Name,
            CollectionTypeName: member.TypeName,
            ElementTypeName: memoryOwnerTypeInfo.ElementTypeName,
            IsMemoryOwner: true,
            CanBeNull: true,
            IsArray: false,
            IsList: false,
            SupportsIndexing: true,
            IsPureEnumerable: false,
            IsGenericList: false,
            IsReadOnlyInterface: false,
            ConcreteTypeName: null,
            RangeFactoryTypeName: null,
            CountPropertyName: "Length",
            CollectionAddMethod: null,
            ElementIsValueType: false,
            ElementIsUnmanagedType: memoryOwnerTypeInfo.IsElementUnmanagedType,
            ElementIsStringType: memoryOwnerTypeInfo.IsElementStringType,
            ElementHasGenerateSerializerAttribute: memoryOwnerTypeInfo.HasElementGenerateSerializerAttribute,
            ElementRequiresDisposal: false,
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
