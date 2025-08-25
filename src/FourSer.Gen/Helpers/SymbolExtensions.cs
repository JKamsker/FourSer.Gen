using Microsoft.CodeAnalysis;

namespace FourSer.Gen.Helpers;

public static class SymbolExtensions
{
    public static bool IsISerializable(this INamedTypeSymbol typeSymbol)
    {
        // better alternative to .ToDisplayString() == "FourSer.Contracts.ISerializable<T>"
        return typeSymbol is
        {
            Name: "ISerializable",
            Arity: 1, // Arity checks the number of generic parameters
            ContainingNamespace:
            { Name: "Contracts", ContainingNamespace: { Name: "FourSer", ContainingNamespace: { IsGlobalNamespace: true } } }
        };
    }
    
}
