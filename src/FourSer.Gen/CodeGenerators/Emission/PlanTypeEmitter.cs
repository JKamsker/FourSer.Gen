using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class PlanTypeEmitter
{
    public static void EmitTypeIdResolve(PlanEmitterContext context, TypeIdResolveOp op)
    {
        if (op.IsCollection)
        {
            EmitCollectionTypeIdResolve(context, op);
            return;
        }

        context.Builder.WriteLine($"switch ({op.InstanceExpression})");
        using (context.Builder.BeginBlock())
        {
            foreach (var option in op.PolymorphicPlan.Options)
            {
                var typeName = TypeHelper.GetGlobalTypeName(option.Type);
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, GetPolymorphicInfo(op));
                context.Builder.WriteLine($"case {typeName} typedInstance:");
                context.Builder.WriteLine($"    {op.TargetLocalName} = {key};");
                context.Builder.WriteLine("    break;");
            }

            context.Builder.WriteLine("case null:");
            context.Builder.WriteLine("    break;");
        }
    }

    public static void EmitSerializeNested(PlanEmitterContext context, SerializeNestedOp op)
    {
        if (op.ThrowOnNull)
        {
            context.Builder.WriteLine($"if ({op.InstanceExpression} is null)");
            using (context.Builder.BeginBlock())
            {
                context.Builder.WriteLine($"throw new System.NullReferenceException({op.NullMessageExpression});");
            }
        }

        if (op.TargetKind == TargetKind.Span)
        {
            context.Builder.WriteLine($"{TypeHelper.GetGlobalTypeName(op.TypeName)}.Serialize({op.InstanceExpression}, ref {op.TargetExpression});");
            return;
        }

        context.Builder.WriteLine($"{TypeHelper.GetGlobalTypeName(op.TypeName)}.Serialize({op.InstanceExpression}, {op.TargetExpression});");
    }

    public static void EmitDeserializeNested(PlanEmitterContext context, DeserializeNestedOp op)
    {
        var refPrefix = op.UseRef ? "ref " : string.Empty;
        context.Builder.WriteLine($"{op.TargetExpression} = {TypeHelper.GetGlobalTypeName(op.TypeName)}.Deserialize({refPrefix}{op.SourceExpression});");
    }

    public static void EmitCustomSerializer(PlanEmitterContext context, CustomSerializerOp op)
    {
        var serializerAccess = $"FourSer.Generated.Internal.__FourSer_Generated_Serializers.{op.SerializerFieldName}";
        var refPrefix = op.UseRef ? "ref " : string.Empty;

        switch (op.Direction)
        {
            case CustomSerializerDirection.Size:
                context.Builder.WriteLine($"size += {serializerAccess}.GetPacketSize({op.InstanceExpression});");
                return;

            case CustomSerializerDirection.Serialize when op.TargetKind == TargetKind.Span:
                context.Builder.WriteLine($"var bytesWritten_{op.Key} = {serializerAccess}.Serialize({op.InstanceExpression}, {op.TargetExpression});");
                context.Builder.WriteLine($"{op.TargetExpression} = {op.TargetExpression}.Slice(bytesWritten_{op.Key});");
                return;

            case CustomSerializerDirection.Serialize:
                context.Builder.WriteLine($"{serializerAccess}.Serialize({op.InstanceExpression}, {op.TargetExpression});");
                return;

            case CustomSerializerDirection.Deserialize:
                context.Builder.WriteLine($"{op.TargetExpression} = {serializerAccess}.Deserialize({refPrefix}{op.SourceExpression});");
                return;

            default:
                throw new NotSupportedException($"Unsupported custom serializer direction: {op.Direction}");
        }
    }

    public static void EmitConstructObject(PlanEmitterContext context, ConstructObjectOp op)
    {
        var constructionPlan = op.ConstructionPlan;
        var constructorArguments = string.Join(", ", constructionPlan.ConstructorArguments.Select(argument => argument.SourceLocalName));
        if (constructionPlan.UsesParameterizedConstructor)
        {
            context.Builder.WriteLine($"var {op.TargetLocalName} = new {constructionPlan.TypeName}({constructorArguments});");
        }
        else
        {
            context.Builder.WriteLine($"var {op.TargetLocalName} = new {constructionPlan.TypeName}();");
        }

        foreach (var assignment in constructionPlan.PostConstructionAssignments)
        {
            context.Builder.WriteLine($"{op.TargetLocalName}.{assignment.MemberName} = {assignment.SourceLocalName};");
        }
    }

    public static void EmitPolymorphicSwitch(PlanEmitterContext context, PolymorphicSwitchOp op)
    {
        context.Builder.WriteLine($"switch ({op.SwitchExpression})");
        using (context.Builder.BeginBlock())
        {
            foreach (var @case in op.Cases)
            {
                context.Builder.WriteLine($"case {@case.LabelExpression}:");
                using (context.Builder.BeginBlock())
                {
                    PlanOpEmitter.EmitOps(context, @case.Body);
                    context.Builder.WriteLine("break;");
                }
            }

            if (op.IncludeNullCase)
            {
                context.Builder.WriteLine("case null:");
                using (context.Builder.BeginBlock())
                {
                    if (!string.IsNullOrEmpty(op.NullCaseMessageExpression))
                    {
                        context.Builder.WriteLine($"throw new System.NullReferenceException({op.NullCaseMessageExpression});");
                    }
                    else
                    {
                        context.Builder.WriteLine("break;");
                    }
                }
            }

            context.Builder.WriteLine("default:");
            using (context.Builder.BeginBlock())
            {
                PlanOpEmitter.EmitOps(context, op.DefaultBody);
            }
        }
    }

    private static void EmitCollectionTypeIdResolve(PlanEmitterContext context, TypeIdResolveOp op)
    {
        var member = context.GetMember(op.Key ?? op.PolymorphicPlan.MemberName);
        var info = GetPolymorphicInfo(op);

        if (op.EmitDefaultWhenEmpty && PolymorphicUtilities.TryGetDefaultOption(info, out var defaultOption))
        {
            var countExpression = PlanExpressionFactory.GetCountExpression(member, op.InstanceExpression, nullable: true);
            context.Builder.WriteLine($"if ({countExpression} == 0)");
            using (context.Builder.BeginBlock())
            {
                var defaultKey = PolymorphicUtilities.FormatTypeIdKey(defaultOption.Key, info);
                context.Builder.WriteLine($"{op.TargetLocalName} = {defaultKey};");
            }

            context.Builder.WriteLine("else");
            using (context.Builder.BeginBlock())
            {
                EmitCollectionTypeIdResolutionSwitch(context, member, op.InstanceExpression, op.TargetLocalName, info);
            }

            return;
        }

        EmitCollectionTypeIdResolutionSwitch(context, member, op.InstanceExpression, op.TargetLocalName, info);
    }

    private static void EmitCollectionTypeIdResolutionSwitch(
        PlanEmitterContext context,
        FourSer.Gen.Models.MemberToGenerate member,
        string instanceExpression,
        string targetLocalName,
        FourSer.Gen.Models.PolymorphicInfo info)
    {
        PolymorphicUtilities.EmitFirstCollectionItemAccess(context.Builder, member, instanceExpression, "firstItem");
        context.Builder.WriteLine($"{targetLocalName} = firstItem switch");
        context.Builder.WriteLine("{");
        context.Builder.Indent();
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        foreach (var option in info.Options)
        {
            var key = PolymorphicUtilities.FormatTypedTypeIdValue(option.Key, info, typeIdType);
            context.Builder.WriteLine($"{PolymorphicUtilities.FormatOptionTypePattern(option)} => {key},");
        }

        context.Builder.WriteLine("_ => throw new System.IO.InvalidDataException($\"Unknown item type: {firstItem.GetType().Name}\")");
        context.Builder.Unindent();
        context.Builder.WriteLine("};");
    }

    private static FourSer.Gen.Models.PolymorphicInfo GetPolymorphicInfo(TypeIdResolveOp op)
    {
        return new FourSer.Gen.Models.PolymorphicInfo(
            op.PolymorphicPlan.TypeIdProperty,
            op.PolymorphicPlan.TypeIdType,
            op.PolymorphicPlan.Options,
            op.PolymorphicPlan.EnumUnderlyingType,
            op.PolymorphicPlan.TypeIdPropertyIndex,
            op.PolymorphicPlan.TypeIdSizeInBytes);
    }
}
