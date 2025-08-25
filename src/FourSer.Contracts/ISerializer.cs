namespace FourSer.Contracts;

/// <summary>
/// Provides a contract for custom serializers.
/// </summary>
/// <typeparam name="T">The type to be serialized and deserialized.</typeparam>
public interface ISerializer<T>
{
    /// <summary>
    /// Calculates the total size in bytes required to serialize the object.
    /// </summary>
    int GetPacketSize(T obj);

    /// <summary>
    /// Serializes the object into the provided span.
    /// </summary>
    int Serialize(T obj, Span<byte> data);

    /// <summary>
    /// Serializes the object into the provided stream.
    /// </summary>
    void Serialize(T obj, Stream stream);

    /// <summary>
    /// Deserializes an object from the provided span, advancing the span.
    /// </summary>
    T Deserialize(ref ReadOnlySpan<byte> data);

    /// <summary>
    /// Deserializes an object from the provided stream.
    /// </summary>
    T Deserialize(Stream stream);
}
