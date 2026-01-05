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
        CollectionInfo collectionInfo)
    {
        if (collectionInfo.PolymorphicMode == PolymorphicMode.IndividualTypeIds)
        {
            GenerateIndividualTypeIdCollection(sb, member, ctx);
        }
        else if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
        {
            GenerateSingleTypeIdPolymorphicCollection
            (
                sb,
                member,
                ctx,
                collectionInfo
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
            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
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

            var typeName = TypeHelper.GetSimpleTypeName(option.Type);
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
        SerializationWriterEmitter.WriterCtx ctx
    )
    {
        sb.WriteLineFormat("foreach(var item in obj.{0})", member.Name);
        using var __ = sb.BeginBlock();
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

        GeneratePolymorphicItemSerialization
        (
            sb,
            itemMember,
            "item",
            ctx
        );
    }

    private static void GenerateSingleTypeIdPolymorphicCollection
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        CollectionInfo collectionInfo
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
                info
            );
        }
        else
        {
            GenerateSingleTypeIdPolymorphicCollectionWithProperty
            (
                sb,
                member,
                ctx,
                info
            );
        }
    }

    private static void GenerateSingleTypeIdPolymorphicCollectionWithProperty
    (
        IndentedStringBuilder sb,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx ctx,
        PolymorphicInfo info
    )
    {
        sb.WriteLineFormat($"if (obj.{member.Name}.Count > 0)");
        using (sb.BeginBlock())
        {
            sb.WriteLineFormat("switch (obj.{0}[0])", member.Name);
            using (sb.BeginBlock())
            {
                foreach (var option in info.Options)
                {
                    var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                    sb.WriteLineFormat("case {0}:", typeName);
                    using (sb.BeginBlock())
                    {
                        sb.WriteLineFormat("foreach(var item in obj.{0})", member.Name);
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
                    "    throw new System.IO.InvalidDataException($\"Unknown type for item in {0}: {{obj.{0}[0].GetType().Name}}\");",
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
        PolymorphicInfo info
    )
    {
        var listItemsVar = member.Name.ToCamelCase();
        if (listItemsVar == "items")
        {
            listItemsVar = "collectionItems"; // Avoid conflict with loop variable
        }

        sb.WriteLine($"var {listItemsVar} = obj.{member.Name};");

        var defaultOption = info.Options.FirstOrDefault();
        if (defaultOption.Equals(default(PolymorphicOption)))
        {
            return;
        }

        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;

        sb.WriteLine($"if ({listItemsVar} is null || {listItemsVar}.Count == 0)");
        using (sb.BeginBlock())
        {
            EmitNullOrEmptyCollectionHeader(sb, ctx, collectionInfo, info);
        }

        sb.WriteLine("else");
        using (sb.BeginBlock())
        {
            var countExpression = $"{listItemsVar}.Count";
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, countExpression);

            EmitDiscriminatorFromFirstItem(sb, ctx, listItemsVar, info, "discriminator");

            sb.WriteLine("switch (discriminator)");
            using (sb.BeginBlock())
            {
                foreach (var option in info.Options)
                {
                    var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                    var typeName = TypeHelper.GetSimpleTypeName(option.Type);
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
        PolymorphicInfo info,
        string discriminatorVarName)
    {
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        sb.WriteLine($"var firstItem = {collectionName}[0];");
        sb.WriteLine($"var {discriminatorVarName} = firstItem switch");
        sb.WriteLine("{");
        sb.Indent();
        foreach (var option in info.Options)
        {
            var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
            sb.WriteLineFormat("{0} => ({1}){2},", TypeHelper.GetSimpleTypeName(option.Type), typeIdType, key);
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
            var defaultOption = polymorphicInfo.Options.FirstOrDefault();
            if (!defaultOption.Equals(default(PolymorphicOption)))
            {
                SerializationWriterEmitter.EmitWriteTypeId(sb, ctx, defaultOption, polymorphicInfo);
            }
        }
    }
    public static void GeneratePolymorphicEnumerableCollection(IndentedStringBuilder sb, MemberToGenerate member, PolymorphicInfo info, SerializationWriterEmitter.WriterCtx ctx)
    {
        BeginCountReservation(sb, ctx, "int");
        EmitPolymorphicCollectionEnumerator(sb, member);
        EmitPolymorphicCollectionEmptyCase(sb, ctx, info);
        EmitPolymorphicCollectionNonEmptyCase(sb, ctx, info);
    }

    private static void BeginCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType)
    {
        if (!ctx.IsSpan)
        {
            sb.WriteLine("if (!stream.CanSeek)");
            using (sb.BeginBlock())
            {
                sb.WriteLine("throw new NotSupportedException(\"Stream must be seekable to serialize this collection.\");");
            }

            sb.WriteLine("var countPosition = stream.Position;");
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, "0", " // Placeholder for count");
        }
        else
        {
            sb.WriteLine("var countSpan = data;");
            sb.WriteLine($"data = data.Slice(sizeof({countType}));");
        }
    }

    private static void EmitPolymorphicCollectionEnumerator(IndentedStringBuilder sb, MemberToGenerate member)
    {
        sb.WriteLine("int count = 0;");
        sb.WriteLine($"using var enumerator = obj.{member.Name}.GetEnumerator();");
    }

    private static void EmitPolymorphicCollectionEmptyCase(
        IndentedStringBuilder sb,
        SerializationWriterEmitter.WriterCtx ctx,
        PolymorphicInfo info)
    {
        var defaultOption = info.Options.FirstOrDefault();
        sb.WriteLine("if (!enumerator.MoveNext())");
        sb.WriteLine("{");
        sb.Indent();
        SerializationWriterEmitter.EmitWriteTypeId(sb, ctx, defaultOption, info);
    if (ctx.IsSpan)
    {
        EndCountReservation(sb, ctx, "int", "0");
    }
        sb.Unindent();
        sb.WriteLine("}");
    }

    private static void EmitPolymorphicCollectionNonEmptyCase(
        IndentedStringBuilder sb,
        SerializationWriterEmitter.WriterCtx ctx,
        PolymorphicInfo info)
    {
        var typeIdType = info.EnumUnderlyingType ?? info.TypeIdType;
        sb.WriteLine("else");
        sb.WriteLine("{");
        sb.Indent();
        sb.WriteLine("var discriminator = enumerator.Current switch");
        sb.WriteLine("{");
        sb.Indent();
        foreach (var option in info.Options)
        {
            var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
            sb.WriteLineFormat($"{TypeHelper.GetSimpleTypeName(option.Type)} => ({typeIdType}){key},");
        }

        sb.WriteLine
            ($"_ => throw new System.IO.InvalidDataException($\"Unknown item type: {{enumerator.Current.GetType().Name}}\")");
        sb.Unindent();
        sb.WriteLine("};");

        SerializationWriterEmitter.EmitWrite(sb, ctx, typeIdType, "discriminator");

        sb.WriteLine("switch (discriminator)");
        using (sb.BeginBlock())
        {
            foreach (var option in info.Options)
            {
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                sb.WriteLine($"case {key}:");
                using (sb.BeginBlock())
                {
                    sb.WriteLine("do");
                    using (sb.BeginBlock())
                    {
                        if (ctx.IsSpan)
                        {
                            sb.WriteLine($"{typeName}.Serialize(({typeName})enumerator.Current, ref data);");
                        }
                        else
                        {
                            sb.WriteLine($"{typeName}.Serialize(({typeName})enumerator.Current, stream);");
                        }

                        sb.WriteLine("count++;");
                    }

                    sb.WriteLine("while (enumerator.MoveNext());");

                    sb.WriteLine("break;");
                }
            }
        }
        EndCountReservation(sb, ctx, "int", "count");
        sb.Unindent();
        sb.WriteLine("}");
    }

    private static void EndCountReservation(IndentedStringBuilder sb, SerializationWriterEmitter.WriterCtx ctx, string countType, string countExpr)
    {
        if (!ctx.IsSpan)
        {
            sb.WriteLine("var endPosition = stream.Position;");
            sb.WriteLine("stream.Position = countPosition;");
            SerializationWriterEmitter.EmitWrite(sb, ctx, countType, countExpr);
            sb.WriteLine("stream.Position = endPosition;");
        }
        else
        {
            var countCtx = ctx with { Target = "countSpan" };
            SerializationWriterEmitter.EmitWrite(sb, countCtx, countType, countExpr);
        }
    }
}
