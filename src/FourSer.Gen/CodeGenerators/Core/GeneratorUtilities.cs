using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;

namespace FourSer.Gen.CodeGenerators.Core;

public static class GeneratorUtilities
{
    public readonly record struct ResolvedSerializer(string TypeName, string FieldName);

    /// <summary>
    ///     Checks if a type has a default serializer override for a given target type.
    /// </summary>
    public static bool HasDefaultSerializerFor(TypeToGenerate type, string targetTypeName)
    {
        foreach (var defaultSerializer in type.DefaultSerializers)
        {
            if (defaultSerializer.TargetTypeName == targetTypeName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Unified method name mapping (consolidates 4 duplicate implementations)
    /// </summary>
    public static string GetMethodFriendlyTypeName(string typeName)
    {
        return typeName switch
        {
            "int" => "Int32",
            "uint" => "UInt32",
            "short" => "Int16",
            "ushort" => "UInt16",
            "long" => "Int64",
            "ulong" => "UInt64",
            "byte" => "Byte",
            "float" => "Single",
            "bool" => "Boolean",
            "double" => "Double",
            _ => TypeHelper.GetMethodFriendlyTypeName(typeName)
        };
    }

    /// <summary>
    ///     Builds the count expression for a member using the conventional <c>obj.Member</c> access pattern.
    /// </summary>
    /// <param name="member">The collection member being inspected.</param>
    /// <param name="memberName">The member name without the <c>obj.</c> prefix.</param>
    /// <param name="nullable">
    ///     Whether the expression should tolerate null collections by using null propagation and a zero fallback.
    /// </param>
    public static string GetCountExpression(MemberToGenerate member, string memberName, bool nullable = false)
    {
        return GetCountExpressionForAccess(member, $"obj.{memberName}", nullable);
    }

    /// <summary>
    ///     Builds the count expression for an arbitrary collection access expression.
    /// </summary>
    /// <param name="member">The collection member being inspected.</param>
    /// <param name="accessExpression">The expression used to access the collection instance.</param>
    /// <param name="nullable">
    ///     Whether the expression should tolerate null collections by using null propagation and a zero fallback.
    /// </param>
    public static string GetCountExpressionForAccess(MemberToGenerate member, string accessExpression, bool nullable = false)
    {
        var canUseNullPropagation = nullable && (member.CollectionTypeInfo?.CanBeNull ?? true);
        var countPropertyName = member.CollectionTypeInfo?.CountPropertyName;

        if (!string.IsNullOrEmpty(countPropertyName))
        {
            return canUseNullPropagation
                ? $"({accessExpression}?.{countPropertyName} ?? 0)"
                : $"{accessExpression}.{countPropertyName}";
        }

        return canUseNullPropagation
            ? $"({accessExpression}?.Count() ?? 0)"
            : $"{accessExpression}.Count()";
    }

    /// <summary>
    ///     Unified polymorphic check (consolidates 4 duplicate implementations)
    /// </summary>
    public static bool ShouldUsePolymorphicSerialization(MemberToGenerate member)
    {
        // Only use polymorphic logic if explicitly configured
        if (member.CollectionInfo?.PolymorphicMode != PolymorphicMode.None)
        {
            return true;
        }

        // Or if the collection has a TypeIdProperty, which implies polymorphic serialization
        if (!string.IsNullOrEmpty(member.CollectionInfo?.TypeIdProperty))
        {
            return true;
        }

        // Or if SerializePolymorphic attribute is present with actual options
        if (member.PolymorphicInfo?.Options.IsEmpty == false)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Checks if the member is an unmanaged type.
    /// </summary>
    public static bool IsUnmanagedType(MemberToGenerate member)
    {
        return member.IsUnmanagedType;
    }

    /// <summary>
    ///     Checks if the member is a string type.
    /// </summary>
    public static bool IsStringType(MemberToGenerate member)
    {
        return member.IsStringType;
    }

    /// <summary>
    ///     Checks if the member has the GenerateSerializer attribute.
    /// </summary>
    public static bool HasGenerateSerializerAttribute(MemberToGenerate member)
    {
        return member.HasGenerateSerializerAttribute;
    }

    public static ResolvedSerializer? ResolveSerializer(MemberToGenerate member, TypeToGenerate type)
    {
        if (member.IsList || member.IsCollection || member.IsMemoryOwner)
        {
            return null;
        }

        string? serializerTypeName = null;

        // 1. Direct override
        if (member.CustomSerializer is { } customSerializer)
        {
            serializerTypeName = customSerializer.SerializerTypeName;
        }
        // 2. Default override
        else
        {
            foreach (var defaultSerializer in type.DefaultSerializers)
            {
                if (defaultSerializer.TargetTypeName == member.TypeName)
                {
                    serializerTypeName = defaultSerializer.SerializerTypeName;
                    break;
                }
            }
        }

        if (serializerTypeName != null)
        {
            return new ResolvedSerializer(serializerTypeName, SerializerGenerator.SanitizeTypeName(serializerTypeName));
        }

        // 3. Fallback
        return null;
    }
}
