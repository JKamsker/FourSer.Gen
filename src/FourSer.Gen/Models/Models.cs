using Microsoft.CodeAnalysis;

namespace FourSer.Gen.Models;

/// <summary>
///     A model describing a type to be serialized.
/// </summary>
/// <param name="Name">The name of the type.</param>
/// <param name="Namespace">The namespace of the type.</param>
/// <param name="IsValueType">Whether the type is a value type (struct).</param>
/// <param name="Members">The list of serializable members in the type.</param>
/// <param name="NestedTypes">The list of nested types to be generated.</param>
/// <param name="HasSerializableBaseType">Whether the type has a base type with the [GenerateSerializer] attribute.</param>
public sealed record TypeToGenerate
(
    string Name,
    string Namespace,
    bool IsValueType,
    bool IsRecord,
    EquatableArray<MemberToGenerate> Members,
    EquatableArray<TypeToGenerate> NestedTypes,
    bool HasSerializableBaseType,
    ConstructorInfo? Constructor,
    EquatableArray<DefaultSerializerInfo> DefaultSerializers
) : IEquatable<TypeToGenerate>;

/// <summary>
///     A model describing a constructor parameter.
/// </summary>
/// <param name="Name">The name of the parameter.</param>
/// <param name="TypeName">The full name of the parameter's type.</param>
public sealed record ParameterInfo
(
    string Name,
    string TypeName
);

/// <summary>
///     A model describing a constructor.
/// </summary>
/// <param name="Parameters">The list of parameters for the constructor.</param>
/// <param name="ShouldGenerate">Whether the constructor should be generated.</param>
public sealed record ConstructorInfo
(
    EquatableArray<ParameterInfo> Parameters,
    bool ShouldGenerate,
    bool HasParameterlessConstructor
) : IEquatable<ConstructorInfo>;

/// <summary>
///     A model describing a member (property or field) to be serialized.
/// </summary>
/// <param name="Name">The name of the member.</param>
/// <param name="TypeName">The full name of the member's type.</param>
/// <param name="IsUnmanagedType">Whether the member's type is unmanaged.</param>
/// <param name="IsStringType">Whether the member's type is a string.</param>
/// <param name="HasGenerateSerializerAttribute">Whether the member's type has the [GenerateSerializer] attribute.</param>
/// <param name="IsList">Whether the member's type is a List&lt;T&gt;.</param>
/// <param name="ListTypeArgument">Information about the list's type argument, if applicable.</param>
/// <param name="CollectionInfo">Information about the collection attribute, if present.</param>
/// <param name="IsCollection">Whether the member's type is any supported collection type.</param>
/// <param name="CollectionTypeInfo">Information about the collection type, if applicable.</param>
public sealed record MemberToGenerate
(
    string Name,
    string TypeName,
    bool IsValueType,
    bool IsUnmanagedType,
    bool IsStringType,
    bool HasGenerateSerializerAttribute,
    bool IsList,
    ListTypeArgumentInfo? ListTypeArgument,
    CollectionInfo? CollectionInfo,
    PolymorphicInfo? PolymorphicInfo,
    bool IsCollection,
    CollectionTypeInfo? CollectionTypeInfo,
    bool IsReadOnly,
    bool IsInitOnly,
    int? IsCountSizeReferenceFor,
    int? IsTypeIdPropertyFor,
    CustomSerializerInfo? CustomSerializer
);

/// <summary>
///     A model describing the [Serializer] attribute on a member.
/// </summary>
/// <param name="SerializerTypeName">The full name of the serializer type.</param>
public readonly record struct CustomSerializerInfo(string SerializerTypeName);

/// <summary>
///     A model describing the [DefaultSerializer] attribute on a type.
/// </summary>
/// <param name="TargetTypeName">The full name of the type to be serialized.</param>
/// <param name="SerializerTypeName">The full name of the serializer type.</param>
public readonly record struct DefaultSerializerInfo(string TargetTypeName, string SerializerTypeName);

/// <summary>
///     A model describing the type argument of a List&lt;T&gt;.
/// </summary>
/// <param name="TypeName">The name of the type argument.</param>
/// <param name="IsUnmanagedType">Whether the type is unmanaged.</param>
/// <param name="IsStringType">Whether the type is a string.</param>
/// <param name="HasGenerateSerializerAttribute">Whether the type has the [GenerateSerializer] attribute.</param>
public readonly record struct ListTypeArgumentInfo
(
    string TypeName,
    bool IsUnmanagedType,
    bool IsStringType,
    bool HasGenerateSerializerAttribute
);

/// <summary>
///     A model describing information about a collection type.
/// </summary>
/// <param name="CollectionTypeName">
///     The full name of the collection type (e.g., "System.Collections.Generic.List<T>").
/// </param>
/// <param name="ElementTypeName">The name of the element type.</param>
/// <param name="IsElementUnmanagedType">Whether the element type is unmanaged.</param>
/// <param name="IsElementStringType">Whether the element type is a string.</param>
/// <param name="HasElementGenerateSerializerAttribute">Whether the element type has the [GenerateSerializer] attribute.</param>
/// <param name="IsArray">Whether this is an array type.</param>
/// <param name="ConcreteTypeName">
///     The concrete type to instantiate for interfaces (e.g., "List<T>" for "ICollection<T>").
/// </param>
public readonly record struct CollectionTypeInfo
(
    string CollectionTypeName,
    string ElementTypeName,
    bool IsElementUnmanagedType,
    bool IsElementStringType,
    bool HasElementGenerateSerializerAttribute,
    bool IsArray,
    string? ConcreteTypeName,
    bool IsPureEnumerable
);

/// <summary>
///     A model describing the [Collection] attribute on a member.
/// </summary>
/// <param name="PolymorphicMode">The polymorphic mode for the collection.</param>
/// <param name="TypeIdProperty">The name of the property used for type discrimination.</param>
/// <param name="CountType">The type to use for the collection count (e.g., "byte", "ushort", "int").</param>
/// <param name="CountSize">The size in bytes for the collection count.</param>
/// <param name="CountSizeReference">The name of a property/field that contains the count.</param>
/// <param name="Unlimited">Whether the collection is unlimited in size.</param>
public readonly record struct CollectionInfo
(
    PolymorphicMode PolymorphicMode,
    string? TypeIdProperty,
    string? CountType,
    int? CountSize,
    string? CountSizeReference,
    int? CountSizeReferenceIndex,
    int? CountTypeSizeInBytes,
    bool Unlimited = false
);

/// <summary>
///     A model describing a polymorphic option.
/// </summary>
/// <param name="Key">The key used to identify the type.</param>
/// <param name="Type">The type associated with the key.</param>
public readonly record struct PolymorphicOption
(
    string Key,
    string Type
);

/// <summary>
///     A model describing the [SerializePolymorphic] attribute on a member.
/// </summary>
/// <param name="TypeIdProperty">The name of the property used for type discrimination.</param>
/// <param name="TypeIdType">The type of the TypeId property.</param>
/// <param name="Options">The list of polymorphic options.</param>
public readonly record struct PolymorphicInfo
(
    string? TypeIdProperty,
    string TypeIdType,
    EquatableArray<PolymorphicOption> Options,
    string? EnumUnderlyingType,
    int? TypeIdPropertyIndex,
    int? TypeIdSizeInBytes
);