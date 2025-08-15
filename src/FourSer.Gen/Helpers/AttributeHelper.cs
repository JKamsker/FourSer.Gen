using System.Linq;
using Microsoft.CodeAnalysis;

namespace FourSer.Gen.Helpers;

/// <summary>
/// Helper methods for working with serialization attributes
/// </summary>
public static class AttributeHelper
{
    public static AttributeData? GetCollectionAttribute(ISymbol member)
    {
        return member.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "SerializeCollectionAttribute");
    }

    public static AttributeData? GetPolymorphicAttribute(ISymbol member)
    {
        return member.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "SerializePolymorphicAttribute");
    }

    public static System.Collections.Generic.List<AttributeData> GetPolymorphicOptions(ISymbol member)
    {
        return member.GetAttributes()
            .Where(a => a.AttributeClass?.Name == "PolymorphicOptionAttribute")
            .ToList();
    }

    public static (object Key, ITypeSymbol Type) GetPolymorphicOption(AttributeData optionAttribute)
    {
        var key = optionAttribute.ConstructorArguments[0].Value!;
        var type = (ITypeSymbol)optionAttribute.ConstructorArguments[1].Value!;
        return (key, type);
    }

    public static bool HasGenerateSerializerAttribute(ITypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute");
    }

    public static string? GetCountSizeReference(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "CountSizeReference")
            .Value.Value?.ToString();
    }

    public static ITypeSymbol? GetCountType(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "CountType")
            .Value.Value as ITypeSymbol;
    }

    public static int? GetCountSize(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "CountSize")
            .Value.Value as int?;
    }

    public static string? GetTypeIdProperty(AttributeData? polymorphicAttribute)
    {
        return polymorphicAttribute?.ConstructorArguments.FirstOrDefault().Value?.ToString();
    }

    public static ITypeSymbol? GetTypeIdType(AttributeData? polymorphicAttribute)
    {
        return polymorphicAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "TypeIdType")
            .Value.Value as ITypeSymbol;
    }

    public static int GetPolymorphicMode(AttributeData? collectionAttribute)
    {
        var polymorphicModeArg = collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "PolymorphicMode");

        // The enum value is returned as an int. 0=None, 1=SingleTypeId, 2=IndividualTypeIds
        return polymorphicModeArg?.Value.Value as int? ?? 0;
    }

    public static ITypeSymbol? GetCollectionTypeIdType(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "TypeIdType")
            .Value.Value as ITypeSymbol;
    }

    public static string? GetCollectionTypeIdProperty(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "TypeIdProperty")
            .Value.Value?.ToString();
    }

    public static bool GetUnlimited(AttributeData? collectionAttribute)
    {
        return collectionAttribute?.NamedArguments
            .FirstOrDefault(arg => arg.Key == "Unlimited")
            .Value.Value as bool? ?? false;
    }
}