using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FourSer.Analyzers.Helpers;

public static class LocationExtensions
{
    public static Location? GetLocation(this ISymbol symbol, CancellationToken contextCancellationToken = default)
    {
        if (symbol is IPropertySymbol propertySymbol)
        {
            // Get the syntax node for the property
            var propertyDecl = propertySymbol.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax(contextCancellationToken))
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault();

            if (propertyDecl != null)
            {
                var typeLocation = propertyDecl.Type.GetLocation();
                return typeLocation;
            }
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            // Get the syntax node for the field
            var fieldDecl = fieldSymbol.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax(contextCancellationToken))
                .OfType<FieldDeclarationSyntax>()
                .FirstOrDefault();

            if (fieldDecl != null)
            {
                var typeLocation = fieldDecl.Declaration.Type.GetLocation();
                return typeLocation;
            }
        }
        
        return null;
    } 
}
