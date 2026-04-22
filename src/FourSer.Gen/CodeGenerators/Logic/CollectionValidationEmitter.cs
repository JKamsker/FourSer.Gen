using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Logic;

internal static class CollectionValidationEmitter
{
    public static void Generate(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        CollectionPlan plan,
        string sourceExpression)
    {
        EmitFixedSizeValidation(sb, member, plan, sourceExpression);

        if (plan.IsMemoryOwner)
        {
            return;
        }

        if (RequiresSingleTypeIdValidation(member))
        {
            EmitSingleTypeIdValidation(sb, member, sourceExpression, member.PolymorphicInfo!.Value);
            return;
        }

        if (RequiresReferenceItemValidation(member, plan))
        {
            EmitReferenceItemValidation(sb, member, plan, sourceExpression);
        }
    }

    private static void EmitFixedSizeValidation(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        CollectionPlan plan,
        string sourceExpression)
    {
        if (plan.CollectionInfo.CountSize is not > 0)
        {
            return;
        }

        sb.WriteLine($"if ({sourceExpression} is null)");
        using (sb.BeginBlock())
        {
            sb.WriteLine($"throw new System.ArgumentNullException(nameof(obj.{member.Name}), \"Fixed-size collections cannot be null.\");");
        }

        var countExpression = GetCountExpression(member, sourceExpression, nullable: false);
        sb.WriteLine($"if ({countExpression} != {plan.CollectionInfo.CountSize})");
        using (sb.BeginBlock())
        {
            sb.WriteLine(
                $"throw new System.InvalidOperationException($\"Collection '{member.Name}' must have a size of {plan.CollectionInfo.CountSize} but was {{{countExpression}}}.\");");
        }
    }

    private static void EmitReferenceItemValidation(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        CollectionPlan plan,
        string sourceExpression)
    {
        var iterationGuard = GeneratorUtilities.GetCollectionIterationGuard(member, sourceExpression);
        if (plan.CanBeNull || iterationGuard is not null)
        {
            var condition = iterationGuard ?? $"{sourceExpression} is not null";
            sb.WriteLine($"if ({condition})");
            using (sb.BeginBlock())
            {
                EmitReferenceItemValidationLoop(sb, member, sourceExpression);
            }

            return;
        }

        EmitReferenceItemValidationLoop(sb, member, sourceExpression);
    }

    private static void EmitReferenceItemValidationLoop(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string sourceExpression)
    {
        sb.WriteLine($"foreach (var item in {sourceExpression})");
        using (sb.BeginBlock())
        {
            sb.WriteLine("if (item is null)");
            using (sb.BeginBlock())
            {
                sb.WriteLine($"throw new System.NullReferenceException({GetItemNullMessage(member)});");
            }
        }
    }

    private static void EmitSingleTypeIdValidation(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string sourceExpression,
        PolymorphicInfo info)
    {
        var countExpression = GetCountExpression(member, sourceExpression, nullable: true);
        sb.WriteLine($"if ({countExpression} > 0)");
        using (sb.BeginBlock())
        {
            const string firstItemVariableName = "firstValidatedItem";
            const string discriminatorVariableName = "validatedDiscriminator";
            const string itemDiscriminatorVariableName = "itemDiscriminator";
            var discriminatorType = info.EnumUnderlyingType ?? info.TypeIdType;

            PolymorphicUtilities.EmitFirstCollectionItemAccess(sb, member, sourceExpression, firstItemVariableName);
            sb.WriteLine($"if ({firstItemVariableName} is null)");
            using (sb.BeginBlock())
            {
                sb.WriteLine($"throw new System.NullReferenceException({GetItemNullMessage(member)});");
            }

            EmitDiscriminatorSwitch(sb, info, discriminatorType, discriminatorVariableName, firstItemVariableName);

            sb.WriteLine($"foreach (var item in {sourceExpression})");
            using (sb.BeginBlock())
            {
                sb.WriteLine("if (item is null)");
                using (sb.BeginBlock())
                {
                    sb.WriteLine($"throw new System.NullReferenceException({GetItemNullMessage(member)});");
                }

                EmitDiscriminatorSwitch(sb, info, discriminatorType, itemDiscriminatorVariableName, "item");
                sb.WriteLine(
                    $"if (!global::System.Collections.Generic.EqualityComparer<{discriminatorType}>.Default.Equals({itemDiscriminatorVariableName}, {discriminatorVariableName}))");
                using (sb.BeginBlock())
                {
                    sb.WriteLine(
                        $"throw new System.IO.InvalidDataException($\"Collection '{member.Name}' contains mixed item types. Expected discriminator {{{discriminatorVariableName}}} but found {{{itemDiscriminatorVariableName}}}.\");");
                }
            }
        }
    }

    private static void EmitDiscriminatorSwitch(
        IndentedStringBuilder sb,
        PolymorphicInfo info,
        string discriminatorType,
        string targetVariableName,
        string itemExpression)
    {
        sb.WriteLine($"var {targetVariableName} = {itemExpression} switch");
        sb.WriteLine("{");
        sb.Indent();
        foreach (var option in info.Options)
        {
            sb.WriteLine(
                $"{PolymorphicUtilities.FormatOptionTypePattern(option)} => {FormatDiscriminatorValue(option, info, discriminatorType)},");
        }

        sb.WriteLine(
            $"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{{itemExpression}.GetType().Name}}\")");
        sb.Unindent();
        sb.WriteLine("};");
    }

    private static string FormatDiscriminatorValue(
        PolymorphicOption option,
        PolymorphicInfo info,
        string discriminatorType)
    {
        return PolymorphicUtilities.FormatTypedTypeIdValue(option.Key, info, discriminatorType);
    }

    private static bool RequiresSingleTypeIdValidation(MemberToGenerate member)
    {
        return GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            && member.CollectionInfo?.PolymorphicMode == PolymorphicMode.SingleTypeId
            && member.PolymorphicInfo is not null;
    }

    private static bool RequiresReferenceItemValidation(MemberToGenerate member, CollectionPlan plan)
    {
        return false;
    }

    private static string GetCountExpression(MemberToGenerate member, string sourceExpression, bool nullable)
    {
        return member.IsMemoryOwner
            ? nullable
                ? $"({sourceExpression}?.Memory.Length ?? 0)"
                : $"{sourceExpression}.Memory.Length"
            : GeneratorUtilities.GetCountExpressionForAccess(member, sourceExpression, nullable);
    }

    private static string GetItemNullMessage(MemberToGenerate member)
    {
        return GeneratorUtilities.ShouldUsePolymorphicSerialization(member)
            ? "\"Item in collection cannot be null.\""
            : "\"Collection item cannot be null.\"";
    }
}
