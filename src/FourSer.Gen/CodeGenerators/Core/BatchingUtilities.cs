using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Core;

/// <summary>
///     Utilities for instruction batching in stream deserialization/serialization.
///     Batching reduces syscalls by reading consecutive fixed-size fields in a single operation.
/// </summary>
public static class BatchingUtilities
{
    /// <summary>
    ///     Minimum batch size in bytes to trigger batching optimization.
    ///     Below this threshold, individual reads are more efficient.
    /// </summary>
    public const int DefaultMinBatchSize = 8;

    /// <summary>
    ///     Maximum batch size that uses stackalloc. Above this, ArrayPool is used.
    /// </summary>
    public const int StackAllocThreshold = 256;

    /// <summary>
    ///     Analyzes members and produces a list of batched and non-batched instructions.
    /// </summary>
    /// <param name="members">The members to analyze.</param>
    /// <param name="type">The containing type (for resolving custom serializers).</param>
    /// <param name="minBatchSize">Minimum bytes to trigger batching.</param>
    /// <returns>List of BatchInstruction (either BatchGroup or SingleMember).</returns>
    public static List<BatchInstruction> AnalyzeMembers(
        IEnumerable<MemberToGenerate> members,
        TypeToGenerate type,
        int minBatchSize = DefaultMinBatchSize)
    {
        var result = new List<BatchInstruction>();
        var currentBatch = new List<BatchedMember>();
        var currentBatchSize = 0;

        foreach (var member in members)
        {
            if (IsBatchable(member, type, out var info))
            {
                currentBatch.Add(new BatchedMember(member, currentBatchSize, info.Size, info.IsFixedCollection, info.FixedCount, info.ElementSize, info.ElementTypeName));
                currentBatchSize += info.Size;
            }
            else
            {
                // Flush current batch if it meets minimum size
                if (currentBatchSize >= minBatchSize)
                {
                    result.Add(new BatchGroup(currentBatch.ToArray(), currentBatchSize));
                }
                else
                {
                    // Batch too small - emit as individual reads
                    foreach (var batchedMember in currentBatch)
                    {
                        result.Add(new SingleMember(batchedMember.Member));
                    }
                }

                // Reset batch
                currentBatch.Clear();
                currentBatchSize = 0;

                // Add non-batchable member
                result.Add(new SingleMember(member));
            }
        }

        // Flush remaining batch
        if (currentBatchSize >= minBatchSize)
        {
            result.Add(new BatchGroup(currentBatch.ToArray(), currentBatchSize));
        }
        else
        {
            foreach (var batchedMember in currentBatch)
            {
                result.Add(new SingleMember(batchedMember.Member));
            }
        }

        return result;
    }

    /// <summary>
    ///     Determines if a member can be batched (fixed size, no conditional logic).
    /// </summary>
    public static bool IsBatchable(MemberToGenerate member, TypeToGenerate type, out BatchableInfo info)
    {
        info = default;

        // Collections: allow fixed-size unmanaged collections (e.g., byte arrays with CountSize)
        if (member.IsList || member.IsCollection)
        {
            if (TryGetFixedCollectionInfo(member, type, out info))
                return true;
            return false;
        }

        // Members that participate in count/type-id logic must go through specialized emitters.
        if (member.IsCountSizeReferenceFor is not null || member.IsTypeIdPropertyFor is not null)
            return false;

        // Must be unmanaged type (fixed size)
        if (!member.IsUnmanagedType)
            return false;

        // Strings are variable length
        if (member.IsStringType)
            return false;

        // Polymorphic fields require type-dependent logic
        if (member.PolymorphicInfo is not null)
            return false;

        // Nested serializable types may contain variable-length fields
        if (member.HasGenerateSerializerAttribute)
            return false;

        // Custom serializers have unknown behavior
        if (GeneratorUtilities.ResolveSerializer(member, type) is not null)
            return false;

        info = new BatchableInfo(GetMemberSize(member), false, 0, 0, null);
        return true;
    }

    /// <summary>
    ///     Gets the size in bytes of a batchable member.
    /// </summary>
    public static int GetMemberSize(MemberToGenerate member)
    {
        return TypeHelper.GetSizeOf(member.TypeName);
    }

    /// <summary>
    ///     Generates the expression to read a value from a batch buffer.
    /// </summary>
    /// <param name="batchVar">The name of the batch span variable.</param>
    /// <param name="typeName">The type to read.</param>
    /// <param name="offset">The byte offset within the batch.</param>
    /// <returns>A C# expression that reads the value.</returns>
    public static string GetBatchReadExpression(string batchVar, string typeName, int offset)
    {
        // For offset 0, we can skip the Slice call for multi-byte types
        var sliceExpr = offset == 0 ? batchVar : $"{batchVar}.Slice({offset})";

        return typeName switch
        {
            "byte" => $"{batchVar}[{offset}]",
            "sbyte" => $"(sbyte){batchVar}[{offset}]",
            "bool" => $"{batchVar}[{offset}] != 0",
            "short" => $"System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian({sliceExpr})",
            "ushort" => $"System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian({sliceExpr})",
            "int" => $"System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian({sliceExpr})",
            "uint" => $"System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian({sliceExpr})",
            "long" => $"System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian({sliceExpr})",
            "ulong" => $"System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian({sliceExpr})",
            "float" => $"System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian({sliceExpr})",
            "double" => $"System.Buffers.Binary.BinaryPrimitives.ReadDoubleLittleEndian({sliceExpr})",
            "decimal" => GenerateDecimalReadExpression(batchVar, offset),
            _ => throw new NotSupportedException($"Unsupported batch type: {typeName}")
        };
    }

    /// <summary>
    ///     Generates the expression to write a value to a batch buffer.
    /// </summary>
    public static string GetBatchWriteExpression(string batchVar, string typeName, int offset, string valueExpr)
    {
        var sliceExpr = offset == 0 ? batchVar : $"{batchVar}.Slice({offset})";

        return typeName switch
        {
            "byte" => $"{batchVar}[{offset}] = (byte)({valueExpr})",
            "sbyte" => $"{batchVar}[{offset}] = (byte)({valueExpr})",
            "bool" => $"{batchVar}[{offset}] = (byte)(({valueExpr}) ? 1 : 0)",
            "short" => $"System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian({sliceExpr}, (short)({valueExpr}))",
            "ushort" => $"System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian({sliceExpr}, (ushort)({valueExpr}))",
            "int" => $"System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian({sliceExpr}, (int)({valueExpr}))",
            "uint" => $"System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian({sliceExpr}, (uint)({valueExpr}))",
            "long" => $"System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian({sliceExpr}, (long)({valueExpr}))",
            "ulong" => $"System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian({sliceExpr}, (ulong)({valueExpr}))",
            "float" => $"System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian({sliceExpr}, (float)({valueExpr}))",
            "double" => $"System.Buffers.Binary.BinaryPrimitives.WriteDoubleLittleEndian({sliceExpr}, (double)({valueExpr}))",
            "decimal" => GenerateDecimalWriteExpression(batchVar, offset, valueExpr),
            _ => throw new NotSupportedException($"Unsupported batch type: {typeName}")
        };
    }

    private static string GenerateDecimalReadExpression(string batchVar, int offset)
    {
        // decimal is 16 bytes: lo (4), mid (4), hi (4), flags (4)
        var slice = offset == 0 ? batchVar : $"{batchVar}.Slice({offset})";
        return $"new decimal(" +
               $"System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian({slice}), " +
               $"System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian({slice}.Slice(4)), " +
               $"System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian({slice}.Slice(8)), " +
               $"(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian({slice}.Slice(12)) & unchecked((int)0x80000000)) != 0, " +
               $"(byte)((System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian({slice}.Slice(12)) >> 16) & 0x7F))";
    }

    private static string GenerateDecimalWriteExpression(string batchVar, int offset, string valueExpr)
    {
        // This is complex - for now, fall back to individual writes
        // TODO: Optimize decimal batch writing
        return $"/* decimal batch write not yet optimized */ throw new System.NotSupportedException(\"Decimal batch write\")";
    }

    private static bool TryGetFixedCollectionInfo(MemberToGenerate member, TypeToGenerate type, out BatchableInfo info)
    {
        info = default;

        if (member.CollectionInfo is not { } collectionInfo)
            return false;

        if (collectionInfo.CountSize is null or <= 0)
            return false;

        if (collectionInfo.Unlimited || collectionInfo.CountSizeReferenceIndex is not null)
            return false;

        // Polymorphic or custom-serializer collections are not batchable.
        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
            return false;

        if (GeneratorUtilities.ResolveSerializer(member, type) is not null)
            return false;

        var isArray = member.CollectionTypeInfo?.IsArray == true;
        if (!isArray)
            return false;

        var elementType = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName;
        var elementIsUnmanaged = member.ListTypeArgument?.IsUnmanagedType ?? member.CollectionTypeInfo?.IsElementUnmanagedType ?? false;
        var elementIsString = member.ListTypeArgument?.IsStringType ?? member.CollectionTypeInfo?.IsElementStringType ?? false;
        var hasElementSerializerAttr = member.ListTypeArgument?.HasGenerateSerializerAttribute ?? member.CollectionTypeInfo?.HasElementGenerateSerializerAttribute ?? false;

        if (!elementIsUnmanaged || elementIsString || hasElementSerializerAttr)
            return false;

        var elemSize = TypeHelper.GetSizeOf(elementType ?? string.Empty);
        var count = collectionInfo.CountSize.Value;
        var totalSize = elemSize * count;

        info = new BatchableInfo(totalSize, true, count, elemSize, elementType);
        return true;
    }
}

/// <summary>
///     Base class for batch instructions.
/// </summary>
public abstract record BatchInstruction;

/// <summary>
///     A group of consecutive batchable members.
/// </summary>
public sealed record BatchGroup(BatchedMember[] Members, int TotalSize) : BatchInstruction;

/// <summary>
///     A single non-batchable member.
/// </summary>
public sealed record SingleMember(MemberToGenerate Member) : BatchInstruction;

/// <summary>
///     Internal struct describing batchable member info.
/// </summary>
public readonly record struct BatchableInfo(int Size, bool IsFixedCollection, int FixedCount, int ElementSize, string? ElementTypeName);

/// <summary>
///     A member within a batch group.
/// </summary>
public sealed record BatchedMember(MemberToGenerate Member, int Offset, int Size, bool IsFixedCollection, int FixedCount, int ElementSize, string? ElementTypeName);
