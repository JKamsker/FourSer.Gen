using System;

namespace Serializer.Contracts;

public interface ISerializable<T> where T : ISerializable<T>
{
    static abstract int GetPacketSize(T obj);
    static abstract T Deserialize(ReadOnlySpan<byte> data);
    static abstract void Serialize(T obj, Span<byte> data);
}