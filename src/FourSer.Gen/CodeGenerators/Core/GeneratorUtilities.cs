using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using Microsoft.CodeAnalysis;

namespace FourSer.Gen.CodeGenerators.Core;

public static class GeneratorUtilities
{
    public readonly record struct ResolvedSerializer(string TypeName, string FieldName);

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
    ///     Unified count expression generation (consolidates 4 duplicate implementations)
    /// </summary>
    public static string GetCountExpression(MemberToGenerate member, string memberName, bool nullable = false)
    {
        // Arrays use .Length property
        if (member.CollectionTypeInfo?.IsArray == true)
        {
            return nullable
                ? $"(obj.{memberName}?.Length ?? 0)"
                : $"obj.{memberName}.Length";
        }

        // IEnumerable and interface types that need Count() method
        if (member.CollectionTypeInfo?.CollectionType is INamedTypeSymbol collectionType &&
            (collectionType.IsGenericIEnumerable() || collectionType.IsGenericICollection() || collectionType.IsGenericIList()))
        {
            return nullable
                ? $"(obj.{memberName}?.Count() ?? 0)"
                : $"obj.{memberName}.Count()";
        }

        // Most concrete collection types use .Count property
        // List<T>, HashSet<T>, Queue<T>, Stack<T>, ConcurrentBag<T>, LinkedList<T>, Collection<T>, etc.
        return nullable 
            ? $"(obj.{memberName}?.Count ?? 0)"
            : $"obj.{memberName}.Count";
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
