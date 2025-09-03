using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FourSer.Gen.Helpers;

internal static class StreamWriterHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this Stream stream, byte value)
    {
        stream.WriteByte(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteSByte(this Stream stream, sbyte value)
    {
        stream.WriteByte((byte)value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBoolean(this Stream stream, bool value)
    {
        stream.WriteByte(value ? (byte)1 : (byte)0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBytes(this Stream stream, ReadOnlySpan<byte> value)
    {
        stream.Write(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBytes(this Stream stream, byte[] value)
    {
        stream.Write(value, 0, value.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteUInt32(this Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BitConverter.TryWriteBytes(buffer, value);
        stream.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteSingle(this Stream stream, float value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        BitConverter.TryWriteBytes(buffer, value);
        stream.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteDouble(this Stream stream, double value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        BitConverter.TryWriteBytes(buffer, value);
        stream.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt16(this Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BitConverter.TryWriteBytes(buffer, value);
        stream.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt16(this Stream stream, short value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        BitConverter.TryWriteBytes(buffer, value);
        stream.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt64(this Stream stream, ulong value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BitConverter.TryWriteBytes(buffer, value);
        stream.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt64(this Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BitConverter.TryWriteBytes(buffer, value);
        stream.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt32(this Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(buffer, value);
        stream.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteString(this Stream stream, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            stream.WriteUInt16(0);
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(value);
        stream.WriteUInt16((ushort)byteCount);

        if (byteCount == 0)
        {
            return;
        }

        if (byteCount <= 512)
        {
            Span<byte> buffer = stackalloc byte[512];
            var actualBytes = Encoding.UTF8.GetBytes(value, buffer);
            stream.Write(buffer.Slice(0, actualBytes));
        }
        else
        {
            var rented = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                var actualBytes = Encoding.UTF8.GetBytes(value, 0, value.Length, rented, 0);
                stream.Write(rented, 0, actualBytes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBytes(this Stream stream, IEnumerable<byte>? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is byte[] array)
        {
            stream.Write(array, 0, array.Length);
            return;
        }

        if (value is List<byte> list)
        {
            var span = CollectionsMarshal.AsSpan(list);
            stream.Write(span);
            return;
        }

        foreach (var b in value)
        {
            stream.WriteByte(b);
        }
    }
}
