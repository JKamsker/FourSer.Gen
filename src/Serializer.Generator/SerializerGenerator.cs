using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Serializer.Generator;

[Generator]
public class SerializerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typeDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName
            (
                "Serializer.Contracts.GenerateSerializerAttribute",
                predicate: (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                transform: (context, _) => (TypeDeclarationSyntax)context.TargetNode
            );

        context.RegisterSourceOutput
        (
            context.CompilationProvider.Combine(typeDeclarations.Collect()),
            (spc, source) => Execute(source.Left, source.Right, spc)
        );
    }

    private static void Execute
        (Compilation compilation, ImmutableArray<TypeDeclarationSyntax> types, SourceProductionContext context)
    {
        if (types.IsDefaultOrEmpty)
        {
            return;
        }

        var allTypeSymbols = ExtractTypeSymbols(compilation, types);
        var typeGroups = TypeAnalyzer.GroupTypesByContainer(allTypeSymbols);

        foreach (var typeSymbol in allTypeSymbols)
        {
            // Skip nested types - they will be generated as part of their containing type
            if (typeSymbol.ContainingType != null)
            {
                continue;
            }

            var members = TypeAnalyzer.GetSerializableMembers(typeSymbol);
            var classToGenerate = new ClassToGenerate
            (
                typeSymbol.Name,
                typeSymbol.ContainingNamespace.ToDisplayString(),
                members,
                typeSymbol.IsValueType
            );

            // Get nested types that need to be generated
            var nestedTypes = typeGroups.ContainsKey(typeSymbol) ? typeGroups[typeSymbol] : new List<INamedTypeSymbol>();

            var source = SourceGenerator.GenerateSource(classToGenerate, typeSymbol, nestedTypes);
            context.AddSource($"{classToGenerate.Name}.g.cs", source);
        }
    }

    private static List<INamedTypeSymbol> ExtractTypeSymbols(Compilation compilation, ImmutableArray<TypeDeclarationSyntax> types)
    {
        var allTypeSymbols = new List<INamedTypeSymbol>();

        foreach (var typeDeclaration in types.Distinct())
        {
            var semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);

            if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
            {
                allTypeSymbols.Add(namedTypeSymbol);
            }
        }

        return allTypeSymbols;
    }
    
}