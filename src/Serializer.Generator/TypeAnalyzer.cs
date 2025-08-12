using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Serializer.Generator;

/// <summary>
/// Analyzes types and their members for serialization generation
/// </summary>
public static class TypeAnalyzer
{
    public static ITypeSymbol GetMemberType(ISymbol member)
    {
        return member switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => throw new ArgumentException($"Unsupported member type: {member.GetType()}")
        };
    }

    public static string GetTypeReference(ITypeSymbol typeSymbol, INamedTypeSymbol containingType)
    {
        // If the type is not nested, use the simple name
        if (typeSymbol.ContainingType == null)
        {
            return typeSymbol.Name;
        }

        // If the type is nested within the current class being generated
        if (SymbolEqualityComparer.Default.Equals(typeSymbol.ContainingType, containingType))
        {
            return $"{containingType.Name}.{typeSymbol.Name}";
        }

        // If the type is nested within a different class, use fully qualified name
        // Handle multiple levels of nesting by using ToDisplayString
        return typeSymbol.ToDisplayString();
    }

    public static List<ISymbol> GetSerializableMembers(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetMembers()
            .Where(m =>
                (m.Kind == SymbolKind.Property && ((IPropertySymbol)m).SetMethod != null) ||
                (m.Kind == SymbolKind.Field && m.DeclaredAccessibility == Accessibility.Public
                    && !((IFieldSymbol)m).IsReadOnly))
            .OrderBy(m => m.Locations.First().SourceSpan.Start)
            .ToList();
    }

    public static Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> GroupTypesByContainer(
        IEnumerable<INamedTypeSymbol> typeSymbols)
    {
        var typeGroups = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

        foreach (var typeSymbol in typeSymbols)
        {
            // If this is a nested type, group it with its containing type
            if (typeSymbol.ContainingType != null)
            {
                var containingType = typeSymbol.ContainingType;
                if (!typeGroups.ContainsKey(containingType))
                {
                    typeGroups[containingType] = new List<INamedTypeSymbol>();
                }
                typeGroups[containingType].Add(typeSymbol);
            }
        }

        return typeGroups;
    }
}