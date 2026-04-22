using FourSer.Gen.CodeGenerators.Planning;

namespace FourSer.Gen.CodeGenerators.Emission;

internal static class PlanOpEmitter
{
    public static void EmitOps(PlanEmitterContext context, IEnumerable<PlanOp> ops)
    {
        foreach (var op in ops)
        {
            EmitOp(context, op);
        }
    }

    private static void EmitOp(PlanEmitterContext context, PlanOp op)
    {
        context.WriteComment(op.Comment);

        switch (op)
        {
            case DeclareLocalOp declareLocal:
                EmitDeclareLocal(context, declareLocal);
                return;
            case AssignOp assign:
                context.Builder.WriteLine($"{assign.TargetExpression} = {assign.ValueExpression};");
                return;
            case GuardOp guard:
                EmitGuard(context, guard);
                return;
            case BarrierOp barrier:
                context.WriteComment($"barrier: {barrier.Reason}");
                return;
            case ThrowOp throwOp:
                EmitThrow(context, throwOp);
                return;
            case ScalarReadOp scalarRead:
                PlanScalarEmitter.EmitScalarRead(context, scalarRead);
                return;
            case ScalarWriteOp scalarWrite:
                PlanScalarEmitter.EmitScalarWrite(context, scalarWrite);
                return;
            case StringReadOp stringRead:
                PlanScalarEmitter.EmitStringRead(context, stringRead);
                return;
            case StringWriteOp stringWrite:
                PlanScalarEmitter.EmitStringWrite(context, stringWrite);
                return;
            case CountReadOp countRead:
                PlanScalarEmitter.EmitCountRead(context, countRead);
                return;
            case CountWriteOp countWrite:
                PlanScalarEmitter.EmitCountWrite(context, countWrite);
                return;
            case SizeAddOp sizeAdd:
                PlanScalarEmitter.EmitSizeAdd(context, sizeAdd);
                return;
            case TypeIdResolveOp typeIdResolve:
                PlanTypeEmitter.EmitTypeIdResolve(context, typeIdResolve);
                return;
            case TypeIdMutationOp typeIdMutation:
                context.Builder.WriteLine($"{typeIdMutation.TargetExpression} = {typeIdMutation.ValueExpression};");
                return;
            case SerializeNestedOp serializeNested:
                PlanTypeEmitter.EmitSerializeNested(context, serializeNested);
                return;
            case DeserializeNestedOp deserializeNested:
                PlanTypeEmitter.EmitDeserializeNested(context, deserializeNested);
                return;
            case CustomSerializerOp customSerializer:
                PlanTypeEmitter.EmitCustomSerializer(context, customSerializer);
                return;
            case ConstructObjectOp constructObject:
                PlanTypeEmitter.EmitConstructObject(context, constructObject);
                return;
            case FixedBytesReadOp fixedBytesRead:
                PlanCollectionEmitter.EmitFixedBytesRead(context, fixedBytesRead);
                return;
            case FixedBytesWriteOp fixedBytesWrite:
                PlanCollectionEmitter.EmitFixedBytesWrite(context, fixedBytesWrite);
                return;
            case BatchReadOp batchRead:
                PlanCollectionEmitter.EmitBatchRead(context, batchRead);
                return;
            case BatchWriteOp batchWrite:
                PlanCollectionEmitter.EmitBatchWrite(context, batchWrite);
                return;
            case CollectionReadOp collectionRead:
                PlanCollectionEmitter.EmitCollectionRead(context, collectionRead);
                return;
            case CollectionWriteOp collectionWrite:
                PlanCollectionEmitter.EmitCollectionWrite(context, collectionWrite);
                return;
            case CollectionSizeOp collectionSize:
                PlanCollectionEmitter.EmitCollectionSize(context, collectionSize);
                return;
            case PolymorphicSwitchOp polymorphicSwitch:
                PlanTypeEmitter.EmitPolymorphicSwitch(context, polymorphicSwitch);
                return;
            default:
                throw new NotSupportedException($"Unsupported plan op: {op.GetType().Name}");
        }
    }

    private static void EmitDeclareLocal(PlanEmitterContext context, DeclareLocalOp op)
    {
        var typeKeyword = op.UseVar ? "var" : op.TypeName;
        if (string.IsNullOrEmpty(op.InitializerExpression))
        {
            context.Builder.WriteLine($"{typeKeyword} {op.Name};");
            return;
        }

        context.Builder.WriteLine($"{typeKeyword} {op.Name} = {op.InitializerExpression};");
    }

    private static void EmitGuard(PlanEmitterContext context, GuardOp guard)
    {
        context.Builder.WriteLine($"if ({guard.Condition})");
        using (context.Builder.BeginBlock())
        {
            EmitOps(context, guard.Body);
        }

        if (!guard.HasElse)
        {
            return;
        }

        context.Builder.WriteLine("else");
        using (context.Builder.BeginBlock())
        {
            EmitOps(context, guard.ElseBody);
        }
    }

    private static void EmitThrow(PlanEmitterContext context, ThrowOp op)
    {
        if (string.IsNullOrEmpty(op.ParamNameExpression))
        {
            context.Builder.WriteLine($"throw new {op.ExceptionTypeName}({op.MessageExpression});");
            return;
        }

        context.Builder.WriteLine($"throw new {op.ExceptionTypeName}({op.ParamNameExpression}, {op.MessageExpression});");
    }
}
