using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Logic;

internal static class PolymorphicCollectionValidationEmitter
{
    public static void Emit(IndentedStringBuilder sb, MemberToGenerate member, CollectionInfo collectionInfo)
    {
        if (member.PolymorphicInfo is not { } info || member.CollectionTypeInfo?.IsPureEnumerable == true)
        {
            return;
        }

        var collectionAccessExpression = $"obj.{member.Name}";
        var countExpression = GeneratorUtilities.GetCountExpressionForAccess(member, collectionAccessExpression);

        sb.WriteLineFormat("if ({0} > 0)", countExpression);
        using (sb.BeginBlock())
        {
            if (collectionInfo.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                EmitSingleTypeIdValidation(sb, member, info, collectionAccessExpression);
                return;
            }

            EmitPerItemValidation(sb, member, info, collectionAccessExpression, null);
        }
    }

    private static void EmitSingleTypeIdValidation(IndentedStringBuilder sb, MemberToGenerate member, PolymorphicInfo info, string collectionAccessExpression)
    {
        var discriminatorType = info.EnumUnderlyingType ?? info.TypeIdType;
        PolymorphicUtilities.EmitFirstCollectionItemAccess(sb, member, collectionAccessExpression, "firstItem");
        sb.WriteLineFormat("{0} discriminator = firstItem switch", discriminatorType);
        sb.WriteLine("{");
        sb.Indent();
        foreach (var option in info.Options)
        {
            var typeName = TypeHelper.GetGlobalTypeName(option.Type);
            var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
            sb.WriteLineFormat("{0} => ({1}){2},", typeName, discriminatorType, key);
        }

        sb.WriteLine("null => throw new System.NullReferenceException(\"Item in collection cannot be null.\"),");
        sb.WriteLine("_ => throw new System.IO.InvalidDataException($\"Unknown type for item: {firstItem?.GetType().FullName}\")");
        sb.Unindent();
        sb.WriteLine("};");

        EmitPerItemValidation(sb, member, info, collectionAccessExpression, "discriminator");
    }

    private static void EmitPerItemValidation(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        PolymorphicInfo info,
        string collectionAccessExpression,
        string? discriminatorVariableName)
    {
        var discriminatorType = info.EnumUnderlyingType ?? info.TypeIdType;
        sb.WriteLineFormat("foreach (var item in {0})", collectionAccessExpression);
        using (sb.BeginBlock())
        {
            sb.WriteLine("switch (item)");
            using (sb.BeginBlock())
            {
                foreach (var option in info.Options)
                {
                    var typeName = TypeHelper.GetGlobalTypeName(option.Type);
                    sb.WriteLineFormat("case {0}:", typeName);
                    using (sb.BeginBlock())
                    {
                        if (discriminatorVariableName is not null)
                        {
                            var key = PolymorphicUtilities.FormatTypeIdKey(option.Key, info);
                            sb.WriteLineFormat("if (!global::System.Collections.Generic.EqualityComparer<{0}>.Default.Equals({1}, ({0}){2}))", discriminatorType, discriminatorVariableName, key);
                            using (sb.BeginBlock())
                            {
                                sb.WriteLineFormat("throw new System.IO.InvalidDataException(\"All items in collection {0} must have the same runtime type for single-type-id polymorphism.\");", member.Name);
                            }
                        }

                        sb.WriteLine("break;");
                    }
                }

                sb.WriteLine("case null:");
                sb.WriteLine("    throw new System.NullReferenceException(\"Item in collection cannot be null.\");");
                sb.WriteLine("default:");
                sb.WriteLine("    throw new System.IO.InvalidDataException($\"Unknown type for item: {item?.GetType().FullName}\");");
            }
        }
    }
}
