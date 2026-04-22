using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators;

internal static partial class PacketSizeGenerator
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

        if (GeneratorUtilities.ShouldUsePolymorphicSerialization(member))
        {
            AddPolymorphicSerialization(sb, member, member.CollectionInfo!.Value, itemsVar, validationType);
            return;
        }

        if (GetElementInfo(member) is not { } info)
        {
            return;
        }

        GenerateStandardCollectionSizeCalculation(sb, member, info, itemsVar, validationType);
    }
}
