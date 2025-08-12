using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Serializer.Consumer.Extensions
{
    public static class SpanWriterExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteByte(this ref Span<byte> input, byte value)
        {
            var original = Advance<byte>(ref input);
            original[0] = value;
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

        /// <summary>
        /// Copied from <see cref="BinaryWriter.Write(ushort)"/>
        /// </summary>
        /// <param name="input"></param>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUShort(this ref Span<byte> input, ushort value)
        {
            var original = Advance<ushort>(ref input);
            original[0] = (byte)value;
            original[1] = (byte)(value >> 8);
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
            var encoding = Encoding.UTF8;
            var byteCount = encoding.GetByteCount(value.AsSpan());
            input.WriteInt32(byteCount);
            encoding.GetBytes(value, input);
            input = input[byteCount..];
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
}