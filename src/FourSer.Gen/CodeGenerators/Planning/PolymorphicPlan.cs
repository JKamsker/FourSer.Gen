using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal readonly record struct PolymorphicPlan(
    string MemberName,
    string TypeIdType,
    string? EnumUnderlyingType,
    string? TypeIdProperty,
    int? TypeIdPropertyIndex,
    int TypeIdSizeInBytes,
    PolymorphicMode PolymorphicMode,
    EquatableArray<PolymorphicOption> Options,
    string? CachedDiscriminatorLocalName,
    bool HoistSingleTypeCollectionSwitch,
    bool BatchFixedHeaders);
