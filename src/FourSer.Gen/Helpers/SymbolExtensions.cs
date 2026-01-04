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

    public static bool IsGenericList(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is
        {
            Name: "List",
            Arity: 1,
            ContainingNamespace:
            {
                Name: "Generic",
                ContainingNamespace:
                {
                    Name: "Collections",
                    ContainingNamespace:
                    {
                        Name: "System",
                        ContainingNamespace: { IsGlobalNamespace: true }
                    }
                }
            }
        };
    }

    public static bool IsGenericIList(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { Name: "IList", Arity: 1, ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } } } } };
    }

    public static bool IsGenericICollection(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { Name: "ICollection", Arity: 1, ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } } } } };
    }

    public static bool IsGenericIEnumerable(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { Name: "IEnumerable", Arity: 1, ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } } } } };
    }

    public static bool IsObjectModelCollection(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { Name: "Collection", Arity: 1, ContainingNamespace: { Name: "ObjectModel", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } } } } };
    }

    public static bool IsObjectModelObservableCollection(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { Name: "ObservableCollection", Arity: 1, ContainingNamespace: { Name: "ObjectModel", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } } } } };
    }

    public static bool IsGenericHashSet(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { Name: "HashSet", Arity: 1, ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } } } } };
    }

    public static bool IsGenericSortedSet(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { Name: "SortedSet", Arity: 1, ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } } } } };
    }

    public static bool IsGenericQueue(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { Name: "Queue", Arity: 1, ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } } } } };
    }

    public static bool IsGenericStack(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { Name: "Stack", Arity: 1, ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } } } } };
    }

    public static bool IsGenericLinkedList(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { Name: "LinkedList", Arity: 1, ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } } } } };
    }

    public static bool IsConcurrentConcurrentBag(this INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is { Name: "ConcurrentBag", Arity: 1, ContainingNamespace: { Name: "Concurrent", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace: { IsGlobalNamespace: true } } } } };
    }
}
