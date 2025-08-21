using System.Runtime.CompilerServices;
using System.Runtime.InteropServices; // Required for CollectionsMarshal

namespace FourSer.Consumer.Extensions;

public static class SpanTestExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBytes1(this ref Span<byte> input, IEnumerable<byte>? value)
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
