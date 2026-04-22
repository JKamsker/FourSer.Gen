using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal readonly record struct BatchMemberPlan(
    string MemberName,
    string TypeName,
    string ValueExpression,
    string TargetExpression,
    int Offset,
    int Size,
    bool IsFixedCollection,
    int FixedCount,
    string? ElementTypeName);

internal sealed record FixedBytesReadOp(
    string? Key,
    string TargetExpression,
    string SourceExpression,
    string HelperName,
    int ByteCount,
    bool UseRef,
    string? Comment = null) : PlanOp(Comment);

internal sealed record FixedBytesWriteOp(
    string? Key,
    string ValueExpression,
    string TargetExpression,
    string HelperName,
    int ByteCount,
    string? Comment = null) : PlanOp(Comment);

internal sealed record BatchReadOp(
    string? Key,
    BulkLayoutMode BulkLayoutMode,
    EquatableArray<BatchMemberPlan> Members,
    int TotalSize,
    string BufferName,
    string SourceExpression,
    bool UseRef,
    string? Comment = null) : PlanOp(Comment);

internal sealed record BatchWriteOp(
    string? Key,
    BulkLayoutMode BulkLayoutMode,
    EquatableArray<BatchMemberPlan> Members,
    int TotalSize,
    string BufferName,
    string TargetExpression,
    string? Comment = null) : PlanOp(Comment);

internal sealed record CollectionReadOp(
    string? Key,
    CollectionPlan CollectionPlan,
    string TargetExpression,
    string SourceExpression,
    string HelperName,
    bool UseRef,
    string? Comment = null) : PlanOp(Comment);

internal sealed record CollectionWriteOp(
    string? Key,
    CollectionPlan CollectionPlan,
    string SourceExpression,
    string TargetExpression,
    string HelperName,
    TargetKind TargetKind,
    string? Comment = null) : PlanOp(Comment);

internal sealed record CollectionSizeOp(
    string? Key,
    CollectionPlan CollectionPlan,
    string AccessExpression,
    string? Comment = null) : PlanOp(Comment);

internal readonly record struct PolymorphicSwitchCase(
    string LabelExpression,
    string TypeName,
    EquatableArray<PlanOp> Body);

internal sealed record PolymorphicSwitchOp(
    string? Key,
    string SwitchExpression,
    PolymorphicPlan PolymorphicPlan,
    EquatableArray<PolymorphicSwitchCase> Cases,
    EquatableArray<PlanOp> DefaultBody,
    bool IncludeNullCase,
    string? NullCaseMessageExpression = null,
    string? Comment = null) : PlanOp(Comment);
