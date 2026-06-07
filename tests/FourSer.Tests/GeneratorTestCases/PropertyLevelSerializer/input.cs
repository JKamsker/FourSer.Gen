using FourSer.Contracts;
using System;
using System.IO;

namespace FourSer.Tests.GeneratorTestCases.PropertyLevelSerializer;

public class MyCustomStringSerializer : ISerializer<string>
{
    public int GetPacketSize(string obj) => System.Text.Encoding.UTF8.GetByteCount(obj) + 1;

    public int Serialize(string obj, Span<byte> data)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(obj);
        bytes.CopyTo(data);
        data[bytes.Length] = 0; // Null terminator
        return bytes.Length + 1;
    }

    public void Serialize(string obj, Stream stream)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(obj);
        stream.Write(bytes);
        stream.WriteByte(0);
    }

    public string Deserialize(ref ReadOnlySpan<byte> data)
    {
        var nullIdx = data.IndexOf((byte)0);
        var str = System.Text.Encoding.UTF8.GetString(data.Slice(0, nullIdx));
        data = data.Slice(nullIdx + 1);
        return str;
    }

    public string Deserialize(Stream stream)
    {
        using var buffer = new MemoryStream();
        while (true)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                throw new EndOfStreamException();
            }

            if (value == 0)
            {
                return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
            }

            buffer.WriteByte((byte)value);
        }
    }
}

[GenerateSerializer]
public partial class PacketWithPropertySerializer
{
    public int Id { get; set; }

    [Serializer(typeof(MyCustomStringSerializer))]
    public string Name { get; set; }

    public string Description { get; set; } // This should use the default string serializer
}
