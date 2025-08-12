using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Serializer.Generator.Helpers;

internal static class RoSpanReaderExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ReadByte(this ref ReadOnlySpan<byte> input)
    {
        var original = Advance<byte>(ref input);
        return original[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadInt16(this ref ReadOnlySpan<byte> input)
    {
        var original = Advance<short>(ref input);
        return BinaryPrimitives.ReadInt16LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16(this ref ReadOnlySpan<byte> input)
    {
        var original = Advance<ushort>(ref input);
        return BinaryPrimitives.ReadUInt16LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32(this ref ReadOnlySpan<byte> input)
    {
        var original = Advance<int>(ref input);
        return BinaryPrimitives.ReadInt32LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32(this ref ReadOnlySpan<byte> input)
    {
        var original = Advance<uint>(ref input);
        return BinaryPrimitives.ReadUInt32LittleEndian(original);
    }

    public static long ReadInt64(this ref ReadOnlySpan<byte> input)
    {
        var original = Advance<long>(ref input);
        return BinaryPrimitives.ReadInt64LittleEndian(original);
    }

    public static ulong ReadUInt64(this ref ReadOnlySpan<byte> input)
    {
        var original = Advance<ulong>(ref input);
        return BinaryPrimitives.ReadUInt64LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe float ReadSingle(this ref ReadOnlySpan<byte> input)
    {
        int val = ReadInt32(ref input);
        return *(float*)&val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe double ReadDouble(this ref ReadOnlySpan<byte> input)
    {
        ulong val = ReadUInt64(ref input);
        return *(double*)&val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(this ref ReadOnlySpan<byte> input)
        => StringEx.ReadString(ref input);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadBoolean(this ref ReadOnlySpan<byte> input)
    {
        return input.ReadByte() != 0;
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
