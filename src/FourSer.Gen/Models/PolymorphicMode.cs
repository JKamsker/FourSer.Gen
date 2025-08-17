namespace FourSer.Gen;

/// <summary>
///     Specifies the serialization mode for a polymorphic collection.
/// </summary>
public enum PolymorphicMode
{
    /// <summary>
    ///     The collection is not polymorphic.
    /// </summary>
    None,

    /// <summary>
    ///     A single TypeId is written for the entire collection. All elements must be of the same type.
    ///     The TypeId is determined by the property specified in `TypeIdProperty`.
    /// </summary>
    SingleTypeId,

    /// <summary>
    ///     Each element in the collection is prefixed with its own TypeId.
    /// </summary>
    IndividualTypeIds
}