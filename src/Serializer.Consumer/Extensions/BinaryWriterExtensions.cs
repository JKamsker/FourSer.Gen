using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Serializer.Consumer.Extensions;

public static class BinaryWriterExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteByte(this BinaryWriter input, byte value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteUInt32(this BinaryWriter input, uint value) => input.Write(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteInt32(this BinaryWriter input, int value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteUInt16(this BinaryWriter input, ushort value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteUInt64(this BinaryWriter input, ulong value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteInt64(this BinaryWriter input, long value) => input.Write(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteUShort(this BinaryWriter input, ushort value) => input.Write(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void WriteSingle(this BinaryWriter input, float value) => input.Write(value);
        
        
        

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteString(this BinaryWriter input, string value)
    {
        var encoding = Encoding.UTF8;
        var byteCount = encoding.GetByteCount(value.AsSpan());

        Span<byte> buffer = byteCount <= 1024 ? stackalloc byte[byteCount] : new byte[byteCount];
        encoding.GetBytes(value, buffer);
            
        input.WriteInt32(byteCount);
        input.Write(buffer);
    }
}