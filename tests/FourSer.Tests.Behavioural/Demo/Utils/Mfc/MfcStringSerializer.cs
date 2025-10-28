using FourSer.Contracts;
using System.Text;
using System;
using System.IO;

namespace FourSer.Tests.Behavioural.Demo
{
    public class MfcStringSerializer : ISerializer<string>
    {
        public int GetPacketSize(string obj)
        {
            return MfcStringUtils.GetMfcUnicodeCStringSize(obj);
        }

        public int Serialize(string obj, Span<byte> data)
        {
            return MfcStringUtils.WriteMfcUnicodeCString(data, obj);
        }

        public void Serialize(string obj, Stream stream)
        {
            MfcStringUtils.WriteMfcUnicodeCString(stream, obj);
        }

        public string Deserialize(ref ReadOnlySpan<byte> data)
        {
            return MfcStringUtils.ReadMfcUnicodeCString(ref data);
        }

        public string Deserialize(Stream stream)
        {
            var rawValue = MfcStringUtils.ReadMfcUnicodeCStringRaw(stream);
            return Encoding.Unicode.GetString(rawValue);
        }
    }
}
