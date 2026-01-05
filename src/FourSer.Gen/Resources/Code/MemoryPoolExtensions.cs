using System;
using System.Buffers;

namespace FourSer.Gen.Helpers;

internal static class MemoryPoolExtensions
{
    public static IMemoryOwner<T> SliceToSize<T>(this IMemoryOwner<T> owner, int size)
    {
        if (owner is null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        if (owner.Memory.Length == size)
        {
            return owner;
        }

        return new MemoryOwnerWrapper<T>(owner, size);
    }

    private sealed class MemoryOwnerWrapper<T> : IMemoryOwner<T>
    {
        private readonly IMemoryOwner<T> _inner;
        private readonly int _size;

        public MemoryOwnerWrapper(IMemoryOwner<T> inner, int size)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _size = size;
        }

        public Memory<T> Memory => _inner.Memory.Slice(0, _size);

        public void Dispose()
        {
            _inner.Dispose();
        }
    }
}

