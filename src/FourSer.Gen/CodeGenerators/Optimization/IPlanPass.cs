using FourSer.Gen.CodeGenerators.Planning;

namespace FourSer.Gen.CodeGenerators.Optimization;

internal interface IPlanPass
{
    MethodPlan Apply(MethodPlan plan);
}
