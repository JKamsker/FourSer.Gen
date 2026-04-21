using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

public static partial class PacketSizeGenerator
{
    private static bool ShouldDeferCollectionCountValidation(MemberToGenerate member)
    {
        return member.CollectionTypeInfo?.IsPureEnumerable == true;
    }

    private static string? GetDeferredCountValidationType(MemberToGenerate member, TypeToGenerate type)
    {
        if (!ShouldDeferCollectionCountValidation(member) || member.CollectionInfo is not { } collectionInfo || collectionInfo.Unlimited)
        {
            return null;
        }

        if (collectionInfo.CountSizeReferenceIndex is { } index)
        {
            return type.Members[index].TypeName;
        }

        return collectionInfo.CountSize is null or < 0 ? collectionInfo.CountType ?? TypeHelper.GetDefaultCountType() : null;
    }

    private static void GenerateDeferredCountValidationCollectionSizeCalculation(IndentedStringBuilder sb, MemberToGenerate member, TypeToGenerate type, string validationType)
    {
        var itemsVar = $"{member.Name.ToCamelCase()}SizedItems";
        sb.WriteLineFormat("var {0} = obj.{1} is null ? null : global::System.Linq.Enumerable.ToArray(obj.{1});", itemsVar, member.Name);
        EmitCheckedCountValidation(sb, validationType, $"{itemsVar}?.Length ?? 0");

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            AddPolymorphicSerialization(sb, member, member.CollectionInfo!.Value, itemsVar);
            return;
        }

        if (GetElementInfo(member) is not { } info)
        {
            return;
        }

        if (member.CustomSerializer is { } customSerializer)
        {
            var serializerField = global::FourSer.Gen.SerializerGenerator.SanitizeTypeName(customSerializer.SerializerTypeName);
            sb.WriteLineFormat("if ({0} is not null)", itemsVar);
            using var _ = sb.BeginBlock();
            sb.WriteLineFormat("foreach (var item in {0}) {{ size += FourSer.Generated.Internal.__FourSer_Generated_Serializers.{1}.GetPacketSize(item); }}", itemsVar, serializerField);
            return;
        }

        if (info.HasSerializer)
        {
            sb.WriteLineFormat("if ({0} is not null)", itemsVar);
            using var _ = sb.BeginBlock();
            sb.WriteLineFormat("foreach (var item in {0})", itemsVar);
            using var __ = sb.BeginBlock();
            if (!info.IsValueType)
            {
                EmitNullCollectionItemGuard(sb, "item");
            }
            sb.WriteLineFormat("size += {0}.GetPacketSize(item);", TypeHelper.GetGlobalTypeName(info.TypeName));
            return;
        }

        if (info.IsUnmanaged)
        {
            sb.WriteLineFormat("size += ({0}?.Length ?? 0) * sizeof({1});", itemsVar, info.TypeName);
            return;
        }

        if (info.IsString)
        {
            sb.WriteLineFormat("if ({0} is not null)", itemsVar);
            using var _ = sb.BeginBlock();
            sb.WriteLineFormat("foreach (var item in {0}) {{ size += StringEx.MeasureSize(item); }}", itemsVar);
        }
    }

    private static string? EmitSingleTypeIdPolymorphicSizeValidationPreamble(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        CollectionInfo collectionInfo,
        string collectionAccessExpression)
    {
        if (member.PolymorphicInfo is not { } info || collectionInfo.PolymorphicMode != PolymorphicMode.SingleTypeId)
        {
            return null;
        }

        var discriminatorType = info.EnumUnderlyingType ?? info.TypeIdType;
        var discriminatorVar = $"{member.Name.ToCamelCase()}ExpectedDiscriminator";
        var countExpression = collectionAccessExpression == $"obj.{member.Name}"
            ? GeneratorUtilities.GetCountExpressionForAccess(member, collectionAccessExpression, true)
            : $"({collectionAccessExpression}?.Length ?? 0)";

        sb.WriteLineFormat("{0} {1} = default;", discriminatorType, discriminatorVar);
        sb.WriteLineFormat("if ({0} > 0)", countExpression);
        using (sb.BeginBlock())
        {
            PolymorphicUtilities.EmitFirstCollectionItemAccess(sb, member, collectionAccessExpression, "firstSizedItem");
            sb.WriteLineFormat("{0} = firstSizedItem switch", discriminatorVar);
            sb.WriteLine("{");
            sb.Indent();
            foreach (var option in info.Options)
            {
                var typeName = TypeHelper.GetGlobalTypeName(option.Type);
                var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                sb.WriteLineFormat("{0} => ({1}){2},", typeName, discriminatorType, key);
            }

            sb.WriteLine("null => throw new System.NullReferenceException(\"Item in collection cannot be null.\"),");
            sb.WriteLine("_ => throw new System.IO.InvalidDataException($\"Unknown type for item: {firstSizedItem?.GetType().FullName}\")");
            sb.Unindent();
            sb.WriteLine("};");
        }

        return discriminatorVar;
    }
}
