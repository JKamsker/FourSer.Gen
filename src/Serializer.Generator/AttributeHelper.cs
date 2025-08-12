using Microsoft.CodeAnalysis;
using System.Linq;

namespace Serializer.Generator;

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

    public static bool HasGenerateSerializerAttribute(ITypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == "Serializer.Contracts.GenerateSerializerAttribute");
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
}