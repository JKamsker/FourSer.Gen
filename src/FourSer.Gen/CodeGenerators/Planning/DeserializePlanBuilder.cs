using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal static class DeserializePlanBuilder
{
    public static EquatableArray<PlanOp> Build(TypeToGenerate type, TargetKind targetKind)
    {
        var ops = new List<PlanOp>();
        AddMemberDeclarations(ops, type);

        foreach (var member in type.Members)
        {
            AddMemberReadOps(ops, member, type, targetKind);
        }

        ops.Add(new ConstructObjectOp(
            Key: type.Name,
            TargetLocalName: "obj",
            ConstructionPlan: ConstructionPlanner.CreatePlan(type)));

        return ops.ToEquatableArray();
    }

    private static void AddMemberDeclarations(List<PlanOp> ops, TypeToGenerate type)
    {
        foreach (var member in type.Members)
        {
            if (ShouldInferCollectionLocal(member))
            {
                continue;
            }

            var initializer = member.PolymorphicInfo is not null
                ? "default"
                : null;

            ops.Add(new DeclareLocalOp(
                TypeName: member.TypeName,
                Name: PlanExpressionFactory.GetMemberLocalName(member),
                InitializerExpression: initializer));

            if (member.PolymorphicInfo is { TypeIdPropertyIndex: null } info
                && !member.IsCollection
                && !member.IsList)
            {
                ops.Add(new DeclareLocalOp(
                    TypeName: info.TypeIdType,
                    Name: PlanExpressionFactory.GetTypeIdLocalName(member)));
            }
        }
    }

    private static void AddMemberReadOps(
        List<PlanOp> ops,
        MemberToGenerate member,
        TypeToGenerate type,
        TargetKind targetKind)
    {
        var sourceExpression = targetKind.GetReadSourceExpression();
        var helperName = targetKind.GetReaderHelperName();
        var useRef = targetKind.UsesRefReadSource();
        var targetLocalName = PlanExpressionFactory.GetMemberLocalName(member);
        var collectionTargetExpression = ShouldInferCollectionLocal(member)
            ? $"var {targetLocalName}"
            : targetLocalName;

        var resolvedSerializer = GeneratorUtilities.ResolveSerializer(member, type);
        if (resolvedSerializer is { } serializer)
        {
            ops.Add(new CustomSerializerOp(
                Key: member.Name,
                Direction: CustomSerializerDirection.Deserialize,
                SerializerFieldName: serializer.FieldName,
                TypeName: member.TypeName,
                InstanceExpression: null,
                TargetExpression: targetLocalName,
                SourceExpression: sourceExpression,
                HelperName: helperName,
                TargetKind: targetKind,
                UseRef: useRef));
            return;
        }

        if (member.IsMemoryOwner)
        {
            var memoryOwnerPlan = MemoryOwnerPlanBuilder.TryCreate(member);
            if (memoryOwnerPlan is not null)
            {
                ops.Add(new CollectionReadOp(
                    Key: member.Name,
                    CollectionPlan: memoryOwnerPlan.Value,
                    TargetExpression: collectionTargetExpression,
                    SourceExpression: sourceExpression,
                    HelperName: helperName,
                    UseRef: useRef));
            }
            return;
        }

        if (member.IsList || member.IsCollection)
        {
            var collectionPlan = CollectionPlanBuilder.TryCreate(member);
            if (collectionPlan is not null)
            {
                ops.Add(new CollectionReadOp(
                    Key: member.Name,
                    CollectionPlan: collectionPlan.Value,
                    TargetExpression: collectionTargetExpression,
                    SourceExpression: sourceExpression,
                    HelperName: helperName,
                    UseRef: useRef));
            }
            return;
        }

        if (member.PolymorphicInfo is not null)
        {
            AddPolymorphicReadOps(ops, member, targetKind, sourceExpression, helperName, useRef, targetLocalName);
            return;
        }

        if (member.HasGenerateSerializerAttribute)
        {
            ops.Add(new DeserializeNestedOp(
                Key: member.Name,
                TypeName: member.TypeName,
                TargetExpression: targetLocalName,
                SourceExpression: sourceExpression,
                HelperName: helperName,
                UseRef: useRef));
            return;
        }

        if (member.IsStringType)
        {
            ops.Add(new StringReadOp(
                Key: member.Name,
                TargetExpression: targetLocalName,
                SourceExpression: sourceExpression,
                HelperName: helperName,
                UseRef: useRef));
            return;
        }

        if (member.IsUnmanagedType)
        {
            ops.Add(new ScalarReadOp(
                Key: member.Name,
                TypeName: member.TypeName,
                TargetExpression: targetLocalName,
                SourceExpression: sourceExpression,
                HelperName: helperName,
                UseRef: useRef));
        }
    }

    private static void AddPolymorphicReadOps(
        List<PlanOp> ops,
        MemberToGenerate member,
        TargetKind targetKind,
        string sourceExpression,
        string helperName,
        bool useRef,
        string targetLocalName)
    {
        var plan = PolymorphicPlanBuilder.TryCreate(member)!.Value;
        var info = member.PolymorphicInfo!.Value;
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        var switchExpression = info.TypeIdPropertyIndex is not null
            ? (info.TypeIdProperty ?? "typeId").ToCamelCase()
            : PlanExpressionFactory.GetTypeIdLocalName(member);

        if (info.TypeIdPropertyIndex is null)
        {
            ops.Add(new CountReadOp(
                Key: member.Name,
                TypeName: typeIdType,
                TargetExpression: switchExpression,
                SourceExpression: sourceExpression,
                HelperName: helperName,
                UseRef: useRef,
                CastTypeName: info.EnumUnderlyingType is not null ? info.TypeIdType : null));
        }

        var cases = info.Options.Array
            .Select(option => new PolymorphicSwitchCase(
                LabelExpression: PolymorphicUtilities.FormatTypeIdKey(option.Key, info),
                TypeName: TypeHelper.GetGlobalTypeName(option.Type),
                Body: new PlanOp[]
                {
                    new DeserializeNestedOp(
                        Key: member.Name,
                        TypeName: option.Type,
                        TargetExpression: targetLocalName,
                        SourceExpression: sourceExpression,
                        HelperName: helperName,
                        UseRef: useRef),
                }.ToEquatableArray()))
            .ToEquatableArray();

        ops.Add(new PolymorphicSwitchOp(
            Key: member.Name,
            SwitchExpression: switchExpression,
            PolymorphicPlan: plan,
            Cases: cases,
            DefaultBody: new PlanOp[]
            {
                new ThrowOp(
                    ExceptionTypeName: "System.IO.InvalidDataException",
                    MessageExpression: PlanExpressionFactory.QuoteInterpolatedString($"Unknown type id for {member.Name}: {{{switchExpression}}}")),
            }.ToEquatableArray(),
            IncludeNullCase: false));
    }

    private static bool ShouldInferCollectionLocal(MemberToGenerate member)
    {
        if (!member.IsCollection && !member.IsList)
        {
            return false;
        }

        if (member.CollectionTypeInfo?.IsPureEnumerable == true)
        {
            return true;
        }

        if (member.CollectionTypeInfo?.IsReadOnlyInterface == true)
        {
            return true;
        }

        return CollectionUtilities.ShouldDeserializeIntoStagingCollection(member);
    }
}
