using System;
using System.IO;

namespace FourSer.Contracts;

public interface ISerializable<T> where T : ISerializable<T>
{
    static abstract int GetPacketSize(T obj);
    static abstract int Serialize(T obj, Span<byte> data);
    static abstract int Serialize(T obj, Stream stream);

    static abstract T Deserialize(ref ReadOnlySpan<byte> data);
    static abstract T Deserialize(ReadOnlySpan<byte> data);
    static abstract T Deserialize(Stream stream);
}