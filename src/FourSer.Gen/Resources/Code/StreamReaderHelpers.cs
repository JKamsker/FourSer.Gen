using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace FourSer.Gen.Helpers;

internal static class StreamReaderHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ReadByte(this Stream stream)
    {
        int b = stream.ReadByte();
        if (b == -1)
            throw new EndOfStreamException();
        return (byte)b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte ReadSByte(this Stream stream) => (sbyte)ReadByte(stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadBoolean(this Stream stream) => ReadByte(stream) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadInt16(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        stream.ReadExactly(buffer);
        return BitConverter.ToInt16(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        stream.ReadExactly(buffer);
        return BitConverter.ToUInt16(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        stream.ReadExactly(buffer);
        return BitConverter.ToInt32(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        stream.ReadExactly(buffer);
        return BitConverter.ToUInt32(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadInt64(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        stream.ReadExactly(buffer);
        return BitConverter.ToInt64(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReadUInt64(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        stream.ReadExactly(buffer);
        return BitConverter.ToUInt64(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadSingle(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        stream.ReadExactly(buffer);
        return BitConverter.ToSingle(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ReadDouble(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        stream.ReadExactly(buffer);
        return BitConverter.ToDouble(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(this Stream stream)
    {
        ushort length = stream.ReadUInt16();
        byte[] buffer = new byte[length];
        stream.ReadExactly(buffer);
        return System.Text.Encoding.UTF8.GetString(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ReadBytes(this Stream stream, int count)
    {
        byte[] buffer = new byte[count];
        stream.ReadExactly(buffer);
        return buffer;
    }
}
