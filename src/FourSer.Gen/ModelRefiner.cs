using System.Collections.Immutable;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;

namespace FourSer.Gen;

/// <summary>
/// Contains the logic for the second layer of the source generation pipeline.
/// It takes the raw, unprocessed models from the <see cref="TypeInfoProvider"/>
/// and refines them into the detailed, processed models required for code generation.
/// </summary>
internal static class ModelRefiner
{
    private static readonly SymbolDisplayFormat s_typeNameFormat = new
    (
        SymbolDisplayGlobalNamespaceStyle.Omitted,
        SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    /// <summary>
    /// Refines a <see cref="RawTypeToGenerate"/> into a <see cref="TypeToGenerate"/>.
    /// </summary>
    public static TypeToGenerate Refine(RawTypeToGenerate rawType)
    {
        var members = RefineMembers(rawType.Members);
        var nestedTypes = rawType.NestedTypes.Select(Refine).ToImmutableArray();

        var constructorInfo = GetConstructorInfo(rawType.TypeSymbol, members);

        return new TypeToGenerate
        (
            rawType.Name,
            rawType.Namespace,
            rawType.IsValueType,
            rawType.IsRecord,
            new(members),
            new(nestedTypes),
            rawType.HasSerializableBaseType,
            constructorInfo
        );
    }

    private static ImmutableArray<MemberToGenerate> RefineMembers(EquatableArray<RawMemberToGenerate> rawMembers)
    {
        var members = rawMembers.Select(rawMember => CreateMemberToGenerate(rawMember, rawMembers)).ToImmutableArray();
        var resolvedMembers = ResolveMemberReferences(members);
        return resolvedMembers;
    }

    private static MemberToGenerate CreateMemberToGenerate(RawMemberToGenerate rawMember, EquatableArray<RawMemberToGenerate> allMembers)
    {
        var (isCollection, collectionTypeInfo) = GetCollectionTypeInfo(rawMember.MemberTypeSymbol);

        var isList = rawMember.MemberTypeSymbol.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>";
        ListTypeArgumentInfo? listTypeArgumentInfo = null;
        if (isCollection && collectionTypeInfo.HasValue)
        {
            var cti = collectionTypeInfo.Value;
            listTypeArgumentInfo = new ListTypeArgumentInfo
            (
                cti.ElementTypeName,
                cti.IsElementUnmanagedType,
                cti.IsElementStringType,
                cti.HasElementGenerateSerializerAttribute
            );
        }

        var memberHasGenerateSerializerAttribute = HasGenerateSerializerAttribute(rawMember.MemberTypeSymbol as INamedTypeSymbol);

        return new MemberToGenerate
        (
            rawMember.Name,
            rawMember.TypeName,
            rawMember.MemberTypeSymbol.IsUnmanagedType,
            rawMember.MemberTypeSymbol.SpecialType == SpecialType.System_String,
            memberHasGenerateSerializerAttribute,
            isList,
            listTypeArgumentInfo,
            GetCollectionInfo(rawMember),
            GetPolymorphicInfo(rawMember, allMembers),
            isCollection,
            collectionTypeInfo,
            rawMember.IsReadOnly,
            rawMember.IsInitOnly,
            null,
            null
        );
    }

    private static ImmutableArray<MemberToGenerate> ResolveMemberReferences(ImmutableArray<MemberToGenerate> members)
    {
        var memberMap = new Dictionary<string, int>();
        for (var i = 0; i < members.Length; i++)
        {
            memberMap[members[i].Name] = i;
        }

        var newMembers = members.ToBuilder();
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

                var countTypeSize = collectionInfo.CountType is null ? (int?)null : TypeHelper.GetSizeOf(collectionInfo.CountType);

                var typeIdPropertyIndex = collectionInfo.TypeIdPropertyIndex;
                if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId && collectionInfo.TypeIdProperty is { } typeIdRef)
                {
                    if (memberMap.TryGetValue(typeIdRef, out var index))
                    {
                        var oldMember = newMembers[index];
                        newMembers[index] = oldMember with { IsTypeIdPropertyFor = i };
                        typeIdPropertyIndex = index;
                    }
                }

                newCollectionInfo = collectionInfo with
                {
                    CountSizeReferenceIndex = countRefIndex,
                    CountTypeSizeInBytes = countTypeSize,
                    TypeIdPropertyIndex = typeIdPropertyIndex
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

        return newMembers.ToImmutable();
    }

    private static (bool IsCollection, CollectionTypeInfo? CollectionTypeInfo) GetCollectionTypeInfo(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
        {
            var elementType = arrayTypeSymbol.ElementType;
            var arrayElementHasGenerateSerializerAttribute = HasGenerateSerializerAttribute(elementType as INamedTypeSymbol);
            return (true, new CollectionTypeInfo(
                typeSymbol.ToDisplayString(s_typeNameFormat),
                elementType.ToDisplayString(s_typeNameFormat),
                elementType.IsUnmanagedType,
                elementType.SpecialType == SpecialType.System_String,
                arrayElementHasGenerateSerializerAttribute,
                true,
                null,
                false
            ));
        }

        if (typeSymbol is not INamedTypeSymbol namedTypeSymbol || !namedTypeSymbol.IsGenericType || namedTypeSymbol.TypeArguments.Length != 1)
        {
            return (false, null);
        }

        var originalDefinition = namedTypeSymbol.OriginalDefinition.ToDisplayString();
        var genericElementType = namedTypeSymbol.TypeArguments[0];

        var (isCollection, concreteTypeName, isPureEnumerable) = originalDefinition switch
        {
            "System.Collections.Generic.List<T>" => (true, null, false),
            "System.Collections.Generic.IList<T>" => (true, "System.Collections.Generic.List", false),
            "System.Collections.Generic.ICollection<T>" => (true, "System.Collections.Generic.List", false),
            "System.Collections.Generic.IEnumerable<T>" => (true, "System.Collections.Generic.List", true),
            "System.Collections.ObjectModel.Collection<T>" => (true, "System.Collections.ObjectModel.Collection", false),
            "System.Collections.ObjectModel.ObservableCollection<T>" => (true, "System.Collections.ObjectModel.ObservableCollection", false),
            "System.Collections.Generic.HashSet<T>" => (true, "System.Collections.Generic.HashSet", false),
            "System.Collections.Generic.SortedSet<T>" => (true, "System.Collections.Generic.SortedSet", false),
            "System.Collections.Generic.Queue<T>" => (true, "System.Collections.Generic.Queue", false),
            "System.Collections.Generic.Stack<T>" => (true, "System.Collections.Generic.Stack", false),
            "System.Collections.Generic.LinkedList<T>" => (true, "System.Collections.Generic.LinkedList", false),
            "System.Collections.Concurrent.ConcurrentBag<T>" => (true, "System.Collections.Concurrent.ConcurrentBag", false),
            _ => (false, null, false)
        };

        if (!isCollection)
        {
            return (false, null);
        }

        var hasGenerateSerializerAttribute = HasGenerateSerializerAttribute(genericElementType as INamedTypeSymbol);

        return (true, new CollectionTypeInfo(
            originalDefinition,
            genericElementType.ToDisplayString(s_typeNameFormat),
            genericElementType.IsUnmanagedType,
            genericElementType.SpecialType == SpecialType.System_String,
            hasGenerateSerializerAttribute,
            false,
            concreteTypeName,
            isPureEnumerable
        ));
    }

    private static CollectionInfo? GetCollectionInfo(RawMemberToGenerate member)
    {
        var (_, collectionTypeInfo) = GetCollectionTypeInfo(member.MemberTypeSymbol);
        var attribute = member.CollectionAttribute;

        if (attribute is null)
        {
            return collectionTypeInfo.HasValue ? new CollectionInfo(PolymorphicMode.None, null, null, null, null, null, null, null) : null;
        }

        var polymorphicMode = (PolymorphicMode)AttributeHelper.GetPolymorphicMode(attribute);
        var typeIdProperty = AttributeHelper.GetCollectionTypeIdProperty(attribute);

        if (!string.IsNullOrEmpty(typeIdProperty) && polymorphicMode == PolymorphicMode.None)
        {
            polymorphicMode = PolymorphicMode.SingleTypeId;
        }

        var countType = AttributeHelper.GetCountType(attribute)?.ToDisplayString(s_typeNameFormat);
        var countSize = AttributeHelper.GetCountSize(attribute);
        var countSizeReference = AttributeHelper.GetCountSizeReference(attribute);
        var unlimited = AttributeHelper.GetUnlimited(attribute);

        return new CollectionInfo(
            polymorphicMode,
            typeIdProperty,
            countType,
            countSize,
            countSizeReference,
            null,
            null,
            null,
            unlimited
        );
    }

    private static PolymorphicInfo? GetPolymorphicInfo(RawMemberToGenerate member, EquatableArray<RawMemberToGenerate> allMembers)
    {
        var attribute = member.PolymorphicAttribute;
        var collectionAttribute = member.CollectionAttribute;
        var options = member.PolymorphicOptions;

        var hasPolymorphicOptions = !options.IsEmpty;
        var hasPolymorphicAttribute = attribute is not null;
        var hasPolymorphicCollectionMode = collectionAttribute is not null && AttributeHelper.GetPolymorphicMode(collectionAttribute) != 0;

        if (!hasPolymorphicOptions && !hasPolymorphicAttribute && !hasPolymorphicCollectionMode)
        {
            return null;
        }

        var typeIdProperty = AttributeHelper.GetTypeIdProperty(attribute) ?? AttributeHelper.GetCollectionTypeIdProperty(collectionAttribute);
        var typeIdType = AttributeHelper.GetTypeIdType(attribute) ?? AttributeHelper.GetCollectionTypeIdType(collectionAttribute);

        if (typeIdType is null && typeIdProperty is not null)
        {
            var typeIdMember = allMembers.FirstOrDefault(m => m.Name == typeIdProperty);
            if (typeIdMember is not null)
            {
                typeIdType = typeIdMember.MemberTypeSymbol;
            }
        }

        var polymorphicOptions = GetPolymorphicOptions(options);

        var enumUnderlyingType = typeIdType is { TypeKind: TypeKind.Enum }
            ? ((INamedTypeSymbol)typeIdType).EnumUnderlyingType!.ToDisplayString()
            : null;

        return new PolymorphicInfo(
            typeIdProperty,
            typeIdType?.ToDisplayString() ?? "int",
            new(polymorphicOptions),
            enumUnderlyingType,
            null,
            null
        );
    }

    private static ImmutableArray<PolymorphicOption> GetPolymorphicOptions(ImmutableArray<AttributeData> options)
    {
        var polymorphicOptionsBuilder = ImmutableArray.CreateBuilder<PolymorphicOption>();
        foreach (var optionAttribute in options)
        {
            var (key, type) = AttributeHelper.GetPolymorphicOption(optionAttribute);
            polymorphicOptionsBuilder.Add(new(key, type.ToDisplayString()));
        }
        return polymorphicOptionsBuilder.ToImmutable();
    }

    private static ConstructorInfo? GetConstructorInfo(INamedTypeSymbol typeSymbol, ImmutableArray<MemberToGenerate> members)
    {
        var constructors = typeSymbol.Constructors.Where(c => !c.IsImplicitlyDeclared).ToList();
        var hasParameterlessCtor = constructors.Any(c => c.Parameters.Length == 0);
        var shouldGenerate = members.Any(m => m.IsReadOnly || m.IsInitOnly);

        if (!shouldGenerate)
        {
            var publicConstructors = constructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).ToList();
            if (publicConstructors.Count > 0)
            {
                var bestConstructor = FindBestConstructor(publicConstructors, members);
                if (bestConstructor is not null)
                {
                    var parameters = bestConstructor.Parameters
                        .Select(p => new ParameterInfo(p.Name, p.Type.ToDisplayString(s_typeNameFormat)))
                        .ToImmutableArray();
                    return new ConstructorInfo(new(parameters), false, hasParameterlessCtor);
                }
            }
        }

        var generatedParameters = members
            .Select(m => new ParameterInfo(m.Name, m.TypeName))
            .ToImmutableArray();
        return new ConstructorInfo(new(generatedParameters), true, hasParameterlessCtor);
    }

    private static IMethodSymbol? FindBestConstructor(List<IMethodSymbol> constructors, ImmutableArray<MemberToGenerate> members)
    {
        foreach (var c in constructors)
        {
            if (c.Parameters.Length != members.Length) continue;
            if (AllParametersMatch(c, members)) return c;
        }
        return null;
    }

    private static bool AllParametersMatch(IMethodSymbol constructor, ImmutableArray<MemberToGenerate> members)
    {
        return constructor.Parameters.All(p =>
            members.Any(m => string.Equals(m.Name, p.Name, StringComparison.OrdinalIgnoreCase) &&
                             m.TypeName == p.Type.ToDisplayString(s_typeNameFormat)));
    }

    private static bool HasGenerateSerializerAttribute(INamedTypeSymbol? typeSymbol)
    {
        return typeSymbol?.GetAttributes().Any(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute") ?? false;
    }
}
