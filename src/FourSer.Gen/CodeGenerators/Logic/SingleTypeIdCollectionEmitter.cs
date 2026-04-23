using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Helpers;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Logic;

internal static class SingleTypeIdCollectionEmitter
{
    public static void EmitValidatedDiscriminatorResolution(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        PolymorphicInfo info,
        string collectionExpression,
        string discriminatorTarget,
        bool declareVariable)
    {
        if (CanUseIndexedAccess(member, collectionExpression))
        {
            EmitIndexedDiscriminatorResolution(builder, member, info, collectionExpression, discriminatorTarget, declareVariable);
            return;
        }

        EmitEnumeratedDiscriminatorResolution(builder, member, info, collectionExpression, discriminatorTarget, declareVariable);
    }

    public static void EmitValidatedPayload(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx writerContext,
        PolymorphicInfo info,
        string collectionExpression,
        string discriminatorExpression)
    {
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        builder.WriteLine($"switch ({discriminatorExpression})");
        using (builder.BeginBlock())
        {
            foreach (var option in info.Options)
            {
                var key = PolymorphicUtilities.FormatTypedTypeIdValue(option.Key, info, typeIdType);
                var typeName = TypeHelper.GetGlobalTypeName(option.Type);
                builder.WriteLine($"case {key}:");
                using (builder.BeginBlock())
                {
                    EmitTypedPayloadLoop(builder, member, writerContext, collectionExpression, typeName);
                    builder.WriteLine("break;");
                }
            }

            builder.WriteLine("default:");
            using (builder.BeginBlock())
            {
                builder.WriteLine($"throw new System.IO.InvalidDataException($\"Unknown type id for {member.Name}: {{{discriminatorExpression}}}\");");
            }
        }
    }

    private static void EmitIndexedDiscriminatorResolution(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        PolymorphicInfo info,
        string collectionExpression,
        string discriminatorTarget,
        bool declareVariable)
    {
        var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, collectionExpression);
        builder.WriteLine($"var firstValidatedItem = {collectionExpression}[0];");
        EmitNullGuard(builder, "firstValidatedItem");
        EmitDiscriminatorAssignment(builder, info, "firstValidatedItem", discriminatorTarget, declareVariable);

        builder.WriteLine($"for (int i = 1; i < {countExpression}; i++)");
        using (builder.BeginBlock())
        {
            builder.WriteLine($"var item = {collectionExpression}[i];");
            EmitNullGuard(builder, "item");
            EmitItemDiscriminatorValidation(builder, member, info, discriminatorTarget);
        }
    }

    private static void EmitEnumeratedDiscriminatorResolution(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        PolymorphicInfo info,
        string collectionExpression,
        string discriminatorTarget,
        bool declareVariable)
    {
        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName
            ?? throw new InvalidOperationException("Polymorphic collection members require element type information.");

        builder.WriteLine(
            $"using var validatedEnumerator = ((global::System.Collections.Generic.IEnumerable<{elementTypeName}>){collectionExpression}).GetEnumerator();");
        builder.WriteLine("if (!validatedEnumerator.MoveNext())");
        using (builder.BeginBlock())
        {
            builder.WriteLine("throw new System.InvalidOperationException(\"Collection must contain at least one item.\");");
        }

        builder.WriteLine("var firstValidatedItem = validatedEnumerator.Current;");
        EmitNullGuard(builder, "firstValidatedItem");
        EmitDiscriminatorAssignment(builder, info, "firstValidatedItem", discriminatorTarget, declareVariable);

        builder.WriteLine("while (validatedEnumerator.MoveNext())");
        using (builder.BeginBlock())
        {
            builder.WriteLine("var item = validatedEnumerator.Current;");
            EmitNullGuard(builder, "item");
            EmitItemDiscriminatorValidation(builder, member, info, discriminatorTarget);
        }
    }

    private static void EmitDiscriminatorAssignment(
        IndentedStringBuilder builder,
        PolymorphicInfo info,
        string itemExpression,
        string discriminatorTarget,
        bool declareVariable)
    {
        var assignmentKeyword = declareVariable ? "var " : string.Empty;
        builder.WriteLine($"{assignmentKeyword}{discriminatorTarget} = {itemExpression} switch");
        builder.WriteLine("{");
        builder.Indent();
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        foreach (var option in info.Options)
        {
            var key = PolymorphicUtilities.FormatTypedTypeIdValue(option.Key, info, typeIdType);
            builder.WriteLine($"{PolymorphicUtilities.FormatOptionTypePattern(option)} => {key},");
        }

        builder.WriteLine($"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{{itemExpression}.GetType().Name}}\")");
        builder.Unindent();
        builder.WriteLine("};");
    }

    private static void EmitItemDiscriminatorValidation(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        PolymorphicInfo info,
        string discriminatorTarget)
    {
        var discriminatorType = info.EnumUnderlyingType ?? info.TypeIdType;
        EmitDiscriminatorAssignment(builder, info, "item", "itemDiscriminator", declareVariable: true);
        builder.WriteLine(
            $"if (!global::System.Collections.Generic.EqualityComparer<{discriminatorType}>.Default.Equals(itemDiscriminator, {discriminatorTarget}))");
        using (builder.BeginBlock())
        {
            builder.WriteLine(
                $"throw new System.IO.InvalidDataException($\"Collection '{member.Name}' contains mixed item types. Expected discriminator {{{discriminatorTarget}}} but found {{itemDiscriminator}}.\");");
        }
    }

    private static void EmitTypedPayloadLoop(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx writerContext,
        string collectionExpression,
        string typeName)
    {
        var typedItemName = $"{TypeHelper.GetSimpleTypeName(typeName).ToCamelCase()}Item";
        if (CanUseIndexedAccess(member, collectionExpression))
        {
            var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, collectionExpression);
            builder.WriteLine($"for (int i = 0; i < {countExpression}; i++)");
            using (builder.BeginBlock())
            {
                builder.WriteLine($"var {typedItemName} = ({typeName}){collectionExpression}[i];");
                EmitTypedSerialization(builder, writerContext, typeName, typedItemName);
            }

            return;
        }

        builder.WriteLine($"foreach (var item in {collectionExpression})");
        using (builder.BeginBlock())
        {
            builder.WriteLine($"var {typedItemName} = ({typeName})item;");
            EmitTypedSerialization(builder, writerContext, typeName, typedItemName);
        }
    }

    private static void EmitTypedSerialization(
        IndentedStringBuilder builder,
        SerializationWriterEmitter.WriterCtx writerContext,
        string typeName,
        string itemExpression)
    {
        if (writerContext.IsSpan)
        {
            builder.WriteLine($"{typeName}.Serialize({itemExpression}, ref {writerContext.Target});");
            return;
        }

        builder.WriteLine($"{typeName}.Serialize({itemExpression}, {writerContext.Target});");
    }

    private static void EmitNullGuard(IndentedStringBuilder builder, string itemExpression)
    {
        builder.WriteLine($"if ({itemExpression} is null)");
        using (builder.BeginBlock())
        {
            builder.WriteLine("throw new System.NullReferenceException(\"Item in collection cannot be null.\");");
        }
    }

    private static bool CanUseIndexedAccess(MemberToGenerate member, string collectionExpression)
    {
        return member.CollectionTypeInfo?.SupportsIndexing == true
            || member.IsList
            || collectionExpression != $"obj.{member.Name}";
    }
}
