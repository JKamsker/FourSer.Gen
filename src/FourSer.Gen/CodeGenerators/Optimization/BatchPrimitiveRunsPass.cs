using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal sealed class BatchPrimitiveRunsPass : IPlanPass
{
    public MethodPlan Apply(MethodPlan plan)
    {
        if (!plan.Facts.Options.EnablesBatching)
        {
            return plan;
        }

        if (plan.MethodKind == MethodKind.PacketSize)
        {
            return plan;
        }

        var factsByName = plan.Facts.Members.Array.ToDictionary(f => f.Name, StringComparer.Ordinal);
        var rewritten = RewriteRoot(plan, factsByName);
        return plan with { Ops = rewritten };
    }

    private static EquatableArray<PlanOp> RewriteRoot(
        MethodPlan plan,
        IReadOnlyDictionary<string, MemberPlanFacts> factsByName)
    {
        var rewritten = new List<PlanOp>();
        var pending = new List<(PlanOp Op, BatchMemberPlan BatchMember)>();
        var pendingSize = 0;

        foreach (var op in plan.Ops)
        {
            if (TryGetBatchMember(op, factsByName, out var batchMember))
            {
                if (pendingSize + batchMember.Size > plan.Facts.Options.MaxBatchBytes && pending.Count > 0)
                {
                    FlushPending(plan, rewritten, pending, pendingSize);
                    pending.Clear();
                    pendingSize = 0;
                }

                pending.Add((op, batchMember));
                pendingSize += batchMember.Size;
                continue;
            }

            FlushPending(plan, rewritten, pending, pendingSize);
            pending.Clear();
            pendingSize = 0;
            rewritten.Add(op);
        }

        FlushPending(plan, rewritten, pending, pendingSize);
        return rewritten.ToEquatableArray();
    }

    private static void FlushPending(
        MethodPlan plan,
        List<PlanOp> rewritten,
        List<(PlanOp Op, BatchMemberPlan BatchMember)> pending,
        int pendingSize)
    {
        if (pending.Count == 0)
        {
            return;
        }

        if (pendingSize < plan.Facts.Options.MinBatchBytes)
        {
            rewritten.AddRange(pending.Select(static item => item.Op));
            return;
        }

        var batchMembers = WithOffsets(pending.Select(static item => item.BatchMember)).ToEquatableArray();
        var batchKey = pending[0].Op switch
        {
            ScalarWriteOp scalarWrite => scalarWrite.Key,
            ScalarReadOp scalarRead => scalarRead.Key,
            CollectionWriteOp collectionWrite => collectionWrite.Key,
            CollectionReadOp collectionRead => collectionRead.Key,
            _ => null,
        };
        var bufferName = GetBatchBufferName(batchKey);

        if (plan.MethodKind == MethodKind.Serialize)
        {
            rewritten.Add(new BatchWriteOp(
                Key: batchKey,
                BulkLayoutMode: BulkLayoutMode.PortablePrimitive,
                Members: batchMembers,
                TotalSize: pendingSize,
                BufferName: bufferName,
                TargetExpression: plan.TargetKind == TargetKind.Span ? "data" : "stream"));
            return;
        }

        rewritten.Add(new BatchReadOp(
            Key: batchKey,
            BulkLayoutMode: BulkLayoutMode.PortablePrimitive,
            Members: batchMembers,
            TotalSize: pendingSize,
            BufferName: bufferName,
            SourceExpression: plan.TargetKind == TargetKind.Span ? "buffer" : "stream",
            UseRef: plan.TargetKind == TargetKind.Span));
    }

    private static IEnumerable<BatchMemberPlan> WithOffsets(IEnumerable<BatchMemberPlan> members)
    {
        var offset = 0;
        foreach (var member in members)
        {
            yield return member with { Offset = offset };
            offset += member.Size;
        }
    }

    private static bool TryGetBatchMember(
        PlanOp op,
        IReadOnlyDictionary<string, MemberPlanFacts> factsByName,
        out BatchMemberPlan batchMember)
    {
        switch (op)
        {
            case ScalarWriteOp scalarWrite when scalarWrite.Key is { } key
                                             && factsByName.TryGetValue(key, out var writeFacts)
                                             && writeFacts.IsScalarFixedSize
                                             && writeFacts.FixedSizeBytes is { } writeSize:
                batchMember = new BatchMemberPlan(
                    key,
                    writeFacts.TypeName,
                    scalarWrite.ValueExpression,
                    string.Empty,
                    Offset: 0,
                    Size: writeSize,
                    IsFixedCollection: false,
                    FixedCount: 0,
                    ElementTypeName: null);
                return true;

            case ScalarReadOp scalarRead when scalarRead.Key is { } readKey
                                            && factsByName.TryGetValue(readKey, out var readFacts)
                                            && readFacts.IsScalarFixedSize
                                            && readFacts.FixedSizeBytes is { } readSize:
                batchMember = new BatchMemberPlan(
                    readKey,
                    readFacts.TypeName,
                    string.Empty,
                    scalarRead.TargetExpression,
                    Offset: 0,
                    Size: readSize,
                    IsFixedCollection: false,
                    FixedCount: 0,
                    ElementTypeName: null);
                return true;

            case CollectionWriteOp collectionWrite when IsFixedBatchableCollection(collectionWrite.CollectionPlan, out var collectionWriteSize):
                batchMember = new BatchMemberPlan(
                    collectionWrite.CollectionPlan.MemberName,
                    collectionWrite.CollectionPlan.CollectionTypeName,
                    collectionWrite.SourceExpression,
                    string.Empty,
                    Offset: 0,
                    Size: collectionWriteSize,
                    IsFixedCollection: true,
                    FixedCount: collectionWrite.CollectionPlan.CollectionInfo.CountSize ?? 0,
                    ElementTypeName: collectionWrite.CollectionPlan.ElementTypeName);
                return true;

            case CollectionReadOp collectionRead when IsFixedBatchableCollection(collectionRead.CollectionPlan, out var collectionReadSize):
                batchMember = new BatchMemberPlan(
                    collectionRead.CollectionPlan.MemberName,
                    collectionRead.CollectionPlan.CollectionTypeName,
                    string.Empty,
                    collectionRead.TargetExpression,
                    Offset: 0,
                    Size: collectionReadSize,
                    IsFixedCollection: true,
                    FixedCount: collectionRead.CollectionPlan.CollectionInfo.CountSize ?? 0,
                    ElementTypeName: collectionRead.CollectionPlan.ElementTypeName);
                return true;

            default:
                batchMember = default;
                return false;
        }
    }

    private static bool IsFixedBatchableCollection(CollectionPlan plan, out int totalSize)
    {
        totalSize = 0;

        if (plan.IsMemoryOwner
            || !plan.IsArray
            || plan.CustomSerializer is not null
            || plan.CollectionInfo.Unlimited
            || plan.CollectionInfo.CountSizeReferenceIndex is not null
            || plan.CollectionInfo.CountSize is null or < 0
            || plan.ElementIsStringType
            || plan.ElementHasGenerateSerializerAttribute
            || !plan.ElementIsUnmanagedType)
        {
            return false;
        }

        totalSize = checked(TypeHelper.GetSizeOf(plan.ElementTypeName) * plan.CollectionInfo.CountSize!.Value);
        return true;
    }

    private static string GetBatchBufferName(string? batchKey)
    {
        if (string.IsNullOrWhiteSpace(batchKey))
        {
            return "batch";
        }

        return $"{batchKey!.ToCamelCase()}Batch";
    }
}
