using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace FourSer.Analyzers.Helpers;

public static class SymbolExtensions
{
    public static AttributeData? GetSerializeCollectionAttribute(this ISymbol symbol)
    {
        return symbol.GetAttributes().FirstOrDefault(static attribute =>
            attribute.AttributeClass is { } attributeClass
            && attributeClass.IsSerializeCollectionAttribute());
    }

    public static AttributeData? GetSerializePolymorphicAttribute(this ISymbol symbol)
    {
        return symbol.GetAttributes().FirstOrDefault(static attribute =>
            attribute.AttributeClass is { } attributeClass
            && attributeClass.IsSerializePolymorphicAttribute());
    }

    public static IEnumerable<AttributeData> GetPolymorphicOptionAttributes(this ISymbol symbol)
    {
        return symbol.GetAttributes().Where(static attribute =>
            attribute.AttributeClass is { } attributeClass
            && attributeClass.IsPolymorphicOptionAttribute());
    }

    public static bool HasIgnoreAttribute(this ISymbol symbol)
    {
        return symbol.GetAttributes().Any(static attribute =>
            attribute.AttributeClass is { } attributeClass
            && (attributeClass.IsIgnoredAttribute() || attributeClass.IsIgnoreDataMemberAttribute()));
    }

    public static ISymbol? GetNonIgnoredPropertyOrField(this INamedTypeSymbol typeSymbol, string memberName)
    {
        return typeSymbol.GetMembers(memberName)
            .FirstOrDefault(static member => member.IsNonIgnoredPropertyOrField());
    }

    public static bool IsNonIgnoredPropertyOrField(this ISymbol symbol)
    {
        return symbol is IPropertySymbol or IFieldSymbol && !symbol.HasIgnoreAttribute();
    }

    public static ITypeSymbol? GetPropertyOrFieldType(this ISymbol symbol)
    {
        return symbol switch
        {
            IPropertySymbol propertySymbol => propertySymbol.Type,
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            _ => null,
        };
    }

    public static bool IsSerializeCollectionAttribute(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is
        {
            Name: "SerializeCollectionAttribute",
            ContainingNamespace:
            {
                Name: "Contracts",
                ContainingNamespace: { Name: "FourSer", ContainingNamespace: { IsGlobalNamespace: true } }
            }
        };
    }

    public static bool IsSerializePolymorphicAttribute(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is
        {
            Name: "SerializePolymorphicAttribute",
            ContainingNamespace:
            {
                Name: "Contracts",
                ContainingNamespace: { Name: "FourSer", ContainingNamespace: { IsGlobalNamespace: true } }
            }
        };
    }

    public static bool IsPolymorphicOptionAttribute(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is
        {
            Name: "PolymorphicOptionAttribute",
            ContainingNamespace:
            {
                Name: "Contracts",
                ContainingNamespace: { Name: "FourSer", ContainingNamespace: { IsGlobalNamespace: true } }
            }
        };
    }

    public static bool IsIgnoredAttribute(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is
        {
            Name: "IgnoredAttribute",
            ContainingNamespace:
            {
                Name: "Contracts",
                ContainingNamespace: { Name: "FourSer", ContainingNamespace: { IsGlobalNamespace: true } }
            }
        };
    }

    public static bool IsIgnoreDataMemberAttribute(this INamedTypeSymbol typeSymbol)
    {
        // System.Runtime.Serialization.IgnoreDataMemberAttribute
        return typeSymbol is
        {
            Name: "IgnoreDataMemberAttribute",
            ContainingNamespace:
            {
                Name: "Serialization",
                ContainingNamespace:
                {
                    Name: "Runtime",
                    ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } }
                }
            }
        };
    }
}
