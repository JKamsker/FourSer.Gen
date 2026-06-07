using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;

namespace FourSer.Gen.Helpers;

internal static class SequenceReaderHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ReadByte(ref SequenceReader<byte> reader)
    {
        if (!reader.TryRead(out var value))
        {
            throw new EndOfStreamException();
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte ReadSByte(ref SequenceReader<byte> reader) => (sbyte)ReadByte(ref reader);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadBoolean(ref SequenceReader<byte> reader) => ReadByte(ref reader) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadInt16(ref SequenceReader<byte> reader)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        ReadBytes(ref reader, buffer);
        return BinaryPrimitives.ReadInt16LittleEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16(ref SequenceReader<byte> reader)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        ReadBytes(ref reader, buffer);
        return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32(ref SequenceReader<byte> reader)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        ReadBytes(ref reader, buffer);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32(ref SequenceReader<byte> reader)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        ReadBytes(ref reader, buffer);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadInt64(ref SequenceReader<byte> reader)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        ReadBytes(ref reader, buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReadUInt64(ref SequenceReader<byte> reader)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        ReadBytes(ref reader, buffer);
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadSingle(ref SequenceReader<byte> reader)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        ReadBytes(ref reader, buffer);
        return BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ReadDouble(ref SequenceReader<byte> reader)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        ReadBytes(ref reader, buffer);
        return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal ReadDecimal(ref SequenceReader<byte> reader)
    {
        var lo = ReadInt32(ref reader);
        var mid = ReadInt32(ref reader);
        var hi = ReadInt32(ref reader);
        var flags = ReadInt32(ref reader);
        var isNegative = (flags & unchecked((int)0x80000000)) != 0;
        var scale = (byte)((flags >> 16) & 0x7F);
        return new decimal(lo, mid, hi, isNegative, scale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(ref SequenceReader<byte> reader)
    {
        var length = ReadInt32(ref reader);
        if (length == 0)
        {
            return string.Empty;
        }

        if (length < 0)
        {
            throw new InvalidDataException("String length cannot be negative.");
        }

        if (length <= 512)
        {
            Span<byte> buffer = stackalloc byte[length];
            ReadBytes(ref reader, buffer);
            return System.Text.Encoding.UTF8.GetString(buffer);
        }

        var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var buffer = rented.AsSpan(0, length);
            ReadBytes(ref reader, buffer);
            return System.Text.Encoding.UTF8.GetString(buffer);
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ReadBytes(ref SequenceReader<byte> reader, int count)
    {
        if (count < 0)
        {
            throw new InvalidDataException("Byte count cannot be negative.");
        }

        var buffer = new byte[count];
        ReadBytes(ref reader, buffer);
        return buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadBytes(ref SequenceReader<byte> reader, scoped Span<byte> destination)
    {
        if (reader.Remaining < destination.Length)
        {
            throw new EndOfStreamException();
        }

        if (!reader.TryCopyTo(destination))
        {
            throw new EndOfStreamException();
        }

        reader.Advance(destination.Length);
    }

    public static T DeserializeSerializable<T>(ref SequenceReader<byte> reader)
        where T : global::FourSer.Contracts.ISerializable<T>
    {
        var remainingBytes = CopyRemainingBytes(reader);
        ReadOnlySpan<byte> data = remainingBytes;
        try
        {
            var value = T.Deserialize(ref data);
            reader.Advance(remainingBytes.Length - data.Length);
            return value;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new EndOfStreamException("The sequence does not contain enough data to deserialize the value.", ex);
        }
        catch (IndexOutOfRangeException ex)
        {
            throw new EndOfStreamException("The sequence does not contain enough data to deserialize the value.", ex);
        }
    }

    public static T DeserializeWithSerializer<T>(
        ref SequenceReader<byte> reader,
        global::FourSer.Contracts.ISerializer<T> serializer)
    {
        if (serializer is null)
        {
            throw new ArgumentNullException(nameof(serializer));
        }

        var remainingBytes = CopyRemainingBytes(reader);
        ReadOnlySpan<byte> data = remainingBytes;
        try
        {
            var value = serializer.Deserialize(ref data);
            reader.Advance(remainingBytes.Length - data.Length);
            return value;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new EndOfStreamException("The sequence does not contain enough data to deserialize the value.", ex);
        }
        catch (IndexOutOfRangeException ex)
        {
            throw new EndOfStreamException("The sequence does not contain enough data to deserialize the value.", ex);
        }
    }

    private static byte[] CopyRemainingBytes(SequenceReader<byte> reader)
    {
        var length = checked((int)reader.Remaining);
        var buffer = new byte[length];
        if (!reader.TryCopyTo(buffer))
        {
            throw new EndOfStreamException();
        }

        return buffer;
    }
}
