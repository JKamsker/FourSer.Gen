using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal sealed record PlanFacts(
    TypeToGenerate Type,
    FourSerGeneratorOptions Options,
    TargetCapabilities Capabilities,
    ConstructionPlan ConstructionPlan,
    EquatableArray<MemberPlanFacts> Members);
