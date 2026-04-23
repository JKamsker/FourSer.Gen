namespace FourSer.Contracts;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SerializeCollectionAttribute : Attribute
{
    /// <summary>
    /// Defines the property type that determines the amount of items in the collection.
    /// </summary>
    public Type? CountType { get; set; }
    
    /// <summary>
    /// The amount of items in the collection.
    /// </summary>
    public int CountSize { get; set; } = -1;
    
    /// <summary>
    /// Reference to a property or field that mirrors the collection count on the wire.
    /// During serialization, the generator writes the actual count derived from the collection.
    /// During deserialization, the count read from the wire is assigned back to the referenced member.
    /// The referenced member must be of type int, byte, ushort, long, or an enum-backed integral type.
    /// </summary>
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
    /// For `SingleTypeId` mode only. The name of the property or field that mirrors the collection discriminator on the wire.
    /// During serialization, the discriminator is derived from the actual collection contents rather than this member's current value.
    /// During deserialization, the discriminator read from the wire is assigned back to the referenced member.
    /// </summary>
    public string? TypeIdProperty { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the collection should be serialized until the end of the stream.
    /// If true, no count prefix is written for the collection.
    /// </summary>
    public bool Unlimited { get; set; }
}
