namespace FourSer.Gen.CodeGenerators.Planning;

internal sealed record TypeIdResolveOp(
    string? Key,
    string TargetLocalName,
    string TypeIdTypeName,
    string InstanceExpression,
    string? FallbackExpression,
    PolymorphicPlan PolymorphicPlan,
    bool IsCollection,
    bool EmitDefaultWhenEmpty,
    string? Comment = null) : PlanOp(Comment);

internal sealed record TypeIdMutationOp(
    string? Key,
    string TargetExpression,
    string ValueExpression,
    string? Comment = null) : PlanOp(Comment);

internal sealed record SerializeNestedOp(
    string? Key,
    string TypeName,
    string InstanceExpression,
    TargetKind TargetKind,
    string TargetExpression,
    bool ThrowOnNull,
    string? NullMessageExpression = null,
    string? Comment = null) : PlanOp(Comment);

internal sealed record DeserializeNestedOp(
    string? Key,
    string TypeName,
    string TargetExpression,
    string SourceExpression,
    string HelperName,
    bool UseRef,
    string? Comment = null) : PlanOp(Comment);

internal enum CustomSerializerDirection
{
    Size,
    Serialize,
    Deserialize,
}

internal sealed record CustomSerializerOp(
    string? Key,
    CustomSerializerDirection Direction,
    string SerializerFieldName,
    string TypeName,
    string? InstanceExpression,
    string TargetExpression,
    string SourceExpression,
    string HelperName,
    TargetKind TargetKind,
    bool UseRef,
    string? Comment = null) : PlanOp(Comment);

internal sealed record ConstructObjectOp(
    string? Key,
    string TargetLocalName,
    ConstructionPlan ConstructionPlan,
    string? Comment = null) : PlanOp(Comment);
