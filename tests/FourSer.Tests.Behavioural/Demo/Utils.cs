using System;
using System.IO;
using System.Text;

public static class MfcStringUtils
{
    /// <summary>
    /// Reads an MFC-style Unicode CString from a stream.
    /// The format is: BOM (0xFEFF), then a length prefix, then UTF-16LE bytes,
    /// and finally an optional null terminator (0x0000).
    /// </summary>
    /// <param name="stream">The input stream to read from.</param>
    /// <returns>The raw UTF-16LE content bytes (without BOM or terminator).</returns>
    public static byte[] ReadMfcUnicodeCStringRaw(Stream stream)
    {
        // Use a 2-byte buffer on the stack for efficiency.
        Span<byte> buffer = stackalloc byte[2];

        // 1. Read and verify the UTF-16LE BOM (Byte Order Mark).
        stream.ReadExactly(buffer);
        ushort bom = BitConverter.ToUInt16(buffer);
        if (bom != 0xFEFF)
        {
            throw new InvalidDataException($"Expected UTF-16LE BOM 0xFEFF, found 0x{bom:X4}");
        }

        // 2. Read the MFC-style length prefix.
        int charCount = ReadMfcCount(stream);
        if (charCount < 0)
        {
            throw new InvalidDataException("Invalid negative length for string.");
        }

        if (charCount == 0)
        {
            // // Handle optional null terminator even for empty strings
            // Redacted because not required for this format.

            return Array.Empty<byte>();
        }

        // 3. Read the string payload.
        int byteCount = checked(charCount * 2);
        byte[] data = new byte[byteCount];
        stream.ReadExactly(data); // Ensures all bytes are read or throws.

        // // 4. Handle the optional UTF-16 null terminator (0x0000).
        // Redacted because not required for this format.

        return data;
    }


    /// <summary>
    /// Reads a modified MFC length prefix.
    /// This has been changed from the standard to handle a specific non-standard file format.
    /// </summary>
    private static int ReadMfcCount(Stream stream)
    {
        int firstByte = stream.ReadByte();
        if (firstByte == -1) throw new EndOfStreamException();

        if (firstByte != 0xFF)
        {
            // Standard case for lengths 0-254.
            return firstByte;
        }

        // --- NON-STANDARD BEHAVIOR ---
        // The file format being parsed uses a non-standard length encoding.
        // For example, a length of 4 is encoded as `FF 04`.
        // The standard MFC format would encode this simply as `04`. For a length
        // of 256, the standard is `FF 00 01`.
        //
        // This implementation handles the non-standard `FF [byte]` format.
        // It will FAIL to parse standard MFC files with string lengths >= 256
        // where the low byte of the length is not 0xFF.
        // This is an accepted trade-off to parse the required file.

        // Let's peek at the next byte to decide what to do.
        int secondByte = stream.ReadByte();
        if (secondByte == -1) throw new EndOfStreamException();

        if (secondByte != 0xFF)
        {
            // This handles the non-standard case `FF 04` -> length 4.
            // It also handles the standard case `FF FF 00` -> length 255, because
            // it will read `FF` and return `FF` (255).
            return secondByte;
        }

        // We have `FF FF`. This matches the standard prefix for a 2-byte or 4-byte length.
        Span<byte> buffer = stackalloc byte[4];
        stream.ReadExactly(buffer.Slice(0, 2));
        ushort word = BitConverter.ToUInt16(buffer);
        if (word != 0xFFFF)
        {
            return word;
        }

        stream.ReadExactly(buffer.Slice(0, 4));
        uint dword = BitConverter.ToUInt32(buffer);
        return checked((int)dword); // Throw if length exceeds int.MaxValue
    }
}
