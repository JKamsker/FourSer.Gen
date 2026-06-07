namespace FourSer.Contracts;

/// <summary>
/// Specifies the serialization mode for a polymorphic collection.
/// </summary>
public enum PolymorphicMode
{
    /// <summary>
    /// The collection is not polymorphic.
    /// </summary>
    None,
    /// <summary>
    /// A single TypeId is written for the entire collection. All elements must be of the same type.
    /// When `SerializeCollectionAttribute.TypeIdProperty` is configured, serialization derives the discriminator from the collection contents,
    /// and deserialization restores the wire discriminator into the referenced member.
    /// </summary>
    SingleTypeId,
    /// <summary>
    /// Each element in the collection is prefixed with its own TypeId.
    /// </summary>
    IndividualTypeIds
}
