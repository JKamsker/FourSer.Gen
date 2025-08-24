using System.Collections.Immutable;
using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;

namespace FourSer.Gen.Models;

/// <summary>
/// A raw, unprocessed model describing a type to be serialized.
/// This model holds the basic information extracted from Roslyn symbols
/// before the more complex processing and refinement logic is applied.
/// </summary>
internal sealed record RawTypeToGenerate(
    string Name,
    string Namespace,
    bool IsValueType,
    bool IsRecord,
    INamedTypeSymbol TypeSymbol,
    EquatableArray<RawMemberToGenerate> Members,
    EquatableArray<RawTypeToGenerate> NestedTypes,
    bool HasSerializableBaseType);

/// <summary>
/// A raw, unprocessed model describing a member to be serialized.
/// It holds basic information and raw attribute data.
/// </summary>
internal sealed record RawMemberToGenerate(
    string Name,
    string TypeName,
    ITypeSymbol MemberTypeSymbol,
    AttributeData? CollectionAttribute,
    AttributeData? PolymorphicAttribute,
    ImmutableArray<AttributeData> PolymorphicOptions,
    bool IsReadOnly,
    bool IsInitOnly,
    Location Location);
