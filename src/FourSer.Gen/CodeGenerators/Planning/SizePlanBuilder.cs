using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal static class SizePlanBuilder
{
    public static EquatableArray<PlanOp> Build(TypeToGenerate type)
    {
        var ops = new List<PlanOp>();

        foreach (var member in type.Members)
        {
            AddMemberOps(ops, member, type);
        }

        return ops.ToEquatableArray();
    }

    private static void AddMemberOps(List<PlanOp> ops, MemberToGenerate member, TypeToGenerate type)
    {
        if (member.IsCountSizeReferenceFor is { } countReferenceIndex)
        {
            var referencedMember = type.Members[countReferenceIndex];
            if (!referencedMember.CollectionTypeInfo?.IsPureEnumerable ?? true)
            {
                var countExpression = PlanExpressionFactory.GetCountExpression(referencedMember, $"obj.{referencedMember.Name}", nullable: true);
                if (GeneratorUtilities.ShouldUseCheckedCountConversion(member.TypeName))
                {
                    ops.Add(new AssignOp(
                        TargetExpression: "_",
                        ValueExpression: $"checked(({member.TypeName})({countExpression}))"));
                }
            }
        }

        var resolvedSerializer = GeneratorUtilities.ResolveSerializer(member, type);
        if (resolvedSerializer is { } serializer)
        {
            ops.Add(new CustomSerializerOp(
                Key: member.Name,
                Direction: CustomSerializerDirection.Size,
                SerializerFieldName: serializer.FieldName,
                TypeName: member.TypeName,
                InstanceExpression: $"obj.{member.Name}",
                TargetExpression: string.Empty,
                SourceExpression: string.Empty,
                HelperName: string.Empty,
                TargetKind: TargetKind.None,
                UseRef: false));
            return;
        }

        if (member.IsMemoryOwner)
        {
            var memoryOwnerPlan = MemoryOwnerPlanBuilder.TryCreate(member);
            if (memoryOwnerPlan is not null)
            {
                ops.Add(new CollectionSizeOp(member.Name, memoryOwnerPlan.Value, $"obj.{member.Name}"));
            }
            return;
        }

        if (member.IsList || member.IsCollection)
        {
            var collectionPlan = CollectionPlanBuilder.TryCreate(member);
            if (collectionPlan is not null)
            {
                ops.Add(new CollectionSizeOp(member.Name, collectionPlan.Value, $"obj.{member.Name}"));
            }
            return;
        }

        if (member.PolymorphicInfo is { } polymorphicInfo)
        {
            if (polymorphicInfo.TypeIdPropertyIndex is null)
            {
                var typeIdType = polymorphicInfo.EnumUnderlyingType ?? polymorphicInfo.TypeIdType;
                var constantValue = TypeHelper.GetSizeOf(typeIdType);
                ops.Add(new SizeAddOp(member.Name, $"sizeof({typeIdType})", constantValue));
            }

            ops.Add(BuildPolymorphicSizeSwitch(member));
            return;
        }

        if (member.HasGenerateSerializerAttribute)
        {
            ops.Add(new SizeAddOp(
                member.Name,
                $"{TypeHelper.GetGlobalTypeName(member.TypeName)}.GetPacketSize(obj.{member.Name})"));
            return;
        }

        if (member.IsStringType)
        {
            ops.Add(new SizeAddOp(member.Name, $"StringEx.MeasureSize(obj.{member.Name})"));
            return;
        }

        if (member.IsUnmanagedType)
        {
            var constantSize = TypeHelper.GetSizeOf(member.TypeName);
            ops.Add(new SizeAddOp(
                member.Name,
                $"sizeof({member.TypeName})",
                constantSize > 0 ? constantSize : null));
        }
    }

    private static PolymorphicSwitchOp BuildPolymorphicSizeSwitch(MemberToGenerate member)
    {
        var info = PolymorphicPlanBuilder.TryCreate(member)!.Value;
        var cases = info.Options.Array
            .Select(option =>
            {
                var typeName = TypeHelper.GetGlobalTypeName(option.Type);
                return new PolymorphicSwitchCase(
                    LabelExpression: $"{typeName} typedInstance",
                    TypeName: typeName,
                    Body: new PlanOp[] { new SizeAddOp(member.Name, $"{typeName}.GetPacketSize(typedInstance)") }.ToEquatableArray());
            })
            .ToEquatableArray();

        var defaultBody = new PlanOp[]
        {
            new ThrowOp(
                ExceptionTypeName: "System.IO.InvalidDataException",
                MessageExpression: PlanExpressionFactory.QuoteInterpolatedString($"Unknown type for {member.Name}: {{obj.{member.Name}?.GetType().FullName}}")),
        }.ToEquatableArray();

        return new PolymorphicSwitchOp(
            Key: member.Name,
            SwitchExpression: $"obj.{member.Name}",
            PolymorphicPlan: info,
            Cases: cases,
            DefaultBody: defaultBody,
            IncludeNullCase: true);
    }
}
