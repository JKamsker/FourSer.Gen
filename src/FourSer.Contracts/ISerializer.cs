namespace FourSer.Contracts;

/// <summary>
/// Provides a contract for custom serializers.
/// All members are required; span-based and stream-based serialization are equal parts of the public contract.
/// Generated stream paths call the stream members directly rather than routing through the span members.
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
    /// This overload is required even when the serializer also supports stream serialization.
    /// </summary>
    int Serialize(T obj, Span<byte> data);

    /// <summary>
    /// Serializes the object into the provided stream.
    /// Generated stream serializers call this method directly.
    /// </summary>
    void Serialize(T obj, Stream stream);

    /// <summary>
    /// Deserializes an object from the provided span, advancing the span.
    /// </summary>
    T Deserialize(ref ReadOnlySpan<byte> data);

    /// <summary>
    /// Deserializes an object from the provided stream.
    /// Generated stream deserializers call this method directly.
    /// </summary>
    T Deserialize(Stream stream);
}
