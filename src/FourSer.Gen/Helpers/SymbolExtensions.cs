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

    public static bool IsGenerateSerializerAttribute(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is
        {
            Name: "GenerateSerializerAttribute",
            ContainingNamespace:
            {
                Name: "Contracts",
                ContainingNamespace: { Name: "FourSer", ContainingNamespace: { IsGlobalNamespace: true } }
            }
        };
    }

    public static bool IsDefaultSerializerAttribute(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is
        {
            Name: "DefaultSerializerAttribute",
            ContainingNamespace:
            {
                Name: "Contracts",
                ContainingNamespace: { Name: "FourSer", ContainingNamespace: { IsGlobalNamespace: true } }
            }
        };
    }

    public static bool IsSerializerAttribute(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is
        {
            Name: "SerializerAttribute",
            ContainingNamespace:
            {
                Name: "Contracts",
                ContainingNamespace: { Name: "FourSer", ContainingNamespace: { IsGlobalNamespace: true } }
            }
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
}
