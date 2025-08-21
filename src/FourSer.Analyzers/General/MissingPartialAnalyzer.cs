using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.General
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MissingPartialAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0001";

        private static readonly LocalizableString Title = "Missing partial modifier";
        private static readonly LocalizableString MessageFormat = "Type '{0}' is marked with [GenerateSerializer] but is not declared as partial";
        private static readonly LocalizableString Description = "Types marked with [GenerateSerializer] must be declared as partial to allow the source generator to extend them.";
        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            var generateSerializerAttribute = context.Compilation.GetTypeByMetadataName("FourSer.Contracts.GenerateSerializerAttribute");
            if (generateSerializerAttribute == null)
            {
                return;
            }

            bool hasGenerateSerializerAttribute = namedTypeSymbol.GetAttributes()
                .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, generateSerializerAttribute));

            if (!hasGenerateSerializerAttribute)
            {
                return;
            }

            foreach (var syntaxReference in namedTypeSymbol.DeclaringSyntaxReferences)
            {
                var syntax = syntaxReference.GetSyntax(context.CancellationToken);
                if (syntax is TypeDeclarationSyntax typeDeclaration)
                {
                    if (!typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        var diagnostic = Diagnostic.Create(Rule, typeDeclaration.Identifier.GetLocation(), namedTypeSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
