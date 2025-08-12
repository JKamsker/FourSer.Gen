using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Serializer.Generator;

[Generator]
public class SerializerGenerator : IIncrementalGenerator
{
    private static readonly string[] s_helperFileNames =
    {
        "BinaryWriterExtensions.cs",
        "RoSpanReaderExtensions.cs",
        "SpanReaderExtensions.cs",
        "SpanWriterExtensions.cs",
        "StringHelper.cs"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(AddHelpers);
        
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

    private void AddHelpers(IncrementalGeneratorPostInitializationContext context)
    {
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var file in s_helperFileNames)
        {
            var resourceName = $"Serializer.Generator.Helpers.{file}";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();
            context.AddSource(file, source);
        }
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

            TypeAnalyzer.ValidateSerializableType(context, typeSymbol);

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