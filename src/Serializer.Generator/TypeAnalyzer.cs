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
        var members = new List<ISymbol>();
        var currentType = typeSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var typeMembers = currentType.GetMembers()
                .Where(m =>
                    !m.IsImplicitlyDeclared &&
                    ((m.Kind == SymbolKind.Property && ((IPropertySymbol)m).SetMethod != null) ||
                    (m.Kind == SymbolKind.Field && m.DeclaredAccessibility == Accessibility.Public
                        && !((IFieldSymbol)m).IsReadOnly)))
                .OrderBy(m => m.Locations.First().SourceSpan.Start)
                .ToList();
            members.InsertRange(0, typeMembers);
            currentType = currentType.BaseType;
        }
        return members;
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

    public static void ValidateSerializableType(SourceProductionContext context, INamedTypeSymbol typeSymbol)
    {
        foreach (var member in GetSerializableMembers(typeSymbol))
        {
            var collectionAttribute = AttributeHelper.GetCollectionAttribute(member);
            if (collectionAttribute != null)
            {
                var polymorphicMode = (PolymorphicMode)AttributeHelper.GetPolymorphicMode(collectionAttribute);
                var typeIdProperty = AttributeHelper.GetCollectionTypeIdProperty(collectionAttribute);

                if (polymorphicMode == PolymorphicMode.SingleTypeId)
                {
                    if (string.IsNullOrEmpty(typeIdProperty))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor("SER001", "Missing TypeIdProperty", "TypeIdProperty must be specified when PolymorphicMode is SingleTypeId.", "Serialization", DiagnosticSeverity.Error, true),
                            member.Locations.FirstOrDefault()));
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(typeIdProperty))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor("SER002", "Invalid TypeIdProperty", "TypeIdProperty must not be specified when PolymorphicMode is not SingleTypeId.", "Serialization", DiagnosticSeverity.Error, true),
                            member.Locations.FirstOrDefault()));
                    }
                }
            }
        }
    }
}