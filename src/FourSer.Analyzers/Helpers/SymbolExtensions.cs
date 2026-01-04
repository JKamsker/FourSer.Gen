using Microsoft.CodeAnalysis;

namespace FourSer.Analyzers.Helpers;

public static class SymbolExtensions
{
    public static bool HasIgnoreAttribute(this ISymbol symbol)
    {
        return symbol.GetAttributes().Any(ad =>
            ad.AttributeClass is not null
            && (ad.AttributeClass.IsIgnoredAttribute() || ad.AttributeClass.IsIgnoreDataMemberAttribute()));
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
