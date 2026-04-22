using FourSer.Gen.CodeGenerators.Core;
using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Planning;

internal static class PlanExpressionFactory
{
    public static string GetMemberAccess(string instanceName, MemberToGenerate member)
    {
        return $"{instanceName}.{member.Name}";
    }

    public static string GetMemberLocalName(MemberToGenerate member)
    {
        return member.Name.ToCamelCase();
    }

    public static string GetCountLocalName(MemberToGenerate member)
    {
        return $"{member.Name.ToCamelCase()}Count";
    }

    public static string GetByteCountLocalName(MemberToGenerate member)
    {
        return $"{member.Name.ToCamelCase()}ByteCount";
    }

    public static string GetTypeIdLocalName(MemberToGenerate member)
    {
        return $"{member.Name.ToCamelCase()}TypeId";
    }

    public static string GetDiscriminatorLocalName(MemberToGenerate member)
    {
        return $"{member.Name.ToCamelCase()}Discriminator";
    }

    public static string GetCountExpression(MemberToGenerate member, string accessExpression, bool nullable = false)
    {
        return member.IsMemoryOwner
            ? $"({accessExpression}?.Memory.Length ?? 0)"
            : GeneratorUtilities.GetCountExpressionForAccess(member, accessExpression, nullable);
    }

    public static string GetCountType(MemberToGenerate member)
    {
        return member.CollectionInfo?.CountType ?? TypeHelper.GetDefaultCountType();
    }

    public static string GetScalarReadMethodName(string typeName)
    {
        return $"Read{GeneratorUtilities.GetMethodFriendlyTypeName(typeName)}";
    }

    public static string GetScalarWriteMethodName(string typeName)
    {
        return $"Write{GeneratorUtilities.GetMethodFriendlyTypeName(typeName)}";
    }

    public static string GetSimpleTypeName(string typeName)
    {
        return TypeHelper.GetSimpleTypeName(typeName);
    }

    public static string QuoteString(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"") + "\"";
    }

    public static string QuoteInterpolatedString(string value)
    {
        return "$\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"") + "\"";
    }
}
