using FourSer.Contracts;
using System;
using System.IO;

namespace FourSer.Tests.Behavioural.Demo
{
    public class MfcAnsiStringSerializer : ISerializer<string>
    {
        public int GetPacketSize(string obj)
        {
            return MfcStringUtils.GetMfcAnsiCStringSize(obj);
        }

        public int Serialize(string obj, Span<byte> data)
        {
            return MfcStringUtils.WriteMfcAnsiCString(data, obj);
        }

        public void Serialize(string obj, Stream stream)
        {
            MfcStringUtils.WriteMfcAnsiCString(stream, obj);
        }

        public string Deserialize(ref ReadOnlySpan<byte> data)
        {
            return MfcStringUtils.ReadMfcAnsiCString(ref data);
        }

        public string Deserialize(Stream stream)
        {
            return MfcStringUtils.ReadMfcAnsiCString(stream);
        }
    }
}
