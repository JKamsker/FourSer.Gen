using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace FourSer.Gen.Helpers;

/// <summary>
/// Helper methods for working with serialization attributes
/// </summary>
public static class AttributeHelper
{
    public static AttributeData? GetCollectionAttribute(ISymbol member)
    {
        foreach (var attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == "SerializeCollectionAttribute")
            {
                return attribute;
            }
        }
        return null;
    }

    public static AttributeData? GetPolymorphicAttribute(ISymbol member)
    {
        foreach (var attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == "SerializePolymorphicAttribute")
            {
                return attribute;
            }
        }
        return null;
    }

    public static System.Collections.Generic.List<AttributeData> GetPolymorphicOptions(ISymbol member)
    {
        var options = new List<AttributeData>();
        foreach (var attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == "PolymorphicOptionAttribute")
            {
                options.Add(attribute);
            }
        }
        return options;
    }

    public static (object Key, ITypeSymbol Type) GetPolymorphicOption(AttributeData optionAttribute)
    {
        var key = optionAttribute.ConstructorArguments[0].Value!;
        var type = (ITypeSymbol)optionAttribute.ConstructorArguments[1].Value!;
        return (key, type);
    }

    public static bool HasGenerateSerializerAttribute(ITypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute")
            {
                return true;
            }
        }
        return false;
    }

    private static TypedConstant? GetNamedArgument(AttributeData attribute, string name)
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key == name)
            {
                return arg.Value;
            }
        }
        return null;
    }

    public static string? GetCountSizeReference(AttributeData? collectionAttribute)
    {
        if (collectionAttribute is null) return null;
        return GetNamedArgument(collectionAttribute, "CountSizeReference")?.Value?.ToString();
    }

    public static ITypeSymbol? GetCountType(AttributeData? collectionAttribute)
    {
        if (collectionAttribute is null) return null;
        return GetNamedArgument(collectionAttribute, "CountType")?.Value as ITypeSymbol;
    }

    public static int? GetCountSize(AttributeData? collectionAttribute)
    {
        if (collectionAttribute is null) return null;
        return GetNamedArgument(collectionAttribute, "CountSize")?.Value as int?;
    }

    public static string? GetTypeIdProperty(AttributeData? polymorphicAttribute)
    {
        if (polymorphicAttribute is null || polymorphicAttribute.ConstructorArguments.Length == 0)
        {
            return null;
        }
        return polymorphicAttribute.ConstructorArguments[0].Value?.ToString();
    }

    public static ITypeSymbol? GetTypeIdType(AttributeData? polymorphicAttribute)
    {
        if (polymorphicAttribute is null) return null;
        return GetNamedArgument(polymorphicAttribute, "TypeIdType")?.Value as ITypeSymbol;
    }

    public static int GetPolymorphicMode(AttributeData? collectionAttribute)
    {
        if (collectionAttribute is null) return 0;
        // The enum value is returned as an int. 0=None, 1=SingleTypeId, 2=IndividualTypeIds
        return GetNamedArgument(collectionAttribute, "PolymorphicMode")?.Value as int? ?? 0;
    }

    public static ITypeSymbol? GetCollectionTypeIdType(AttributeData? collectionAttribute)
    {
        if (collectionAttribute is null) return null;
        return GetNamedArgument(collectionAttribute, "TypeIdType")?.Value as ITypeSymbol;
    }

    public static string? GetCollectionTypeIdProperty(AttributeData? collectionAttribute)
    {
        if (collectionAttribute is null) return null;
        return GetNamedArgument(collectionAttribute, "TypeIdProperty")?.Value?.ToString();
    }

    public static bool GetUnlimited(AttributeData? collectionAttribute)
    {
        if (collectionAttribute is null) return false;
        return GetNamedArgument(collectionAttribute, "Unlimited")?.Value as bool? ?? false;
    }
}