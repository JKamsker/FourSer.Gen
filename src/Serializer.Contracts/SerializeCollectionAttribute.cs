using System;

namespace Serializer.Contracts;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SerializeCollectionAttribute : Attribute
{
    public Type? CountType { get; set; }
    public int CountSize { get; set; } = -1;
    public string? CountSizeReference { get; set; }

    /// <summary>
    /// Gets or sets the polymorphic serialization mode for this collection.
    /// Use `SingleTypeId` for homogeneous collections and `IndividualTypeIds` for heterogeneous collections.
    /// Defaults to `None`.
    /// </summary>
    public PolymorphicMode PolymorphicMode { get; set; } = PolymorphicMode.None;

    /// <summary>
    /// Gets or sets the type of the TypeId discriminator (e.g., typeof(byte), typeof(ushort)).
    /// This is used for both `SingleTypeId` and `IndividualTypeIds` modes.
    /// Defaults to `int`.
    /// </summary>
    public Type? TypeIdType { get; set; }

    /// <summary>
    /// For `SingleTypeId` mode only. The name of the property on the containing class that holds the TypeId for all elements in the collection.
    /// </summary>
    public string? TypeIdProperty { get; set; }
}

