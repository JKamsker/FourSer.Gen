namespace FourSer.Gen.CodeGenerators.Planning;

internal sealed record ScalarReadOp(
    string? Key,
    string TypeName,
    string TargetExpression,
    string SourceExpression,
    string HelperName,
    bool UseRef,
    string? Comment = null) : PlanOp(Comment);

internal sealed record ScalarWriteOp(
    string? Key,
    string TypeName,
    string ValueExpression,
    string TargetExpression,
    string HelperName,
    bool UseCheckedConversion,
    string? Comment = null) : PlanOp(Comment);

internal sealed record StringReadOp(
    string? Key,
    string TargetExpression,
    string SourceExpression,
    string HelperName,
    bool UseRef,
    string? Comment = null) : PlanOp(Comment);

internal sealed record StringWriteOp(
    string? Key,
    string ValueExpression,
    string TargetExpression,
    string HelperName,
    bool UseFusedStreamWrite,
    string? Comment = null) : PlanOp(Comment);

internal sealed record CountReadOp(
    string? Key,
    string TypeName,
    string TargetExpression,
    string SourceExpression,
    string HelperName,
    bool UseRef,
    string? CastTypeName = null,
    string? Comment = null) : PlanOp(Comment);

internal sealed record CountWriteOp(
    string? Key,
    string TypeName,
    string ValueExpression,
    string TargetExpression,
    string HelperName,
    bool UseCheckedConversion,
    bool IsPlaceholder = false,
    string? ReservationName = null,
    string? Comment = null) : PlanOp(Comment);

internal sealed record SizeAddOp(
    string? Key,
    string Expression,
    int? ConstantValue = null,
    string? CacheKey = null,
    string? Comment = null) : PlanOp(Comment);
