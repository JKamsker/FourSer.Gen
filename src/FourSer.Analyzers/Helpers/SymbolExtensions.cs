using Microsoft.CodeAnalysis;

namespace FourSer.Analyzers.Helpers;

public static class SymbolExtensions
{
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
}
