using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace FourSer.Gen.Helpers;

internal class StringEx
{
    private static readonly Encoding Utf8Encoding = Encoding.UTF8;
    
    public static int MeasureSize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return sizeof(int);
        }

        return sizeof(int) + Utf8Encoding.GetByteCount(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe string ReadString(ref ReadOnlySpan<byte> input)
    {
        var length = input.ReadInt32();
        var strSpan = input.Slice(0, length);
        input = input.Slice(length);

        if (length == 0)
        {
            return string.Empty;
        }

        fixed (byte* p = strSpan)
        {
            return Utf8Encoding.GetString(p, strSpan.Length);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe string ReadString(ref Span<byte> input)
    {
        var length = input.ReadInt32();
        var strSpan = input.Slice(0, length);
        input = input.Slice(length);

        if (length == 0)
        {
            return string.Empty;
        }

        fixed (byte* p = strSpan)
        {
            return Utf8Encoding.GetString(p, strSpan.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteString(ref Span<byte> input, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            input.WriteInt32(0);
            return;
        }

        var byteCount = Utf8Encoding.GetByteCount(value);
        input.WriteInt32(byteCount);
        var destination = input.Slice(0, byteCount);
        input = input.Slice(byteCount);

        fixed (char* pValue = value)
        fixed (byte* pDestination = destination)
        {
            Utf8Encoding.GetBytes(pValue, value.Length, pDestination, destination.Length);
        }
    }
}
