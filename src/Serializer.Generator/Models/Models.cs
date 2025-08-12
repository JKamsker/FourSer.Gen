using System;
using System.Collections.Immutable;

namespace Serializer.Generator.Models;

/// <summary>
/// A model describing a type to be serialized.
/// </summary>
/// <param name="Name">The name of the type.</param>
/// <param name="Namespace">The namespace of the type.</param>
/// <param name="IsValueType">Whether the type is a value type (struct).</param>
/// <param name="Members">The list of serializable members in the type.</param>
/// <param name="NestedTypes">The list of nested types to be generated.</param>
public readonly record struct TypeToGenerate(
    string Name,
    string Namespace,
    bool IsValueType,
    EquatableArray<MemberToGenerate> Members,
    EquatableArray<TypeToGenerate> NestedTypes) : IEquatable<TypeToGenerate>;

/// <summary>
/// A model describing a member (property or field) to be serialized.
/// </summary>
/// <param name="Name">The name of the member.</param>
/// <param name="TypeName">The full name of the member's type.</param>
/// <param name="IsUnmanagedType">Whether the member's type is unmanaged.</param>
/// <param name="IsStringType">Whether the member's type is a string.</param>
/// <param name="HasGenerateSerializerAttribute">Whether the member's type has the [GenerateSerializer] attribute.</param>
/// <param name="IsList">Whether the member's type is a List&lt;T&gt;.</param>
/// <param name="ListTypeArgument">The type argument for the list, if applicable.</param>
/// <param name="CollectionInfo">Information about the collection attribute, if present.</param>
public readonly record struct MemberToGenerate(
    string Name,
    string TypeName,
    bool IsUnmanagedType,
    bool IsStringType,
    bool HasGenerateSerializerAttribute,
    bool IsList,
    string? ListTypeArgument,
    CollectionInfo? CollectionInfo) : IEquatable<MemberToGenerate>;

/// <summary>
/// A model describing the [Collection] attribute on a member.
/// </summary>
/// <param name="PolymorphicMode">The polymorphic mode for the collection.</param>
/// <param name="TypeIdProperty">The name of the property used for type discrimination.</param>
public readonly record struct CollectionInfo(
    PolymorphicMode PolymorphicMode,
    string? TypeIdProperty) : IEquatable<CollectionInfo>;
