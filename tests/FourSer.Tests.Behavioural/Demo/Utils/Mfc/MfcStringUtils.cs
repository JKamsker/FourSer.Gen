using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace FourSer.Tests.Behavioural.Demo
{
    public static class MfcStringUtils
    {
        private static readonly Encoding AnsiEncoding;

        static MfcStringUtils()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            AnsiEncoding = Encoding.GetEncoding(1250);
        }

        public static string ReadMfcUnicodeCString(ref ReadOnlySpan<byte> data)
        {
            int charCount = ReadUnicodeLength(ref data);
            if (charCount < 0)
            {
                throw new InvalidDataException("Invalid negative length for string.");
            }

            int byteCount = checked(charCount * 2);

            if (data.Length < byteCount)
            {
                throw new InvalidDataException("Insufficient data for Unicode string content.");
            }

            var content = data.Slice(0, byteCount);
            data = data.Slice(byteCount);

            var result = Encoding.Unicode.GetString(content);
            return result;
        }

        public static string ReadMfcAnsiCString(ref ReadOnlySpan<byte> data)
        {
            int charCount = ReadMfcCount(ref data);
            if (charCount < 0)
            {
                throw new InvalidDataException("Invalid negative length for ANSI string.");
            }

            if (data.Length < charCount)
            {
                throw new InvalidDataException("Insufficient data for ANSI string content.");
            }

            var content = data.Slice(0, charCount);
            data = data.Slice(charCount);

            return AnsiEncoding.GetString(content);
        }

        private static int ReadMfcCount(ref ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty)
            {
                throw new EndOfStreamException();
            }

            byte firstByte = data[0];
            data = data.Slice(1);

            if (firstByte != 0xFF)
            {
                return firstByte;
            }

            if (data.Length < 2)
            {
                throw new EndOfStreamException();
            }

            ushort word = BinaryPrimitives.ReadUInt16LittleEndian(data);
            data = data.Slice(2);

            if (word != 0xFFFF)
            {
                return word;
            }

            if (data.Length < 4)
            {
                throw new EndOfStreamException();
            }

            uint dword = BinaryPrimitives.ReadUInt32LittleEndian(data);
            data = data.Slice(4);

            return checked((int)dword);
        }

        public static int WriteMfcUnicodeCString(Span<byte> buffer, string value)
        {
            string safeValue = value ?? string.Empty;
            int charCount = safeValue.Length;

            int prefixSize = WriteUnicodeLength(buffer, charCount);
            buffer = buffer.Slice(prefixSize);

            int bytesWritten = Encoding.Unicode.GetBytes(safeValue.AsSpan(), buffer);
            buffer = buffer.Slice(bytesWritten);

            return prefixSize + bytesWritten;
        }

        public static int WriteMfcAnsiCString(Span<byte> buffer, string? value)
        {
            string safeValue = value ?? string.Empty;
            int byteCount = AnsiEncoding.GetByteCount(safeValue);

            int prefixSize = WriteMfcCount(buffer, byteCount);
            buffer = buffer.Slice(prefixSize);

            int bytesWritten = AnsiEncoding.GetBytes(safeValue.AsSpan(), buffer);
            return prefixSize + bytesWritten;
        }
    
        private static int WriteMfcCount(Span<byte> buffer, int charCount)
        {
            if (charCount < 0)
            {
                throw new ArgumentException("Character count cannot be negative.", nameof(charCount));
            }

            if (charCount < 0xFF)
            {
                buffer[0] = (byte)charCount;
                return 1;
            }

            if (charCount <= 0xFFFE)
            {
                buffer[0] = 0xFF;
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1), (ushort)charCount);
                return 3;
            }

            buffer[0] = 0xFF;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1), 0xFFFF);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(3), (uint)charCount);
            return 7;
        }

        private static int WriteUnicodeLength(Span<byte> buffer, int charCount)
        {
            if (charCount < 0)
            {
                throw new ArgumentException("Character count cannot be negative.", nameof(charCount));
            }

            if (charCount <= 0xFF)
            {
                buffer[0] = 0xFF;
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1), 0xFFFE);
                buffer[3] = (byte)charCount;
                return 4;
            }

            buffer[0] = 0xFF;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1), 0xFFFF);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(3), (uint)charCount);
            return 7;
        }

        public static int GetMfcUnicodeCStringSize(string value)
        {
            int charCount = (value ?? string.Empty).Length;
            int lengthPrefixSize = GetUnicodeLengthPrefixSize(charCount);
            int stringDataSize = charCount * 2; // UTF-16LE characters

            return lengthPrefixSize + stringDataSize;
        }

        public static int GetMfcAnsiCStringSize(string? value)
        {
            string safeValue = value ?? string.Empty;
            int byteCount = AnsiEncoding.GetByteCount(safeValue);
            int lengthPrefixSize = GetMfcCountSize(byteCount);
            return lengthPrefixSize + byteCount;
        }

        private static int GetMfcCountSize(int count)
        {
            if (count < 0)
            {
                throw new ArgumentException("Count cannot be negative.", nameof(count));
            }

            if (count < 0xFF)
            {
                return 1;
            }

            if (count <= 0xFFFE)
            {
                return 3;
            }

            return 7;
        }

        private static int GetUnicodeLengthPrefixSize(int charCount)
        {
            if (charCount <= 0xFF)
            {
                return 4;
            }

            return 7;
        }
    
        #region Stream-based methods (legacy)
    
        public static byte[] ReadMfcUnicodeCStringRaw(Stream stream)
        {
            int charCount = ReadUnicodeLength(stream);
            if (charCount < 0)
            {
                throw new InvalidDataException("Invalid negative length for string.");
            }

            int byteCount = checked(charCount * 2);
            byte[] data = new byte[byteCount];
            if (byteCount > 0)
            {
                stream.ReadExactly(data);
            }

            return data;
        }

        private static int ReadMfcCount(Stream stream)
        {
            int firstByte = stream.ReadByte();
            if (firstByte == -1) throw new EndOfStreamException();

            if (firstByte != 0xFF)
            {
                return firstByte;
            }

            Span<byte> buffer = stackalloc byte[4];
            stream.ReadExactly(buffer.Slice(0, 2));
            ushort word = BitConverter.ToUInt16(buffer);
            if (word != 0xFFFF)
            {
                return word;
            }

            stream.ReadExactly(buffer.Slice(0, 4));
            uint dword = BitConverter.ToUInt32(buffer);
            return checked((int)dword);
        }

        private static int ReadUnicodeLength(ref ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty)
            {
                throw new EndOfStreamException();
            }

            byte firstByte = data[0];
            data = data.Slice(1);

            if (firstByte != 0xFF)
            {
                return firstByte;
            }

            if (data.Length < 2)
            {
                throw new EndOfStreamException();
            }

            ushort marker = BinaryPrimitives.ReadUInt16LittleEndian(data);
            data = data.Slice(2);

            if (marker == 0xFFFE)
            {
                if (data.IsEmpty)
                {
                    throw new EndOfStreamException();
                }

                int shortLength = data[0];
                data = data.Slice(1);
                return shortLength;
            }

            if (marker == 0xFFFF)
            {
                if (data.Length < 4)
                {
                    throw new EndOfStreamException();
                }

                uint length = BinaryPrimitives.ReadUInt32LittleEndian(data);
                data = data.Slice(4);
                return checked((int)length);
            }

            return marker;
        }

        private static int ReadUnicodeLength(Stream stream)
        {
            int firstByte = stream.ReadByte();
            if (firstByte == -1)
            {
                throw new EndOfStreamException();
            }

            if (firstByte != 0xFF)
            {
                return firstByte;
            }

            Span<byte> buffer = stackalloc byte[4];
            stream.ReadExactly(buffer.Slice(0, 2));
            ushort marker = BinaryPrimitives.ReadUInt16LittleEndian(buffer);

            if (marker == 0xFFFE)
            {
                int lengthByte = stream.ReadByte();
                if (lengthByte == -1)
                {
                    throw new EndOfStreamException();
                }

                return lengthByte;
            }

            if (marker == 0xFFFF)
            {
                stream.ReadExactly(buffer);
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
                return checked((int)length);
            }

            return marker;
        }


        public static void WriteMfcUnicodeCString(Stream stream, string value)
        {
            string safeValue = value ?? string.Empty;
            int charCount = safeValue.Length;

            WriteUnicodeLength(stream, charCount);

            if (charCount > 0)
            {
                byte[] utf16Bytes = Encoding.Unicode.GetBytes(safeValue);
                stream.Write(utf16Bytes, 0, utf16Bytes.Length);
            }
        }

        public static string ReadMfcAnsiCString(Stream stream)
        {
            int byteCount = ReadMfcCount(stream);
            if (byteCount < 0)
            {
                throw new InvalidDataException("Invalid negative length for ANSI string.");
            }

            byte[] data = new byte[byteCount];
            if (byteCount > 0)
            {
                stream.ReadExactly(data);
            }

            return AnsiEncoding.GetString(data);
        }

        public static void WriteMfcAnsiCString(Stream stream, string? value)
        {
            string safeValue = value ?? string.Empty;
            int byteCount = AnsiEncoding.GetByteCount(safeValue);

            WriteMfcCount(stream, byteCount);

            if (byteCount > 0)
            {
                byte[] data = AnsiEncoding.GetBytes(safeValue);
                stream.Write(data, 0, data.Length);
            }
        }

        private static void WriteMfcCount(Stream stream, int count)
        {
            if (count < 0)
            {
                throw new ArgumentException("Character count cannot be negative.", nameof(count));
            }

            if (count < 0xFF)
            {
                stream.WriteByte((byte)count);
                return;
            }

            stream.WriteByte(0xFF);

            if (count <= 0xFFFE)
            {
                Span<byte> wordBuffer = stackalloc byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(wordBuffer, (ushort)count);
                stream.Write(wordBuffer);
                return;
            }

            Span<byte> sentinelBuffer = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(sentinelBuffer, 0xFFFF);
            stream.Write(sentinelBuffer);

            Span<byte> dwordBuffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(dwordBuffer, (uint)count);
            stream.Write(dwordBuffer);
        }

        private static void WriteUnicodeLength(Stream stream, int charCount)
        {
            if (charCount < 0)
            {
                throw new ArgumentException("Character count cannot be negative.", nameof(charCount));
            }

            if (charCount <= 0xFF)
            {
                stream.WriteByte(0xFF);
                Span<byte> buffer = stackalloc byte[3];
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, 0xFFFE);
                buffer[2] = (byte)charCount;
                stream.Write(buffer);
                return;
            }

            stream.WriteByte(0xFF);
            Span<byte> prefixBuffer = stackalloc byte[6];
            BinaryPrimitives.WriteUInt16LittleEndian(prefixBuffer.Slice(0, 2), 0xFFFF);
            BinaryPrimitives.WriteUInt32LittleEndian(prefixBuffer.Slice(2, 4), (uint)charCount);
            stream.Write(prefixBuffer);
        }
    
        #endregion
    }
}
