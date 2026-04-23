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
        var sourceExpressions = AddCollectionPreparationPrePass(ops, type);

        foreach (var member in type.Members)
        {
            AddMemberOps(ops, member, type, targetKind, sourceExpressions);
        }

        return ops.ToEquatableArray();
    }

    private static Dictionary<string, string> AddCollectionPreparationPrePass(List<PlanOp> ops, TypeToGenerate type)
    {
        var sourceExpressions = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var member in type.Members)
        {
            if (!ShouldMaterializeCollectionSource(member))
            {
                continue;
            }

            var localName = $"{member.Name.ToCamelCase()}PreparedItems";
            var memberAccess = $"obj.{member.Name}";
            var initializer = member.CollectionTypeInfo?.CanBeNull == true
                ? $"{memberAccess} is null ? null : global::System.Linq.Enumerable.ToList({memberAccess})"
                : $"global::System.Linq.Enumerable.ToList({memberAccess})";

            ops.Add(new DeclareLocalOp(
                TypeName: "var",
                Name: localName,
                InitializerExpression: initializer,
                UseVar: true));
            sourceExpressions[member.Name] = localName;
        }

        return sourceExpressions;
    }

    private static void AddMemberOps(
        List<PlanOp> ops,
        MemberToGenerate member,
        TypeToGenerate type,
        TargetKind targetKind,
        IReadOnlyDictionary<string, string> sourceExpressions)
    {
        var helperName = targetKind == TargetKind.Span
            ? "global::FourSer.Gen.Helpers.SpanWriterHelpers"
            : "global::FourSer.Gen.Helpers.StreamWriterHelpers";
        var targetExpression = targetKind == TargetKind.Span ? "data" : "stream";
        var sourceExpression = GetSourceExpression(member, sourceExpressions);

        if (TryAddCountReferenceWrite(ops, member, type, helperName, targetExpression, sourceExpressions))
        {
            return;
        }

        if (TryAddTypeIdPropertyWrite(ops, member, type, helperName, targetExpression, sourceExpressions))
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
                    CollectionPlan: ApplyCollectionWriteCaching(memoryOwnerPlan.Value, member),
                    SourceExpression: sourceExpression,
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
                    CollectionPlan: ApplyCollectionWriteCaching(collectionPlan.Value, member),
                    SourceExpression: sourceExpression,
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
        string targetExpression,
        IReadOnlyDictionary<string, string> sourceExpressions)
    {
        if (member.IsCountSizeReferenceFor is not { } countReferenceIndex)
        {
            return false;
        }

        var collectionMember = type.Members[countReferenceIndex];
        if (collectionMember.CollectionTypeInfo?.IsPureEnumerable == true
            && !sourceExpressions.ContainsKey(collectionMember.Name))
        {
            return true;
        }

        var collectionSourceExpression = GetSourceExpression(collectionMember, sourceExpressions);
        var countLocalName = PlanExpressionFactory.GetCountLocalName(collectionMember);
        var countExpression = PlanExpressionFactory.GetCountExpression(collectionMember, collectionSourceExpression, nullable: true);
        ops.Add(new DeclareLocalOp(
            TypeName: "var",
            Name: countLocalName,
            InitializerExpression: countExpression,
            UseVar: true));
        ops.Add(new CountWriteOp(
            Key: member.Name,
            TypeName: member.TypeName,
            ValueExpression: countLocalName,
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
        string targetExpression,
        IReadOnlyDictionary<string, string> sourceExpressions)
    {
        if (member.IsTypeIdPropertyFor is not { } typeIdIndex)
        {
            return false;
        }

        var referencedMember = type.Members[typeIdIndex];
        if (referencedMember.PolymorphicInfo is not { } info)
        {
            throw new InvalidOperationException("Type-id property references require polymorphic information.");
        }

        var polymorphicPlan = PolymorphicPlanBuilder.TryCreate(referencedMember);
        if (polymorphicPlan is null)
        {
            return false;
        }

        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        if (!referencedMember.IsList && !referencedMember.IsCollection)
        {
            var typeIdLocalName = PlanExpressionFactory.GetTypeIdLocalName(referencedMember);
            ops.Add(new TypeIdResolveOp(
                Key: referencedMember.Name,
                TargetLocalName: typeIdLocalName,
                TypeIdTypeName: typeIdType,
                InstanceExpression: $"obj.{referencedMember.Name}",
                FallbackExpression: $"obj.{member.Name}",
                PolymorphicPlan: polymorphicPlan.Value,
                IsCollection: false,
                EmitDefaultWhenEmpty: false));
            ops.Add(new ScalarWriteOp(
                Key: member.Name,
                TypeName: typeIdType,
                ValueExpression: typeIdLocalName,
                TargetExpression: targetExpression,
                HelperName: helperName,
                UseCheckedConversion: false));
            return true;
        }

        if (!PolymorphicUtilities.TryGetDefaultOption(info, out var defaultOption))
        {
            throw new InvalidOperationException("Polymorphic members require at least one [PolymorphicOption].");
        }

        var localName = PlanExpressionFactory.GetDiscriminatorLocalName(referencedMember);
        var referencedSourceExpression = GetSourceExpression(referencedMember, sourceExpressions);
        var countExpression = PlanExpressionFactory.GetCountExpression(referencedMember, referencedSourceExpression, nullable: true);
        var defaultKey = PolymorphicUtilities.FormatTypeIdKey(defaultOption.Key, info);

        ops.Add(new DeclareLocalOp(
            TypeName: typeIdType,
            Name: localName,
            InitializerExpression: "default"));
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
                    InstanceExpression: referencedSourceExpression,
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

    private static CollectionPlan? GetCollectionPlan(MemberToGenerate member)
    {
        if (member.IsMemoryOwner)
        {
            return MemoryOwnerPlanBuilder.TryCreate(member);
        }

        if (member.IsList || member.IsCollection)
        {
            return CollectionPlanBuilder.TryCreate(member);
        }

        return null;
    }

    private static string GetSourceExpression(MemberToGenerate member, IReadOnlyDictionary<string, string> sourceExpressions)
    {
        return sourceExpressions.TryGetValue(member.Name, out var sourceExpression)
            ? sourceExpression
            : $"obj.{member.Name}";
    }

    private static bool ShouldMaterializeCollectionSource(MemberToGenerate member)
    {
        if (member.IsMemoryOwner
            || (!member.IsList && !member.IsCollection)
            || member.CollectionTypeInfo is not { } collectionTypeInfo
            || member.CollectionInfo is not { } collectionInfo)
        {
            return false;
        }

        if (collectionTypeInfo.IsPureEnumerable
            && collectionInfo is { Unlimited: false, CountSize: null or < 0 }
            && (!TypeHelper.IsByteCollection(collectionTypeInfo.ElementTypeName)
                || collectionInfo.CountSizeReferenceIndex is not null))
        {
            return true;
        }

        return collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId
            && !collectionTypeInfo.SupportsIndexing
            && !member.IsList;
    }

    private static CollectionPlan ApplyCollectionWriteCaching(CollectionPlan plan, MemberToGenerate member)
    {
        if (member.CollectionInfo?.PolymorphicMode == PolymorphicMode.SingleTypeId
            && member.PolymorphicInfo?.TypeIdPropertyIndex is not null
            && string.IsNullOrEmpty(plan.CachedDiscriminatorLocalName))
        {
            return plan with
            {
                CachedDiscriminatorLocalName = PlanExpressionFactory.GetDiscriminatorLocalName(member),
            };
        }

        return plan;
    }
}
