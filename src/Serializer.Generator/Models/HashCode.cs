// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// This file is a an exact copy of the an official an internal implementation of HashCode
// from the .NET Community Toolkit. It is being used here as the generator targets
// netstandard2.0, which doesn't have access to this type.
// Source: https://github.com/CommunityToolkit/dotnet/blob/7b53ae23dfc6a7fb12d0fc058b89b6e948f48448/src/CommunityToolkit.Mvvm.Source.Generators/Helpers/HashCode.cs

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Serializer.Generator.Models;

/// <summary>
/// A helper type to build hash codes for custom types.
/// </summary>
/// <remarks>This type is not intended to be used directly by consumers.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
internal struct HashCode
{
    private static readonly uint s_seed = (uint)new System.Random().Next();

    private const uint Prime1 = 2654435761U;
    private const uint Prime2 = 2246822519U;
    private const uint Prime3 = 3266489917U;
    private const uint Prime4 = 668265263U;
    private const uint Prime5 = 374761393U;

    private uint hash;

    /// <summary>
    /// Initializes a new instance of the <see cref="HashCode"/> struct.
    /// </summary>
    public HashCode()
    {
        this.hash = s_seed;
    }

    /// <summary>
    /// Adds a value to the hash code.
    /// </summary>
    /// <typeparam name="T">The type of the value to add.</typeparam>
    /// <param name="value">The value to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add<T>(T value)
    {
        this.hash += (uint)(value?.GetHashCode() ?? 0);
        this.hash *= Prime1;
    }

    /// <summary>
    /// Adds a value to the hash code.
    /// </summary>
    /// <typeparam name="T">The type of the value to add.</typeparam>
    /// <param name="value">The value to add.</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> instance to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add<T>(T value, IEqualityComparer<T> comparer)
    {
        this.hash += (uint)(value is null ? 0 : comparer.GetHashCode(value));
        this.hash *= Prime1;
    }

    /// <summary>
    /// Gets the resulting hash code.
    /// </summary>
    /// <returns>The resulting hash code.</returns>
    public int ToHashCode()
    {
        uint h = this.hash;

        h ^= h >> 15;
        h *= Prime2;
        h ^= h >> 13;
        h *= Prime3;
        h ^= h >> 16;

        return (int)h;
    }
}
