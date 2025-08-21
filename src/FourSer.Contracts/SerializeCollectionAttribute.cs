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
    /// Reference to a property that contains the size of the collection.
    /// The referenced collection must be of type int, byte, ushort, long or an Enum.
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
    /// For `SingleTypeId` mode only. The name of the property on the containing class that holds the TypeId for all elements in the collection.
    /// </summary>
    public string? TypeIdProperty { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the collection should be serialized until the end of the stream.
    /// If true, no count prefix is written for the collection.
    /// </summary>
    public bool Unlimited { get; set; }
}

