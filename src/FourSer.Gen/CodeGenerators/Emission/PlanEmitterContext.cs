using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Emission;

internal readonly record struct PlanEmitterContext(
    IndentedStringBuilder Builder,
    MethodPlan Plan)
{
    public MemberToGenerate GetMember(string key)
    {
        return Plan.Facts.Type.Members.First(member => member.Name == key);
    }

    public MemberPlanFacts GetMemberFacts(string key)
    {
        return Plan.Facts.Members.First(member => member.Name == key);
    }

    public void WriteComment(string? comment)
    {
        if (string.IsNullOrEmpty(comment) || !Plan.Facts.Options.EmitOptimizationComments)
        {
            return;
        }

        Builder.WriteLine($"// {comment}");
    }
}
