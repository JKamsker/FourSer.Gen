using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal readonly record struct MemberPlanFacts(
    int Index,
    string Name,
    string TypeName,
    string SimpleTypeName,
    bool IsScalarFixedSize,
    int? FixedSizeBytes,
    string? ResolvedSerializerTypeName,
    string? ResolvedSerializerFieldName,
    bool HasCountReference,
    bool HasEncodedCount,
    bool HasFixedCount,
    string? CountTypeName,
    int? CountHeaderSizeBytes,
    int? FixedCount,
    bool ByteExactEligible,
    bool PortablePrimitiveEligible,
    bool NativeLayoutEligible,
    CollectionPlan? CollectionPlan,
    PolymorphicPlan? PolymorphicPlan);
