using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace FourSer.Tests.Behavioural.Demo;

public static class MfcStringUtils
{
    public static string ReadMfcUnicodeCString(ref ReadOnlySpan<byte> data)
    {
        // 1. Read and verify BOM
        if (data.Length < 2)
        {
            throw new InvalidDataException("Insufficient data for BOM.");
        }

        var bom = BinaryPrimitives.ReadUInt16LittleEndian(data);
        if (bom != 0xFEFF)
        {
            throw new InvalidDataException($"Expected UTF-16LE BOM 0xFEFF, found 0x{bom:X4}");
        }

        data = data.Slice(2);

        // 2. Read length prefix
        int charCount = ReadMfcCount(ref data);
        if (charCount < 0)
        {
            throw new InvalidDataException("Invalid negative length for string.");
        }

        if (charCount == 0)
        {
            return string.Empty;
        }

        // 3. Read string payload
        int byteCount = checked(charCount * 2);
        if (data.Length < byteCount)
        {
            throw new InvalidDataException("Insufficient data for string content.");
        }

        var content = data.Slice(0, byteCount);
        data = data.Slice(byteCount);

        return Encoding.Unicode.GetString(content);
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

        if (data.IsEmpty)
        {
            throw new EndOfStreamException();
        }

        byte secondByte = data[0];
        data = data.Slice(1);

        if (secondByte != 0xFF)
        {
            return secondByte;
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
        int initialLength = buffer.Length;
        
        // 1. Write BOM
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, 0xFEFF);
        buffer = buffer.Slice(2);

        // 2. Write length prefix
        int charCount = value.Length;
        int prefixSize = WriteMfcCount(buffer, charCount);
        buffer = buffer.Slice(prefixSize);

        // 3. Write string payload
        int bytesWritten = Encoding.Unicode.GetBytes(value, buffer);

        return 2 + prefixSize + bytesWritten;
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

        if (charCount <= 0xFF)
        {
            buffer[0] = 0xFF;
            buffer[1] = (byte)charCount;
            return 2;
        }

        if (charCount <= 0xFFFF)
        {
            buffer[0] = 0xFF;
            buffer[1] = 0xFF;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2), (ushort)charCount);
            return 4;
        }

        buffer[0] = 0xFF;
        buffer[1] = 0xFF;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2), 0xFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(4), (uint)charCount);
        return 8;
    }

    public static int GetMfcUnicodeCStringSize(string value)
    {
        int charCount = (value ?? string.Empty).Length;
        int bomSize = 2; // UTF-16LE BOM
        int lengthPrefixSize = GetMfcCountSize(charCount);
        int stringDataSize = charCount * 2; // UTF-16LE

        return bomSize + lengthPrefixSize + stringDataSize;
    }

    private static int GetMfcCountSize(int charCount)
    {
        if (charCount < 0xFF)
        {
            return 1;
        }

        if (charCount <= 0xFF)
        {
            return 2;
        }

        if (charCount <= 0xFFFF)
        {
            return 4;
        }

        return 8;
    }
    
    #region Stream-based methods (legacy)
    
    public static byte[] ReadMfcUnicodeCStringRaw(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[2];

        stream.ReadExactly(buffer);
        ushort bom = BitConverter.ToUInt16(buffer);
        if (bom != 0xFEFF)
        {
            throw new InvalidDataException($"Expected UTF-16LE BOM 0xFEFF, found 0x{bom:X4}");
        }

        int charCount = ReadMfcCount(stream);
        if (charCount < 0)
        {
            throw new InvalidDataException("Invalid negative length for string.");
        }

        if (charCount == 0)
        {
            return Array.Empty<byte>();
        }

        int byteCount = checked(charCount * 2);
        byte[] data = new byte[byteCount];
        stream.ReadExactly(data);

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

        int secondByte = stream.ReadByte();
        if (secondByte == -1) throw new EndOfStreamException();

        if (secondByte != 0xFF)
        {
            return secondByte;
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

    public static void WriteMfcUnicodeCString(Stream stream, string value)
    {
        Span<byte> bomBuffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(bomBuffer, (ushort)0xFEFF);
        stream.Write(bomBuffer);

        byte[] utf16Bytes = Encoding.Unicode.GetBytes(value ?? string.Empty);
        int charCount = utf16Bytes.Length / 2;

        WriteMfcCount(stream, charCount);

        if (utf16Bytes.Length > 0)
        {
            stream.Write(utf16Bytes);
        }
    }

    private static void WriteMfcCount(Stream stream, int charCount)
    {
        if (charCount < 0)
        {
            throw new ArgumentException("Character count cannot be negative.", nameof(charCount));
        }

        if (charCount < 0xFF)
        {
            stream.WriteByte((byte)charCount);
        }
        else if (charCount <= 0xFF)
        {
            stream.WriteByte(0xFF);
            stream.WriteByte((byte)charCount);
        }
        else if (charCount <= 0xFFFF)
        {
            stream.WriteByte(0xFF);
            stream.WriteByte(0xFF);
            Span<byte> buffer = stackalloc byte[2];
            BitConverter.TryWriteBytes(buffer, (ushort)charCount);
            stream.Write(buffer);
        }
        else
        {
            stream.WriteByte(0xFF);
            stream.WriteByte(0xFF);
            Span<byte> buffer = stackalloc byte[6];
            BitConverter.TryWriteBytes(buffer.Slice(0, 2), (ushort)0xFFFF);
            BitConverter.TryWriteBytes(buffer.Slice(2, 4), (uint)charCount);
            stream.Write(buffer);
        }
    }
    
    #endregion
}
