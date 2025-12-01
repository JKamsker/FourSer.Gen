using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FourSer.Gen.Helpers;

internal static class RoSpanReaderHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ReadByte(ref ReadOnlySpan<byte> input)
    {
        var original = Advance<byte>(ref input);
        return original[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte ReadSByte(ref ReadOnlySpan<byte> input)
    {
        return (sbyte)ReadByte(ref input);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadInt16(ref ReadOnlySpan<byte> input)
    {
        var original = Advance<short>(ref input);
        return BinaryPrimitives.ReadInt16LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16(ref ReadOnlySpan<byte> input)
    {
        var original = Advance<ushort>(ref input);
        return BinaryPrimitives.ReadUInt16LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32(ref ReadOnlySpan<byte> input)
    {
        var original = Advance<int>(ref input);
        return BinaryPrimitives.ReadInt32LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32(ref ReadOnlySpan<byte> input)
    {
        var original = Advance<uint>(ref input);
        return BinaryPrimitives.ReadUInt32LittleEndian(original);
    }

    public static long ReadInt64(ref ReadOnlySpan<byte> input)
    {
        var original = Advance<long>(ref input);
        return BinaryPrimitives.ReadInt64LittleEndian(original);
    }

    public static ulong ReadUInt64(ref ReadOnlySpan<byte> input)
    {
        var original = Advance<ulong>(ref input);
        return BinaryPrimitives.ReadUInt64LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe float ReadSingle(ref ReadOnlySpan<byte> input)
    {
        int val = ReadInt32(ref input);
        return *(float*)&val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe double ReadDouble(ref ReadOnlySpan<byte> input)
    {
        ulong val = ReadUInt64(ref input);
        return *(double*)&val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal ReadDecimal(ref ReadOnlySpan<byte> input)
    {
        var lo = ReadInt32(ref input);
        var mid = ReadInt32(ref input);
        var hi = ReadInt32(ref input);
        var flags = ReadInt32(ref input);
        var isNegative = (flags & unchecked((int)0x80000000)) != 0;
        var scale = (byte)((flags >> 16) & 0x7F);
        return new decimal(lo, mid, hi, isNegative, scale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(ref ReadOnlySpan<byte> input)
        => StringEx.ReadString(ref input);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadBoolean(ref ReadOnlySpan<byte> input)
    {
        return ReadByte(ref input) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ReadBytes(ref ReadOnlySpan<byte> input, int count)
    {
        var result = new byte[count];
        var source = input.Slice(0, count);
        source.CopyTo(result);
        input = input.Slice(count);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadBytes(ref ReadOnlySpan<byte> input, Span<byte> destination)
    {
        var source = input.Slice(0, destination.Length);
        source.CopyTo(destination);
        input = input.Slice(destination.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe ReadOnlySpan<byte> Advance<T>(ref ReadOnlySpan<byte> input) where T : unmanaged
    {
        var original = input;
        var slized = input.Slice(sizeof(T));
        input = slized;
        return original;
    }
}
