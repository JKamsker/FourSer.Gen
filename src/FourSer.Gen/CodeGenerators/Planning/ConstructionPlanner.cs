using System.Collections.Immutable;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal static class ConstructionPlanner
{
    public static ConstructionPlan CreatePlan(TypeToGenerate type)
    {
        if (type.Constructor is not { } constructor)
        {
            return new ConstructionPlan(
                type.Name,
                UsesParameterizedConstructor: false,
                HasParameterlessConstructor: !type.IsValueType,
                ConstructorArguments: ImmutableArray<ConstructionArgument>.Empty,
                PostConstructionAssignments: GetAssignments(type, ImmutableHashSet<string>.Empty));
        }

        var constructorArguments = constructor.Parameters
            .Select(parameter => new ConstructionArgument(
                parameter.Name,
                parameter.TypeName,
                parameter.Name.ToCamelCase()))
            .ToImmutableArray();

        var membersInConstructor = constructor.Parameters
            .Select(static parameter => parameter.Name)
            .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

        return new ConstructionPlan(
            type.Name,
            UsesParameterizedConstructor: constructor.Parameters.Count > 0,
            HasParameterlessConstructor: constructor.HasParameterlessConstructor,
            ConstructorArguments: constructorArguments,
            PostConstructionAssignments: GetAssignments(type, membersInConstructor));
    }

    private static EquatableArray<ConstructionAssignment> GetAssignments(
        TypeToGenerate type,
        ImmutableHashSet<string> membersInConstructor)
    {
        var assignments = type.Members
            .Where(member => !membersInConstructor.Contains(member.Name))
            .Select(member => new ConstructionAssignment(
                member.Name,
                PlanExpressionFactory.GetMemberLocalName(member)))
            .ToImmutableArray();

        return assignments;
    }
}
