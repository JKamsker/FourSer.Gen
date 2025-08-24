using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Buffers.Binary;

namespace FourSer.Tests
{
    public static class StringEx
    {
        private static readonly Encoding Utf8Encoding = Encoding.UTF8;
        public static int MeasureSize(string value)
        {
            if (string.IsNullOrEmpty(value)) { return sizeof(int); }
            return sizeof(int) + Utf8Encoding.GetByteCount(value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadString(ref ReadOnlySpan<byte> input)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(input);
            input = input.Slice(sizeof(int));
            if (length == 0) { return string.Empty; }
            var strSpan = input.Slice(0, length);
            input = input.Slice(length);
            return Utf8Encoding.GetString(strSpan);
        }
    }

    public static class RoSpanReaderExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByte(this ref ReadOnlySpan<byte> input)
        {
            var val = input[0];
            input = input.Slice(1);
            return val;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32(this ref ReadOnlySpan<byte> input)
        {
            var val = BinaryPrimitives.ReadInt32LittleEndian(input);
            input = input.Slice(sizeof(int));
            return val;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32(this ref ReadOnlySpan<byte> input)
        {
            var val = BinaryPrimitives.ReadUInt32LittleEndian(input);
            input = input.Slice(sizeof(uint));
            return val;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16(this ref ReadOnlySpan<byte> input)
        {
            var val = BinaryPrimitives.ReadUInt16LittleEndian(input);
            input = input.Slice(sizeof(ushort));
            return val;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadSingle(this ref ReadOnlySpan<byte> input)
        {
            var val = BinaryPrimitives.ReadSingleLittleEndian(input);
            input = input.Slice(sizeof(float));
            return val;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadString(this ref ReadOnlySpan<byte> input) => StringEx.ReadString(ref input);
    }

    public static class SpanWriterExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteByte(this ref Span<byte> input, byte value)
        {
            input[0] = value;
            input = input.Slice(1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32(this ref Span<byte> input, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(input, value);
            input = input.Slice(sizeof(int));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt32(this ref Span<byte> input, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(input, value);
            input = input.Slice(sizeof(uint));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt16(this ref Span<byte> input, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(input, value);
            input = input.Slice(sizeof(ushort));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSingle(this ref Span<byte> input, float value)
        {
            BinaryPrimitives.WriteSingleLittleEndian(input, value);
            input = input.Slice(sizeof(float));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteString(this ref Span<byte> input, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                BinaryPrimitives.WriteInt32LittleEndian(input, 0);
                input = input.Slice(sizeof(int));
                return;
            }
            var byteCount = Encoding.UTF8.GetByteCount(value);
            BinaryPrimitives.WriteInt32LittleEndian(input, byteCount);
            input = input.Slice(sizeof(int));
            Encoding.UTF8.GetBytes(value, input);
            input = input.Slice(byteCount);
        }
    }
}
