using FourSer.Contracts;
using System.Text;
using FourSer.Gen.Helpers;
using System.IO;
using System;

namespace FourSer.Tests.Behavioural.Demo;

public class MfcStringSerializer : ISerializer<string>
{
    public int GetPacketSize(string obj)
    {
        return StringEx.MeasureSize(obj);
    }

    public int Serialize(string obj, Span<byte> data)
    {
        var origLen = data.Length;
        StringEx.WriteString(ref data, obj);
        return origLen - data.Length;
    }

    public void Serialize(string obj, Stream stream)
    {
        StreamWriterHelpers.WriteString(stream, obj);
    }

    public string Deserialize(ref ReadOnlySpan<byte> data)
    {
        return StringEx.ReadString(ref data);
    }

    public string Deserialize(Stream stream)
    {
        var rawValue = MfcStringUtils.ReadMfcUnicodeCStringRaw(stream);
        return Encoding.Unicode.GetString(rawValue);
    }
}
