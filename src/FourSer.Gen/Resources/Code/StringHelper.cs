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
    public static string ReadString(ref ReadOnlySpan<byte> input)
    {
        var length = FourSer.Gen.Helpers.RoSpanReaderHelpers.ReadInt32(ref input);
        if (length == 0)
        {
            return string.Empty;
        }
        var strSpan = input.Slice(0, length);
        input = input.Slice(length);
        return Utf8Encoding.GetString(strSpan);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(ref Span<byte> input)
    {
        var length = FourSer.Gen.Helpers.SpanReaderHelpers.ReadInt32(ref input);
        if (length == 0)
        {
            return string.Empty;
        }
        var strSpan = input.Slice(0, length);
        input = input.Slice(length);
        return Utf8Encoding.GetString(strSpan);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteString(ref Span<byte> input, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            FourSer.Gen.Helpers.SpanWriterHelpers.WriteInt32(ref input, 0);
            return;
        }

        var byteCount = Utf8Encoding.GetByteCount(value);
        FourSer.Gen.Helpers.SpanWriterHelpers.WriteInt32(ref input, byteCount);
        var destination = input.Slice(0, byteCount);
        Utf8Encoding.GetBytes(value, destination);
        input = input.Slice(byteCount);
    }
}
