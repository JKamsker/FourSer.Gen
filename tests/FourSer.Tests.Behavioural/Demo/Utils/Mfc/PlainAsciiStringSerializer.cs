using FourSer.Contracts;
using System;
using System.IO;
using System.Text;

namespace FourSer.Tests.Behavioural.Demo
{
    public class PlainAsciiStringSerializer : ISerializer<string>
    {
        private static readonly Encoding Ascii = Encoding.ASCII;

        public int GetPacketSize(string obj)
        {
            return Ascii.GetByteCount(obj ?? string.Empty);
        }

        public int Serialize(string obj, Span<byte> data)
        {
            string value = obj ?? string.Empty;
            return Ascii.GetBytes(value.AsSpan(), data);
        }

        public void Serialize(string obj, Stream stream)
        {
            string value = obj ?? string.Empty;
            if (value.Length == 0)
            {
                return;
            }

            var bytes = Ascii.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        public string Deserialize(ref ReadOnlySpan<byte> data)
        {
            var result = Ascii.GetString(data);
            data = ReadOnlySpan<byte>.Empty;
            return result;
        }

        public string Deserialize(Stream stream)
        {
            using var reader = new StreamReader(stream, Ascii, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            return reader.ReadToEnd();
        }
    }
}
