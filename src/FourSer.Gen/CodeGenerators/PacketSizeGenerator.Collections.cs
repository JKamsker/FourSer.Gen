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
}
