using System.Collections.Immutable;
using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal static class SerializePlanBuilder
{
    public static EquatableArray<PlanOp> Build(TypeToGenerate type, TargetKind targetKind)
    {
        var ops = new List<PlanOp>();
        AddTypeIdMutationPrePass(ops, type);

        foreach (var member in type.Members)
        {
            AddMemberOps(ops, member, type, targetKind);
        }

        return ops.ToEquatableArray();
    }

    private static void AddTypeIdMutationPrePass(List<PlanOp> ops, TypeToGenerate type)
    {
        foreach (var member in type.Members)
        {
            if (member.PolymorphicInfo is not { TypeIdPropertyIndex: not null } info)
            {
                continue;
            }

            if ((member.IsList || member.IsCollection) &&
                member.CollectionInfo?.PolymorphicMode == FourSer.Gen.PolymorphicMode.SingleTypeId)
            {
                continue;
            }

            var polymorphicPlan = PolymorphicPlanBuilder.TryCreate(member);
            if (polymorphicPlan is null)
            {
                continue;
            }

            var typeIdLocalName = PlanExpressionFactory.GetTypeIdLocalName(member);
            var targetMember = type.Members[info.TypeIdPropertyIndex!.Value];
            ops.Add(new TypeIdResolveOp(
                Key: member.Name,
                TargetLocalName: typeIdLocalName,
                TypeIdTypeName: info.TypeIdType,
                InstanceExpression: $"obj.{member.Name}",
                FallbackExpression: $"obj.{targetMember.Name}",
                PolymorphicPlan: polymorphicPlan.Value,
                IsCollection: member.IsList || member.IsCollection,
                EmitDefaultWhenEmpty: false));
            ops.Add(new TypeIdMutationOp(
                Key: targetMember.Name,
                TargetExpression: $"obj.{targetMember.Name}",
                ValueExpression: typeIdLocalName));
        }
    }

    private static void AddMemberOps(
        List<PlanOp> ops,
        MemberToGenerate member,
        TypeToGenerate type,
        TargetKind targetKind)
    {
        var helperName = targetKind == TargetKind.Span ? "SpanWriter" : "StreamWriter";
        var targetExpression = targetKind == TargetKind.Span ? "data" : "stream";

        if (TryAddCountReferenceWrite(ops, member, type, helperName, targetExpression))
        {
            return;
        }

        if (TryAddTypeIdPropertyWrite(ops, member, type, helperName, targetExpression))
        {
            return;
        }

        var resolvedSerializer = GeneratorUtilities.ResolveSerializer(member, type);
        if (resolvedSerializer is { } serializer)
        {
            ops.Add(new CustomSerializerOp(
                Key: member.Name,
                Direction: CustomSerializerDirection.Serialize,
                SerializerFieldName: serializer.FieldName,
                TypeName: member.TypeName,
                InstanceExpression: $"obj.{member.Name}",
                TargetExpression: targetExpression,
                SourceExpression: string.Empty,
                HelperName: helperName,
                TargetKind: targetKind,
                UseRef: targetKind == TargetKind.Span));
            return;
        }

        if (member.IsMemoryOwner)
        {
            var memoryOwnerPlan = MemoryOwnerPlanBuilder.TryCreate(member);
            if (memoryOwnerPlan is not null)
            {
                ops.Add(new CollectionWriteOp(
                    Key: member.Name,
                    CollectionPlan: memoryOwnerPlan.Value,
                    SourceExpression: $"obj.{member.Name}",
                    TargetExpression: targetExpression,
                    HelperName: helperName,
                    TargetKind: targetKind));
            }
            return;
        }

        if (member.IsList || member.IsCollection)
        {
            var collectionPlan = CollectionPlanBuilder.TryCreate(member);
            if (collectionPlan is not null)
            {
                ops.Add(new CollectionWriteOp(
                    Key: member.Name,
                    CollectionPlan: collectionPlan.Value,
                    SourceExpression: $"obj.{member.Name}",
                    TargetExpression: targetExpression,
                    HelperName: helperName,
                    TargetKind: targetKind));
            }
            return;
        }

        if (member.PolymorphicInfo is not null)
        {
            ops.Add(BuildPolymorphicSerializeSwitch(member, targetKind, helperName, targetExpression));
            return;
        }

        if (member.HasGenerateSerializerAttribute)
        {
            ops.Add(new SerializeNestedOp(
                Key: member.Name,
                TypeName: member.TypeName,
                InstanceExpression: $"obj.{member.Name}",
                TargetKind: targetKind,
                TargetExpression: targetExpression,
                ThrowOnNull: !member.IsValueType,
                NullMessageExpression: PlanExpressionFactory.QuoteString($"Member \"obj.{member.Name}\" cannot be null.")));
            return;
        }

        if (member.IsStringType)
        {
            ops.Add(new StringWriteOp(
                Key: member.Name,
                ValueExpression: $"obj.{member.Name}",
                TargetExpression: targetExpression,
                HelperName: helperName,
                UseFusedStreamWrite: false));
            return;
        }

        if (member.IsUnmanagedType)
        {
            ops.Add(new ScalarWriteOp(
                Key: member.Name,
                TypeName: member.TypeName,
                ValueExpression: $"obj.{member.Name}",
                TargetExpression: targetExpression,
                HelperName: helperName,
                UseCheckedConversion: false));
        }
    }

    private static bool TryAddCountReferenceWrite(
        List<PlanOp> ops,
        MemberToGenerate member,
        TypeToGenerate type,
        string helperName,
        string targetExpression)
    {
        if (member.IsCountSizeReferenceFor is not { } countReferenceIndex)
        {
            return false;
        }

        var collectionMember = type.Members[countReferenceIndex];
        if (collectionMember.CollectionTypeInfo?.IsPureEnumerable == true)
        {
            return true;
        }

        var countExpression = PlanExpressionFactory.GetCountExpression(collectionMember, $"obj.{collectionMember.Name}", nullable: true);
        ops.Add(new CountWriteOp(
            Key: member.Name,
            TypeName: member.TypeName,
            ValueExpression: countExpression,
            TargetExpression: targetExpression,
            HelperName: helperName,
            UseCheckedConversion: GeneratorUtilities.ShouldUseCheckedCountConversion(member.TypeName)));
        return true;
    }

    private static bool TryAddTypeIdPropertyWrite(
        List<PlanOp> ops,
        MemberToGenerate member,
        TypeToGenerate type,
        string helperName,
        string targetExpression)
    {
        if (member.IsTypeIdPropertyFor is not { } typeIdIndex)
        {
            return false;
        }

        var referencedMember = type.Members[typeIdIndex];
        if (!referencedMember.IsList && !referencedMember.IsCollection)
        {
            return false;
        }

        if (referencedMember.PolymorphicInfo is not { } info)
        {
            throw new InvalidOperationException("Type-id property references require polymorphic information.");
        }

        var polymorphicPlan = PolymorphicPlanBuilder.TryCreate(referencedMember);
        if (polymorphicPlan is null)
        {
            return false;
        }

        if (!PolymorphicUtilities.TryGetDefaultOption(info, out var defaultOption))
        {
            throw new InvalidOperationException("Polymorphic members require at least one [PolymorphicOption].");
        }

        var localName = PlanExpressionFactory.GetDiscriminatorLocalName(referencedMember);
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        var countExpression = PlanExpressionFactory.GetCountExpression(referencedMember, $"obj.{referencedMember.Name}", nullable: true);
        var defaultKey = PolymorphicUtilities.FormatTypeIdKey(defaultOption.Key, info);

        ops.Add(new GuardOp(
            Key: new GuardKey("collection-type-id-property", referencedMember.Name, member.Name),
            Condition: $"{countExpression} == 0",
            Scope: GuardScope.Method,
            Body: new PlanOp[]
            {
                new ScalarWriteOp(
                    Key: member.Name,
                    TypeName: typeIdType,
                    ValueExpression: defaultKey,
                    TargetExpression: targetExpression,
                    HelperName: helperName,
                    UseCheckedConversion: false),
            }.ToEquatableArray(),
            ElseBody: new PlanOp[]
            {
                new TypeIdResolveOp(
                    Key: referencedMember.Name,
                    TargetLocalName: localName,
                    TypeIdTypeName: typeIdType,
                    InstanceExpression: $"obj.{referencedMember.Name}",
                    FallbackExpression: null,
                    PolymorphicPlan: polymorphicPlan.Value,
                    IsCollection: true,
                    EmitDefaultWhenEmpty: false),
                new ScalarWriteOp(
                    Key: member.Name,
                    TypeName: typeIdType,
                    ValueExpression: localName,
                    TargetExpression: targetExpression,
                    HelperName: helperName,
                    UseCheckedConversion: false),
            }.ToEquatableArray()));
        return true;
    }

    private static PolymorphicSwitchOp BuildPolymorphicSerializeSwitch(
        MemberToGenerate member,
        TargetKind targetKind,
        string helperName,
        string targetExpression)
    {
        var plan = PolymorphicPlanBuilder.TryCreate(member)!.Value;
        var typeIdType = plan.EnumUnderlyingType ?? plan.TypeIdType;
        var cases = ImmutableArray.CreateBuilder<PolymorphicSwitchCase>();

        foreach (var option in plan.Options)
        {
            var caseOps = new List<PlanOp>();
            if (plan.TypeIdPropertyIndex is null)
            {
                caseOps.Add(new ScalarWriteOp(
                    Key: member.Name,
                    TypeName: typeIdType,
                    ValueExpression: PolymorphicUtilities.FormatTypeIdKey(option.Key, member.PolymorphicInfo!.Value),
                    TargetExpression: targetExpression,
                    HelperName: helperName,
                    UseCheckedConversion: false));
            }

            caseOps.Add(new SerializeNestedOp(
                Key: member.Name,
                TypeName: option.Type,
                InstanceExpression: "typedInstance",
                TargetKind: targetKind,
                TargetExpression: targetExpression,
                ThrowOnNull: false));

            var optionTypeName = TypeHelper.GetGlobalTypeName(option.Type);
            cases.Add(new PolymorphicSwitchCase(
                LabelExpression: $"{optionTypeName} typedInstance",
                TypeName: optionTypeName,
                Body: caseOps.ToEquatableArray()));
        }

        return new PolymorphicSwitchOp(
            Key: member.Name,
            SwitchExpression: $"obj.{member.Name}",
            PolymorphicPlan: plan,
            Cases: cases.ToImmutable().ToEquatableArray(),
            DefaultBody: new PlanOp[]
            {
                new ThrowOp(
                    ExceptionTypeName: "System.IO.InvalidDataException",
                    MessageExpression: PlanExpressionFactory.QuoteInterpolatedString($"Unknown type for {member.Name}: {{obj.{member.Name}?.GetType().FullName}}")),
            }.ToEquatableArray(),
            IncludeNullCase: true,
            NullCaseMessageExpression: PlanExpressionFactory.QuoteString($"Property \"{member.Name}\" cannot be null."));
    }
}
