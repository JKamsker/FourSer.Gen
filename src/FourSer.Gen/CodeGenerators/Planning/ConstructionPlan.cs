using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal readonly record struct ConstructionArgument(
    string ParameterName,
    string TypeName,
    string SourceLocalName);

internal readonly record struct ConstructionAssignment(
    string MemberName,
    string SourceLocalName);

internal readonly record struct ConstructionPlan(
    string TypeName,
    bool UsesParameterizedConstructor,
    bool HasParameterlessConstructor,
    EquatableArray<ConstructionArgument> ConstructorArguments,
    EquatableArray<ConstructionAssignment> PostConstructionAssignments);
