using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;

namespace FourSer.Gen.Models.Raw;

/// <summary>
/// A "raw" model of a type to be generated, containing Roslyn symbols.
/// This is not cacheable and is intended to be transformed into a <see cref="TypeToGenerate"/>.
/// </summary>
public sealed record RawTypeToGenerate
(
    INamedTypeSymbol TypeSymbol,
    EquatableArray<RawMemberToGenerate> Members,
    EquatableArray<RawTypeToGenerate> NestedTypes,
    bool HasSerializableBaseType,
    ConstructorInfo? Constructor,
    EquatableArray<DefaultSerializerInfo> DefaultSerializers
);

/// <summary>
/// A "raw" model of a member to be generated, containing Roslyn symbols.
/// This is not cacheable and is intended to be transformed into a <see cref="MemberToGenerate"/>.
/// </summary>
public sealed record RawMemberToGenerate
(
    ISymbol MemberSymbol,
    ITypeSymbol MemberTypeSymbol,
    bool IsList,
    ListTypeArgumentInfo? ListTypeArgument,
    CollectionInfo? CollectionInfo,
    PolymorphicInfo? PolymorphicInfo,
    bool IsCollection,
    RawCollectionTypeInfo? RawCollectionTypeInfo,
    bool IsReadOnly,
    bool IsInitOnly,
    CustomSerializerInfo? CustomSerializer
);

/// <summary>
/// A "raw" model of collection type information, containing Roslyn symbols.
/// This is not cacheable and is intended to be transformed into a <see cref="CollectionTypeInfo"/>.
/// </summary>
public readonly record struct RawCollectionTypeInfo
(
    ISymbol CollectionType,
    ITypeSymbol ElementType,
    bool IsArray,
    string? ConcreteTypeName,
    bool IsPureEnumerable
);
