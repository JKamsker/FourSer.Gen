// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// This file is a an exact copy of the an official an internal implementation of EquatableArray<T>
// from the .NET Community Toolkit. It is being used here as the generator targets
// netstandard2.0, which doesn't have access to this type.
// Source: https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.Mvvm.Source.Generators/Helpers/EquatableArray%7BT%7D.cs

using System.Collections;
using System.Collections.Immutable;

namespace FourSer.Gen.Models;

/// <summary>
///     A wrapper for an <see cref="ImmutableArray{T}" /> that implements value equality.
/// </summary>
/// <typeparam name="T">The type of values in the array.</typeparam>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    /// <summary>
    ///     The wrapped <see cref="ImmutableArray{T}" /> instance.
    /// </summary>
    private readonly ImmutableArray<T> array;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EquatableArray{T}" /> struct.
    ///     <param name="array">The <see cref="ImmutableArray{T}" /> to wrap.</param>
    /// </summary>
    public EquatableArray(ImmutableArray<T> array)
    {
        this.array = array;
    }

    /// <summary>
    ///     Gets a value indicating whether the current array is empty.
    /// </summary>
    public bool IsEmpty => array.IsDefaultOrEmpty;

    /// <summary>
    ///     Gets the number of items in the current array.
    /// </summary>
    public int Count => array.Length;

    /// <summary>
    ///     Gets the item at a given index.
    /// </summary>
    /// <param name="index">The index of the item to get.</param>
    /// <returns>The item at the specified index.</returns>
    public T this[int index] => array[index];

    /// <summary>
    ///     Implicitly converts an <see cref="ImmutableArray{T}" /> to <see cref="EquatableArray{T}" />.
    /// </summary>
    /// <param name="array">The <see cref="ImmutableArray{T}" /> to wrap.</param>
    public static implicit operator EquatableArray<T>(ImmutableArray<T> array)
    {
        return new(array);
    }

    /// <inheritdoc />
    public bool Equals(EquatableArray<T> other)
    {
        return array.SequenceEqual(other.array);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> array && Equals(array);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (array.IsDefault)
        {
            return 0;
        }

        HashCode hashCode = default;

        foreach (var item in array)
        {
            hashCode.Add(item);
        }

        return hashCode.ToHashCode();
    }

    /// <summary>
    ///     Gets an <see cref="ImmutableArray{T}.Enumerator" /> for the current array.
    /// </summary>
    /// <returns>An <see cref="ImmutableArray{T}.Enumerator" /> for the current array.</returns>
    public ImmutableArray<T>.Enumerator GetEnumerator()
    {
        return array.GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)array).GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)array).GetEnumerator();
    }
}