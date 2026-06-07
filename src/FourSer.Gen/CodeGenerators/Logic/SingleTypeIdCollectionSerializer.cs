using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.CodeGenerators.Helpers;
using FourSer.Gen.CodeGenerators.Planning;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Logic;

internal static class SingleTypeIdCollectionSerializer
{
    public static void Generate(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx writerContext,
        CollectionInfo collectionInfo,
        PolymorphicInfo info,
        string collectionExpression,
        CollectionPlan? collectionPlan)
    {
        if (info.TypeIdPropertyIndex is null)
        {
            GenerateImplicit(builder, member, writerContext, collectionInfo, info, collectionExpression, collectionPlan);
            return;
        }

        GenerateWithProperty(builder, member, writerContext, info, collectionExpression, collectionPlan);
    }

    private static void GenerateWithProperty(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx writerContext,
        PolymorphicInfo info,
        string collectionExpression,
        CollectionPlan? collectionPlan)
    {
        if (string.IsNullOrEmpty(info.TypeIdProperty))
        {
            throw new InvalidOperationException("Single-type-id collections with a type-id property require a property name.");
        }

        var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, collectionExpression);
        builder.WriteLine($"if ({countExpression} > 0)");
        using (builder.BeginBlock())
        {
            var discriminatorExpression = ResolveDiscriminator(builder, member, info, collectionExpression, collectionPlan);
            SingleTypeIdCollectionEmitter.EmitValidatedPayload(
                builder,
                member,
                writerContext,
                info,
                collectionExpression,
                discriminatorExpression);
        }
    }

    private static void GenerateImplicit(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        SerializationWriterEmitter.WriterCtx writerContext,
        CollectionInfo collectionInfo,
        PolymorphicInfo info,
        string collectionExpression,
        CollectionPlan? collectionPlan)
    {
        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, collectionExpression);

        builder.WriteLine($"if ({countExpression} == 0)");
        using (builder.BeginBlock())
        {
            EmitNullOrEmptyCollectionHeader(builder, writerContext, collectionInfo, info);
        }

        builder.WriteLine("else");
        using (builder.BeginBlock())
        {
            var discriminatorExpression = ResolveDiscriminator(builder, member, info, collectionExpression, collectionPlan);
            SerializationWriterEmitter.EmitCountWrite(builder, writerContext, countType, countExpression);
            SerializationWriterEmitter.EmitWrite(builder, writerContext, info.EnumUnderlyingType ?? info.TypeIdType, discriminatorExpression);
            SingleTypeIdCollectionEmitter.EmitValidatedPayload(
                builder,
                member,
                writerContext,
                info,
                collectionExpression,
                discriminatorExpression);
        }
    }

    private static string ResolveDiscriminator(
        IndentedStringBuilder builder,
        MemberToGenerate member,
        PolymorphicInfo info,
        string collectionExpression,
        CollectionPlan? collectionPlan)
    {
        var discriminatorExpression = collectionPlan?.CachedDiscriminatorLocalName;
        if (!string.IsNullOrEmpty(discriminatorExpression))
        {
            return discriminatorExpression!;
        }

        discriminatorExpression = "discriminator";
        SingleTypeIdCollectionEmitter.EmitValidatedDiscriminatorResolution(
            builder,
            member,
            info,
            collectionExpression,
            discriminatorExpression,
            declareVariable: true);
        return discriminatorExpression;
    }

    private static void EmitNullOrEmptyCollectionHeader(
        IndentedStringBuilder builder,
        SerializationWriterEmitter.WriterCtx writerContext,
        CollectionInfo collectionInfo,
        PolymorphicInfo polymorphicInfo)
    {
        var countType = collectionInfo.CountType ?? TypeHelper.GetDefaultCountType();
        SerializationWriterEmitter.EmitWrite(builder, writerContext, countType, "0");

        if (polymorphicInfo.TypeIdPropertyIndex is null
            && PolymorphicUtilities.TryGetDefaultOption(polymorphicInfo, out var defaultOption))
        {
            SerializationWriterEmitter.EmitWriteTypeId(builder, writerContext, defaultOption, polymorphicInfo);
        }
    }
}
