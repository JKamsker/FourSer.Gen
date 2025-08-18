using System;
using System.IO;

namespace FourSer.Contracts;

public interface ISerializer<T>
{
    int GetPacketSize(T obj);
    int Serialize(T obj, Span<byte> data);
    void Serialize(T obj, Stream stream);

    T Deserialize(ref ReadOnlySpan<byte> data);
    T Deserialize(ReadOnlySpan<byte> data);
    T Deserialize(Stream stream);
}
