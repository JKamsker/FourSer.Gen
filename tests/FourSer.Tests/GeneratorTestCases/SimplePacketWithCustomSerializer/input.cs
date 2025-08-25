using FourSer.Contracts;
using System;
using System.IO;

namespace FourSer.Tests.GeneratorTestCases.SimplePacketWithCustomSerializer;

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
    public void Serialize(string obj, Stream stream) { /* not needed for this test */ }
    public string Deserialize(ref ReadOnlySpan<byte> data)
    {
        var nullIdx = data.IndexOf((byte)0);
        var str = System.Text.Encoding.UTF8.GetString(data.Slice(0, nullIdx));
        data = data.Slice(nullIdx + 1);
        return str;
    }
    public string Deserialize(Stream stream) => "";
}

[GenerateSerializer]
[DefaultSerializer(typeof(string), typeof(MyCustomStringSerializer))]
public partial class SimplePacket
{
    public int Id { get; set; }
    public string Name { get; set; }
}
