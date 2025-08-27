using System.Collections.Immutable;
using System.Linq;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models.Raw;
using Microsoft.CodeAnalysis;

namespace FourSer.Gen.Models;

public static class ModelTransformer
{
    private static readonly SymbolDisplayFormat s_typeNameFormat = new
    (
        SymbolDisplayGlobalNamespaceStyle.Omitted,
        SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    public static TypeToGenerate Transform(RawTypeToGenerate rawType)
    {
        var members = rawType.Members.Select(m => TransformMember(m)).ToImmutableArray();
        var nestedTypes = rawType.NestedTypes.Select(Transform).ToImmutableArray();

        var resolvedMembers = ResolveMemberReferences(members);

        var ns = rawType.TypeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : rawType.TypeSymbol.ContainingNamespace.ToDisplayString();

        return new TypeToGenerate
        (
            Name: rawType.TypeSymbol.Name,
            Namespace: ns,
            IsValueType: rawType.TypeSymbol.IsValueType,
            IsRecord: rawType.TypeSymbol.IsRecord,
            Members: new(resolvedMembers),
            NestedTypes: new(nestedTypes),
            HasSerializableBaseType: rawType.HasSerializableBaseType,
            Constructor: rawType.Constructor,
            DefaultSerializers: rawType.DefaultSerializers
        );
    }

    private static MemberToGenerate TransformMember(RawMemberToGenerate rawMember)
    {
        var memberTypeSymbol = rawMember.MemberTypeSymbol;
        var collectionTypeInfo = rawMember.RawCollectionTypeInfo is not null
            ? TransformCollectionTypeInfo(rawMember.RawCollectionTypeInfo.Value)
            : (CollectionTypeInfo?)null;

        return new MemberToGenerate
        (
            Name: rawMember.MemberSymbol.Name,
            TypeName: memberTypeSymbol.ToDisplayString(s_typeNameFormat),
            IsValueType: memberTypeSymbol.IsValueType,
            IsUnmanagedType: memberTypeSymbol.IsUnmanagedType,
            IsStringType: memberTypeSymbol.SpecialType == SpecialType.System_String,
            HasGenerateSerializerAttribute: HasGenerateSerializerAttribute(memberTypeSymbol as INamedTypeSymbol),
            IsList: rawMember.IsList,
            ListTypeArgument: rawMember.ListTypeArgument,
            CollectionInfo: rawMember.CollectionInfo,
            PolymorphicInfo: rawMember.PolymorphicInfo,
            IsCollection: rawMember.IsCollection,
            CollectionTypeInfo: collectionTypeInfo,
            IsReadOnly: rawMember.IsReadOnly,
            IsInitOnly: rawMember.IsInitOnly,
            IsCountSizeReferenceFor: null, // will be resolved later
            IsTypeIdPropertyFor: null, // will be resolved later
            CustomSerializer: rawMember.CustomSerializer
        );
    }

    private static CollectionTypeInfo TransformCollectionTypeInfo(RawCollectionTypeInfo rawInfo)
    {
        var collectionType = rawInfo.CollectionType as INamedTypeSymbol;
        var elementType = rawInfo.ElementType;

        var addMethod = "Add";
        if (collectionType is not null)
        {
            if (collectionType.IsGenericQueue()) addMethod = "Enqueue";
            if (collectionType.IsGenericStack()) addMethod = "Push";
            if (collectionType.IsGenericLinkedList()) addMethod = "AddLast";
        }

        return new CollectionTypeInfo
        (
            CollectionTypeName: rawInfo.CollectionType.ToDisplayString(s_typeNameFormat),
            ElementTypeName: elementType.ToDisplayString(s_typeNameFormat),
            IsElementUnmanagedType: elementType.IsUnmanagedType,
            IsElementStringType: elementType.SpecialType == SpecialType.System_String,
            HasElementGenerateSerializerAttribute: HasGenerateSerializerAttribute(elementType as INamedTypeSymbol),
            IsArray: rawInfo.IsArray,
            ConcreteTypeName: rawInfo.ConcreteTypeName,
            IsPureEnumerable: rawInfo.IsPureEnumerable,
            CollectionAddMethodName: addMethod,
            IsGenericIEnumerable: collectionType?.IsGenericIEnumerable() ?? false,
            IsGenericICollection: collectionType?.IsGenericICollection() ?? false,
            IsGenericIList: collectionType?.IsGenericIList() ?? false
        );
    }

    private static bool HasGenerateSerializerAttribute(INamedTypeSymbol? typeSymbol)
    {
        if (typeSymbol is null)
        {
            return false;
        }

        if (typeSymbol.GetAttributes().Any(ad => ad.AttributeClass is not null && ad.AttributeClass.IsGenerateSerializerAttribute()))
        {
            return true;
        }

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.IsISerializable())
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<MemberToGenerate> ResolveMemberReferences(ImmutableArray<MemberToGenerate> members)
    {
        var memberMap = new Dictionary<string, int>();
        for (var i = 0; i < members.Length; i++)
        {
            memberMap[members[i].Name] = i;
        }

        var newMembers = members.ToList();
        for (var i = 0; i < newMembers.Count; i++)
        {
            var member = newMembers[i];
            var newCollectionInfo = member.CollectionInfo;
            if (member.CollectionInfo is { } collectionInfo)
            {
                int? countRefIndex = null;
                if (collectionInfo.CountSizeReference is { } countRef)
                {
                    if (memberMap.TryGetValue(countRef, out var index))
                    {
                        countRefIndex = index;
                        var oldMember = newMembers[index];
                        newMembers[index] = oldMember with { IsCountSizeReferenceFor = i };
                    }
                }

                var countTypeSize = collectionInfo.CountType is null
                    ? (int?)null
                    : TypeHelper.GetSizeOf(collectionInfo.CountType);

                newCollectionInfo = collectionInfo with
                {
                    CountSizeReferenceIndex = countRefIndex,
                    CountTypeSizeInBytes = countTypeSize,
                };
            }

            var newPolymorphicInfo = member.PolymorphicInfo;
            if (member.PolymorphicInfo is { } polyInfo)
            {
                int? typeIdRefIndex = null;
                if (polyInfo.TypeIdProperty is { } typeIdRef)
                {
                    if (memberMap.TryGetValue(typeIdRef, out var index))
                    {
                        typeIdRefIndex = index;
                        var oldMember = newMembers[index];
                        newMembers[index] = oldMember with { IsTypeIdPropertyFor = i };
                    }
                }

                var typeIdSize = TypeHelper.GetSizeOf(polyInfo.TypeIdType);

                newPolymorphicInfo = polyInfo with
                {
                    TypeIdPropertyIndex = typeIdRefIndex,
                    TypeIdSizeInBytes = typeIdSize
                };
            }

            newMembers[i] = member with
            {
                CollectionInfo = newCollectionInfo,
                PolymorphicInfo = newPolymorphicInfo
            };
        }

        return newMembers.ToImmutableArray();
    }
}
