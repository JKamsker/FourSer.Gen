using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Serializer.Generator.Helpers;

internal static class BinaryWriterExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteByte(this BinaryWriter input, byte value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteUInt32(this BinaryWriter input, uint value) => input.Write(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteInt32(this BinaryWriter input, int value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteInt16(this BinaryWriter input, short value) => input.Write(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteUInt16(this BinaryWriter input, ushort value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteUInt64(this BinaryWriter input, ulong value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteInt64(this BinaryWriter input, long value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteSingle(this BinaryWriter input, float value) => input.Write(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteDouble(this BinaryWriter input, double value) => input.Write(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteBoolean(this BinaryWriter input, bool value) => input.Write(value);

    public static void WriteString(this BinaryWriter input, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            input.Write(0);
            return;
        }

        var buffer = Encoding.UTF8.GetBytes(value);
        input.Write(buffer.Length);
        input.Write(buffer);
    }
}
