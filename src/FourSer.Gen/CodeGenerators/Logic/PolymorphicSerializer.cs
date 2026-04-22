using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Helpers;
using FourSer.Gen.Models;
using FourSer.Gen.Helpers;

namespace FourSer.Gen.CodeGenerators.Logic;

internal static class PolymorphicSerializer
{
    public static void GeneratePolymorphicMember(IndentedStringBuilder sb, MemberToGenerate member, SerializationWriterEmitter.WriterCtx ctx)
    {
        var info = member.PolymorphicInfo!.Value;

        sb.WriteLineFormat("switch (obj.{0})", member.Name);
        using var _ = sb.BeginBlock();

        GeneratePolymorphicSerializationLogic
        (
            sb,
            ctx,
            info
        );

        sb.WriteLine("case null:");
        sb.WriteLineFormat
            ("    throw new System.NullReferenceException($\"Property \\\"{0}\\\" cannot be null.\");", member.Name);
        sb.WriteLine("default:");
        sb.WriteLineFormat
        (
            "    throw new System.IO.InvalidDataException($\"Unknown type for {0}: {{obj.{0}?.GetType().FullName}}\");",
            member.Name
        );
    }

    public static void GeneratePolymorphicCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        string collectionExpression)
    {
        if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
        {
            GenerateIndividualTypeIdCollection(sb, member, ctx, collectionExpression);
        }
        else if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
        {
            GenerateSingleTypeIdPolymorphicCollection
            (
                sb,
                member,
                ctx,
                collectionInfo,
                collectionExpression
            );
        }
    }

    private static void GeneratePolymorphicItemSerialization
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string instanceName,
        SerializationWriterEmitter.WriterCtx ctx
    )
    {
        var info = member.PolymorphicInfo!.Value;

        sb.WriteLineFormat("switch ({0})", instanceName);
        using var _ = sb.BeginBlock();

        GeneratePolymorphicSerializationLogic
        (
            sb,
            ctx,
            info
        );

        sb.WriteLine("case null:");
        sb.WriteLine("    throw new System.NullReferenceException($\"Item in collection cannot be null.\");");
        sb.WriteLine("default:");
        sb.WriteLine
        (
            $"    throw new System.IO.InvalidDataException($\"Unknown type for {instanceName}: {{{instanceName}?.GetType().FullName}}\");"
        );
    }

    private static void GeneratePolymorphicSwitch(
        IndentedStringBuilder sb,
        PolymorphicInfo info,
        Action<IndentedStringBuilder, PolymorphicOption> caseEmitter)
    {
        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetGlobalTypeName(option.Type);
            sb.WriteLineFormat("case {0} typedInstance:", typeName);
            using (sb.BeginBlock())
            {
                caseEmitter(sb, option);
                sb.WriteLine("break;");
            }
        }
    }

    private static void GeneratePolymorphicSerializationLogic
    (
        IndentedStringBuilder sb,
        SerializationWriterEmitter.WriterCtx ctx,
        PolymorphicInfo info
    )
    {
        GeneratePolymorphicSwitch(sb, info, (s, option) =>
        {
            if (info.TypeIdPropertyIndex is null)
            {
                SerializationWriterEmitter.EmitWriteTypeId
                (
                    s,
                    ctx,
                    option,
                    info
                );
            }

            var typeName = TypeHelper.GetGlobalTypeName(option.Type);
            if (ctx.IsSpan)
            {
                s.WriteLineFormat($"{typeName}.Serialize(typedInstance, ref {ctx.Target});");
            }
            else // stream
            {
                s.WriteLineFormat($"{typeName}.Serialize(typedInstance, {ctx.Target});");
            }
        });
    }

    private static void GenerateIndividualTypeIdCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        string collectionExpression
    )
    {
        var itemMember = new MemberToGenerate
        (
            "item",
            member.ListTypeArgument!.Value.TypeName,
            false, // is value type
            member.ListTypeArgument.Value.IsUnmanagedType,
            member.ListTypeArgument.Value.IsStringType,
            member.ListTypeArgument.Value.HasGenerateSerializerAttribute,
            false,
            null,
            null,
            member.PolymorphicInfo,
            false,
            null,
            false,
            null,
            false,
            false,
            null,
            null,
            member.CustomSerializer
        );

        void EmitCollectionBody()
        {
            sb.WriteLineFormat("foreach(var item in {0})", collectionExpression);
            using var __ = sb.BeginBlock();
            GeneratePolymorphicItemSerialization
            (
                sb,
                itemMember,
                "item",
                ctx
            );
        }

        var enumerationGuard = collectionExpression == $"obj.{member.Name}"
            ? GeneratorUtilities.GetCollectionIterationGuard(member, collectionExpression)
            : null;
        if (enumerationGuard is not null)
        {
            sb.WriteLineFormat("if ({0})", enumerationGuard);
            using var _ = sb.BeginBlock();
            EmitCollectionBody();
            return;
        }

        EmitCollectionBody();
    }

    private static void GenerateSingleTypeIdPolymorphicCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        string collectionExpression
    )
    {
        if (member.PolymorphicInfo is not { } info)
        {
            return;
        }

        if (info.TypeIdPropertyIndex is null)
        {
            GenerateSingleTypeIdPolymorphicCollectionImplicit
            (
                sb,
                member,
                ctx,
                collectionInfo,
                info,
                collectionExpression
            );
        }
        else
        {
            GenerateSingleTypeIdPolymorphicCollectionWithProperty
            (
                sb,
                member,
                ctx,
                info,
                collectionExpression
            );
        }
    }

    private static void GenerateSingleTypeIdPolymorphicCollectionWithProperty
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        PolymorphicInfo info,
        string collectionExpression
    )
    {
        if (string.IsNullOrEmpty(info.TypeIdProperty))
        {
            throw new InvalidOperationException("Single-type-id collections with a type-id property require a property name.");
        }

        var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, collectionExpression);
        sb.WriteLineFormat("if ({0} > 0)", countExpression);
        using (sb.BeginBlock())
        {
            PolymorphicUtilities.EmitFirstCollectionItemAccess(sb, member, collectionExpression, "firstItem");
            var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
            sb.WriteLine("var discriminator = firstItem switch");
            sb.WriteLine("{");
            sb.Indent();
            foreach (var option in info.Options)
            {
                var key = PolymorphicUtilities.FormatTypedTypeIdValue(option.Key, info, typeIdType);
                sb.WriteLineFormat("{0} => {1},", PolymorphicUtilities.FormatOptionTypePattern(option), key);
            }

            sb.WriteLine
            (
                $"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{firstItem.GetType().Name}}\")"
            );
            sb.Unindent();
            sb.WriteLine("};");

            sb.WriteLine("switch (discriminator)");
            using (sb.BeginBlock())
            {
                foreach (var option in info.Options)
                {
                    var key = PolymorphicUtilities.FormatTypedTypeIdValue(option.Key, info, typeIdType);
                    var typeName = TypeHelper.GetGlobalTypeName(option.Type);
                    sb.WriteLineFormat("case {0}:", key);
                    using (sb.BeginBlock())
                    {
                        sb.WriteLineFormat("foreach(var item in {0})", collectionExpression);
                        using (sb.BeginBlock())
                        {
                            if (ctx.IsSpan)
                            {
                                sb.WriteLineFormat("{0}.Serialize(({0})item, ref {1});", typeName, ctx.Target);
                            }
                            else
                            {
                                sb.WriteLineFormat("{0}.Serialize(({0})item, {1});", typeName, ctx.Target);
                            }
                        }

                        sb.WriteLine("break;");
                    }
                }

                sb.WriteLine("default:");
                sb.WriteLineFormat
                (
                    "    throw new System.IO.InvalidDataException($\"Unknown type id for {0}: {{discriminator}}\");",
                    member.Name
                );
            }
        }
    }

    private static void GenerateSingleTypeIdPolymorphicCollectionImplicit
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        PolymorphicInfo info,
        string collectionExpression
    )
    {
        var listItemsVar = member.Name.ToCamelCase();
        if (listItemsVar == "items")
        {
            listItemsVar = "collectionItems"; // Avoid conflict with loop variable
        }

        sb.WriteLine($"var {listItemsVar} = {collectionExpression};");

        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, listItemsVar);

        sb.WriteLineFormat("if ({0} == 0)", countExpression);
        using (sb.BeginBlock())
        {
            EmitNullOrEmptyCollectionHeader(sb, ctx, collectionInfo, info);
        }

        sb.WriteLine("else");
        using (sb.BeginBlock())
        {
            SerializationWriterEmitter.EmitCountWrite(sb, ctx, countType, countExpression);

            EmitDiscriminatorFromFirstItem(sb, ctx, listItemsVar, member, info, "discriminator");

            sb.WriteLine("switch (discriminator)");
            using (sb.BeginBlock())
            {
                foreach (var option in info.Options)
                {
                    var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                    var typeName = TypeHelper.GetGlobalTypeName(option.Type);
                    sb.WriteLine($"case {key}:");
                    using (sb.BeginBlock())
                    {
                        sb.WriteLine($"for (int i = 0; i < {countExpression}; i++)");
                        using (sb.BeginBlock())
                        {
                            if (ctx.IsSpan)
                            {
                                sb.WriteLine($"{typeName}.Serialize(({typeName}){listItemsVar}[i], ref {ctx.Target});");
                            }
                            else
                            {
                                sb.WriteLine($"{typeName}.Serialize(({typeName}){listItemsVar}[i], {ctx.Target});");
                            }
                        }

                        sb.WriteLine("break;");
                    }
                }
            }
        }
    }

    private static void EmitDiscriminatorFromFirstItem(
        IndentedStringBuilder sb,
        SerializationWriterEmitter.WriterCtx ctx,
        string collectionName,
        MemberToGenerate member,
        PolymorphicInfo info,
        string discriminatorVarName)
    {
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        PolymorphicUtilities.EmitFirstCollectionItemAccess(sb, member, collectionName, "firstItem");
        sb.WriteLine($"var {discriminatorVarName} = firstItem switch");
        sb.WriteLine("{");
        sb.Indent();
        foreach (var option in info.Options)
        {
            var key = PolymorphicUtilities.FormatTypedTypeIdValue(option.Key, info, typeIdType);
            sb.WriteLineFormat("{0} => {1},", PolymorphicUtilities.FormatOptionTypePattern(option), key);
        }

        sb.WriteLine($"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{firstItem.GetType().Name}}\")");
        sb.Unindent();
        sb.WriteLine("};");

        SerializationWriterEmitter.EmitWrite(sb, ctx, typeIdType, discriminatorVarName);
    }

    private static void EmitNullOrEmptyCollectionHeader(
        IndentedStringBuilder sb,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        PolymorphicInfo polymorphicInfo)
    {
        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        SerializationWriterEmitter.EmitWrite(sb, ctx, countType, "0");

        if (polymorphicInfo.TypeIdPropertyIndex is null)
        {
            if (PolymorphicUtilities.TryGetDefaultOption(polymorphicInfo, out var defaultOption))
            {
                SerializationWriterEmitter.EmitWriteTypeId(sb, ctx, defaultOption, polymorphicInfo);
            }
        }
    }

    public static void GeneratePolymorphicEnumerableCollection(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        PolymorphicInfo info,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo,
        string collectionExpression)
    {
        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        var variablePrefix = member.Name.ToCamelCase();
        var countVariableName = $"{variablePrefix}Count";
        var enumeratorVariableName = $"{variablePrefix}Enumerator";

        BeginCountReservation(sb, ctx, countType, variablePrefix);

        if (member.CollectionTypeInfo?.CanBeNull == true)
        {
            sb.WriteLineFormat("if ({0} is null)", collectionExpression);
            using (sb.BeginBlock())
            {
                if (PolymorphicUtilities.TryGetDefaultOption(info, out var defaultOption))
                {
                    SerializationWriterEmitter.EmitWriteTypeId(sb, ctx, defaultOption, info);
                }

                if (ctx.IsSpan)
                {
                    EndCountReservation(sb, ctx, countType, "0", variablePrefix);
                }
            }

            sb.WriteLine("else");
            using (sb.BeginBlock())
            {
                EmitPolymorphicCollectionEnumerator(sb, member, countVariableName, enumeratorVariableName, collectionExpression);
                EmitPolymorphicCollectionEmptyCase(sb, ctx, info, countType, variablePrefix, enumeratorVariableName);
                EmitPolymorphicCollectionNonEmptyCase(sb, ctx, info, countType, variablePrefix, countVariableName, enumeratorVariableName);
            }

            return;
        }

        EmitPolymorphicCollectionEnumerator(sb, member, countVariableName, enumeratorVariableName, collectionExpression);
        EmitPolymorphicCollectionEmptyCase(sb, ctx, info, countType, variablePrefix, enumeratorVariableName);
        EmitPolymorphicCollectionNonEmptyCase(sb, ctx, info, countType, variablePrefix, countVariableName, enumeratorVariableName);
    }

    private static void BeginCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType)
    {
        BeginCountReservation(sb, ctx, countType, "count");
    }

    private static void BeginCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType, string variablePrefix)
    {
        var countPositionVariableName = $"{variablePrefix}CountPosition";
        var countSpanVariableName = $"{variablePrefix}CountSpan";

        if (!ctx.IsSpan)
        {
            sb.WriteLine("if (!stream.CanSeek)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("throw new NotSupportedException(\"Stream must be seekable to serialize this collection.\");");
            }

            sb.WriteLineFormat("var {0} = stream.Position;", countPositionVariableName);
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, "0", " // Placeholder for count");
        }
        else
        {
            sb.WriteLineFormat("var {0} = data;", countSpanVariableName);
            sb.WriteLine($"data = data.Slice(sizeof({countType}));");
        }
    }

    private static void EmitPolymorphicCollectionEnumerator(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string countVariableName,
        string enumeratorVariableName,
        string collectionExpression)
    {
        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName
            ?? throw new InvalidOperationException("Polymorphic collection members require element type information.");
        sb.WriteLineFormat("int {0} = 0;", countVariableName);
        sb.WriteLineFormat
        (
            "using var {0} = ((global::System.Collections.Generic.IEnumerable<{1}>){2}).GetEnumerator();",
            enumeratorVariableName,
            elementTypeName,
            collectionExpression
        );
    }

    private static void EmitPolymorphicCollectionEmptyCase(
        IndentedStringBuilder sb,
        SerializationWriterEmitter.WriterCtx ctx,
        PolymorphicInfo info,
        string countType,
        string variablePrefix,
        string enumeratorVariableName)
    {
        sb.WriteLineFormat("if (!{0}.MoveNext())", enumeratorVariableName);
        sb.WriteLine("{");
        sb.Indent();
        if (PolymorphicUtilities.TryGetDefaultOption(info, out var defaultOption))
        {
            SerializationWriterEmitter.EmitWriteTypeId(sb, ctx, defaultOption, info);
        }

        if (ctx.IsSpan)
        {
            EndCountReservation(sb, ctx, countType, "0", variablePrefix);
        }
        sb.Unindent();
        sb.WriteLine("}");
    }

    private static void EmitPolymorphicCollectionNonEmptyCase(
        IndentedStringBuilder sb,
        SerializationWriterEmitter.WriterCtx ctx,
        PolymorphicInfo info,
        string countType,
        string variablePrefix,
        string countVariableName,
        string enumeratorVariableName)
    {
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        sb.WriteLine("else");
        sb.WriteLine("{");
        sb.Indent();
        sb.WriteLineFormat("var discriminator = {0}.Current switch", enumeratorVariableName);
        sb.WriteLine("{");
        sb.Indent();
        foreach (var option in info.Options)
        {
            var key = PolymorphicUtilities.FormatTypedTypeIdValue(option.Key, info, typeIdType);
            sb.WriteLineFormat("{0} => {1},", PolymorphicUtilities.FormatOptionTypePattern(option), key);
        }

        sb.WriteLine
            ($"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{{enumeratorVariableName}.Current.GetType().Name}}\")");
        sb.Unindent();
        sb.WriteLine("};");

        SerializationWriterEmitter.EmitWrite(sb, ctx, typeIdType, "discriminator");

        sb.WriteLine("switch (discriminator)");
        using (sb.BeginBlock())
        {
            foreach (var option in info.Options)
            {
                var key = PolymorphicUtilities.FormatTypedTypeIdValue(option.Key, info, typeIdType);
                var typeName = TypeHelper.GetGlobalTypeName(option.Type);
                sb.WriteLine($"case {key}:");
                using (sb.BeginBlock())
                {
                    sb.WriteLine("do");
                    using (sb.BeginBlock())
                    {
                        if (ctx.IsSpan)
                        {
                            sb.WriteLine($"{typeName}.Serialize(({typeName}){enumeratorVariableName}.Current, ref data);");
                        }
                        else
                        {
                            sb.WriteLine($"{typeName}.Serialize(({typeName}){enumeratorVariableName}.Current, stream);");
                        }

                        sb.WriteLineFormat("{0}++;", countVariableName);
                    }

                    sb.WriteLineFormat("while ({0}.MoveNext());", enumeratorVariableName);

                    sb.WriteLine("break;");
                }
            }
        }
        EndCountReservation(sb, ctx, countType, countVariableName, variablePrefix);
        sb.Unindent();
        sb.WriteLine("}");
    }

    private static void EndCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType, string countExpr)
    {
        EndCountReservation(sb, ctx, countType, countExpr, "count");
    }

    private static void EndCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType, string countExpr, string variablePrefix)
    {
        var countPositionVariableName = $"{variablePrefix}CountPosition";
        var countSpanVariableName = $"{variablePrefix}CountSpan";

        if (!ctx.IsSpan)
        {
            var endPositionVariableName = $"{variablePrefix}EndPosition";
            sb.WriteLineFormat("var {0} = stream.Position;", endPositionVariableName);
            sb.WriteLineFormat("stream.Position = {0};", countPositionVariableName);
            SerializationWriterEmitter.EmitCountWrite(sb, ctx, countType, countExpr);
            sb.WriteLineFormat("stream.Position = {0};", endPositionVariableName);
        }
        else
        {
            var countCtx = ctx with { Target = countSpanVariableName };
            SerializationWriterEmitter.EmitCountWrite(sb, countCtx, countType, countExpr);
        }
    }
}
