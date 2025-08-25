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
        return MfcStringUtils.GetMfcUnicodeCStringSize(obj);
    }

    public int Serialize(string obj, Span<byte> data)
    {
        using var memoryStream = new MemoryStream();
        MfcStringUtils.WriteMfcUnicodeCString(memoryStream, obj);
        var serializedData = memoryStream.ToArray();
        
        if (serializedData.Length > data.Length)
        {
            throw new ArgumentException("Insufficient buffer space for MFC string serialization.");
        }
        
        serializedData.CopyTo(data);
        return serializedData.Length;
    }

    public void Serialize(string obj, Stream stream)
    {
        MfcStringUtils.WriteMfcUnicodeCString(stream, obj);
    }

    public string Deserialize(ref ReadOnlySpan<byte> data)
    {
        using var memoryStream = new MemoryStream(data.ToArray());
        var result = Deserialize(memoryStream);
        
        // Update the span to reflect consumed bytes
        int consumedBytes = (int)memoryStream.Position;
        data = data.Slice(consumedBytes);
        
        return result;
    }

    public string Deserialize(Stream stream)
    {
        var rawValue = MfcStringUtils.ReadMfcUnicodeCStringRaw(stream);
        return Encoding.Unicode.GetString(rawValue);
    }
}
