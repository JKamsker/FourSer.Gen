using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

namespace FourSer.Gen.Helpers;

internal static class SpanWriterExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteByte(this ref Span<byte> input, byte value)
    {
        var original = Advance<byte>(ref input);
        original[0] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteSByte(this ref Span<byte> input, sbyte value)
    {
        var original = Advance<sbyte>(ref input);
        original[0] = (byte)value;
    }

    /// <summary>
    /// Copied from <see cref="BinaryWriter.Write(uint)"/>
    /// </summary>
    /// <param name="input"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt32(this ref Span<byte> input, uint value)
    {
        var original = Advance<uint>(ref input);
        original[0] = (byte)value;
        original[1] = (byte)(value >> 8);
        original[2] = (byte)(value >> 16);
        original[3] = (byte)(value >> 24);
    }

    /// <summary>
    /// Copied from <see cref="BinaryWriter.Write(float)"/>
    /// </summary>
    /// <param name="input"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteSingle(this ref Span<byte> input, float value) => input.WriteUInt32(*(uint*)&value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteDouble(this ref Span<byte> input, double value) => input.WriteUInt64(*(ulong*)&value);

    /// <summary>
    /// Copied from <see cref="BinaryWriter.Write(ushort)"/>
    /// </summary>
    /// <param name="input"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt16(this ref Span<byte> input, ushort value)
    {
        var original = Advance<ushort>(ref input);
        original[0] = (byte)value;
        original[1] = (byte)(value >> 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt16(this ref Span<byte> input, short value)
    {
        var original = Advance<short>(ref input);
        original[0] = (byte)value;
        original[1] = (byte)(value >> 8);
    }

    /// <summary>
    /// Copied from <see cref="BinaryWriter.Write(ulong)"/>
    /// </summary>
    /// <param name="input"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUInt64(this ref Span<byte> input, ulong value)
    {
        var original = Advance<ulong>(ref input);
        original[0] = (byte)value;
        original[1] = (byte)(value >> 8);
        original[2] = (byte)(value >> 16);
        original[3] = (byte)(value >> 24);
        original[4] = (byte)(value >> 32);
        original[5] = (byte)(value >> 40);
        original[6] = (byte)(value >> 48);
        original[7] = (byte)(value >> 56);
    }

    /// <summary>
    /// Copied from <see cref="BinaryWriter.Write(long)"/>
    /// </summary>
    /// <param name="input"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt64(this ref Span<byte> input, long value)
    {
        var original = Advance<long>(ref input);
        original[0] = (byte)value;
        original[1] = (byte)(value >> 8);
        original[2] = (byte)(value >> 16);
        original[3] = (byte)(value >> 24);
        original[4] = (byte)(value >> 32);
        original[5] = (byte)(value >> 40);
        original[6] = (byte)(value >> 48);
        original[7] = (byte)(value >> 56);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBoolean(this ref Span<byte> input, bool value)
    {
        var original = Advance<byte>(ref input);
        original[0] = (byte)(value ? 1 : 0);
    }

    /// <summary>
    /// Copied from <see cref="BinaryWriter.Write(int)"/>
    /// </summary>
    /// <param name="input"></param>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt32(this ref Span<byte> input, int value)
    {
        var original = Advance<int>(ref input);
        original[0] = (byte)value;
        original[1] = (byte)(value >> 8);
        original[2] = (byte)(value >> 16);
        original[3] = (byte)(value >> 24);
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteString(this ref Span<byte> input, string value)
    {
        StringEx.WriteString(ref input, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBytes(this ref Span<byte> input, ReadOnlySpan<byte> value)
    {
        value.CopyTo(input);
        input = input.Slice(value.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBytes(this ref Span<byte> input, byte[] value)
    {
        value.AsSpan().CopyTo(input);
        input = input.Slice(value.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Span<byte> Advance<T>(ref Span<byte> input) where T : unmanaged
    {
        var original = input;
        var slized = input.Slice(sizeof(T));
        input = slized;
        return original;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBytes(this ref Span<byte> input, IEnumerable<byte>? value)
    {
        if (value is null)
        {
            return;
        }

        // Best case: The source is an array.
        if (value is byte[] array)
        {
            array.AsSpan().CopyTo(input);
            input = input[array.Length..];
            return;
        }

        // Great case: The source is a List<byte>.
        // We can get its internal memory directly without allocation.
        if (value is List<byte> list)
        {
            var sourceSpan = CollectionsMarshal.AsSpan(list);
            sourceSpan.CopyTo(input);
            input = input[sourceSpan.Length..];
            return;
        }

        // Good case: It's another collection type.
        // Note: The original code wrote the count for ICollection, which is unusual.
        // This version just writes the bytes for consistency. If you need the count,
        // you should handle it separately and explicitly.
        if (value is ICollection<byte> collection)
        {
            if (collection.Count == 0) return;

            // This is still slow but avoids one virtual call from the foreach below.
            foreach (var b in collection)
            {
                input[0] = b;
                input = input[1..];
            }

            return;
        }

        // Slowest case: A generic enumerable (e.g., from a 'yield return' method).
        // We have no choice but to iterate.
        foreach (var b in value)
        {
            input[0] = b;
            input = input[1..];
        }
    }
}
