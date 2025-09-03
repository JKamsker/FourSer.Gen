using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FourSer.Gen.Helpers;

internal static class BinaryWriterHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteByte(BinaryWriter input, byte value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteUInt32(BinaryWriter input, uint value) => input.Write(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteInt32(BinaryWriter input, int value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteInt16(BinaryWriter input, short value) => input.Write(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteUInt16(BinaryWriter input, ushort value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteUInt64(BinaryWriter input, ulong value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteInt64(BinaryWriter input, long value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteSingle(BinaryWriter input, float value) => input.Write(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteDouble(BinaryWriter input, double value) => input.Write(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteBoolean(BinaryWriter input, bool value) => input.Write(value);

    public static void WriteString(BinaryWriter input, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            input.Write(0);
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(value);
        input.Write(byteCount);

        if (byteCount == 0)
        {
            return;
        }

        if (byteCount <= 512)
        {
            Span<byte> buffer = stackalloc byte[512];
            var actualBytes = Encoding.UTF8.GetBytes(value, buffer);
            input.Write(buffer.Slice(0, actualBytes));
        }
        else
        {
            var rented = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                var actualBytes = Encoding.UTF8.GetBytes(value, 0, value.Length, rented, 0);
                input.Write(rented, 0, actualBytes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}
