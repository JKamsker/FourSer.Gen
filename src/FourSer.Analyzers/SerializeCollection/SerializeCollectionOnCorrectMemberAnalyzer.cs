using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SerializeCollectionOnCorrectMemberAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FSG1000";

        private static readonly LocalizableString Title = "Misplaced SerializeCollection attribute";
        private static readonly LocalizableString MessageFormat = "'[SerializeCollection]' can only be applied to properties of type 'IEnumerable<T>'";
        private static readonly LocalizableString Description = "The '[SerializeCollection]' attribute is intended for collections and should only be applied to properties that implement 'IEnumerable<T>'.";
        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        }

        private void AnalyzeField(SymbolAnalysisContext context)
        {
            var fieldSymbol = (IFieldSymbol)context.Symbol;
            AnalyzeSymbol(context, fieldSymbol, fieldSymbol.Type);
        }

        private void AnalyzeProperty(SymbolAnalysisContext context)
        {
            var propertySymbol = (IPropertySymbol)context.Symbol;
            AnalyzeSymbol(context, propertySymbol, propertySymbol.Type);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context, ISymbol symbol, ITypeSymbol typeSymbol)
        {
            var serializeCollectionAttribute = context.Compilation.GetTypeByMetadataName("FourSer.Contracts.SerializeCollectionAttribute");
            if (serializeCollectionAttribute == null)
            {
                return;
            }

            var attribute = symbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass.Equals(serializeCollectionAttribute, SymbolEqualityComparer.Default));

            if (attribute == null)
            {
                return;
            }

            var ienumerableT = context.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
            if (ienumerableT == null)
            {
                return;
            }

            bool isIEnumerable = typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType && namedType.ConstructedFrom.Equals(ienumerableT, SymbolEqualityComparer.Default);
            if (!isIEnumerable)
            {
                isIEnumerable = typeSymbol.AllInterfaces.Any(i => i.IsGenericType && i.ConstructedFrom.Equals(ienumerableT, SymbolEqualityComparer.Default));
            }

            if (isIEnumerable)
            {
                return;
            }

            var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken);
            if (attributeSyntax != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, attributeSyntax.GetLocation()));
            }
        }
    }
}
