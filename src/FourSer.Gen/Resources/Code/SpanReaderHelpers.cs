using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace FourSer.Gen.Helpers;

internal static class SpanReaderHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ReadByte(ref Span<byte> input)
    {
        var original = Advance<byte>(ref input);
        return original[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadInt16(ref Span<byte> input)
    {
        var original = Advance<short>(ref input);
        return BinaryPrimitives.ReadInt16LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16(ref Span<byte> input)
    {
        var original = Advance<ushort>(ref input);
        return BinaryPrimitives.ReadUInt16LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32(ref Span<byte> input)
    {
        var original = Advance<int>(ref input);
        return BinaryPrimitives.ReadInt32LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32(ref Span<byte> input)
    {
        var original = Advance<uint>(ref input);
        return BinaryPrimitives.ReadUInt32LittleEndian(original);
    }

    public static long ReadInt64(ref Span<byte> input)
    {
        var original = Advance<long>(ref input);
        return BinaryPrimitives.ReadInt64LittleEndian(original);
    }

    public static ulong ReadUInt64(ref Span<byte> input)
    {
        var original = Advance<ulong>(ref input);
        return BinaryPrimitives.ReadUInt64LittleEndian(original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe float ReadSingle(ref Span<byte> input)
    {
        int val = ReadInt32(ref input);
        return *(float*)&val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe double ReadDouble(ref Span<byte> input)
    {
        ulong val = ReadUInt64(ref input);
        return *(double*)&val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(ref Span<byte> input)
        => StringEx.ReadString(ref input);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadBoolean(ref Span<byte> input)
    {
        return ReadByte(ref input) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Span<byte> Advance<T>(ref Span<byte> input) where T : unmanaged
    {
        var original = input;
        var slized = input.Slice(sizeof(T));
        input = slized;
        return original;
    }
}
