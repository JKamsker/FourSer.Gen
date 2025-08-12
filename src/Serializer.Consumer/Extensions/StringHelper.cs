using System.Runtime.CompilerServices;
using System.Text;

namespace FourServerProxy.HighPerformance.Extensions;

public class StringEx
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
        var length = input.ReadInt32();
        var strSpan = input[..length];
        input = input[length..];

        if (length == 0)
        {
            return string.Empty;
        }

        return Utf8Encoding.GetString(strSpan);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadString(ref Span<byte> input)
    {
        var length = input.ReadInt32();
        var strSpan = input[..length];
        input = input[length..];

        if (length == 0)
        {
            return string.Empty;
        }

        return Utf8Encoding.GetString(strSpan);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteString(ref Span<byte> input, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            input.WriteInt32(0);
            return;
        }

        var encoding = Utf8Encoding;
        var byteCount = encoding.GetByteCount(value);
        input.WriteInt32(byteCount);
        var original = input[..byteCount];
        input = input[byteCount..];

        if (byteCount == 0)
        {
            return;
        }

        encoding.GetBytes(value, original);
    }
}