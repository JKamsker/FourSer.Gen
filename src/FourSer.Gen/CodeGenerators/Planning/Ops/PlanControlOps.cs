using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal sealed record DeclareLocalOp(
    string TypeName,
    string Name,
    string? InitializerExpression = null,
    bool UseVar = false,
    string? Comment = null) : PlanOp(Comment);

internal sealed record AssignOp(
    string TargetExpression,
    string ValueExpression,
    string? Comment = null) : PlanOp(Comment);

internal sealed record GuardOp(
    GuardKey Key,
    string Condition,
    GuardScope Scope,
    EquatableArray<PlanOp> Body,
    EquatableArray<PlanOp> ElseBody,
    string? Comment = null) : PlanOp(Comment)
{
    public bool HasElse => !ElseBody.IsEmpty;
}

internal sealed record BarrierOp(
    string Reason,
    string? Comment = null) : PlanOp(Comment);

internal sealed record ThrowOp(
    string ExceptionTypeName,
    string MessageExpression,
    string? ParamNameExpression = null,
    string? Comment = null) : PlanOp(Comment);
