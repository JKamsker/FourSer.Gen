using System.Collections.Immutable;
using FourSer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SerializeCollectionOnCorrectMemberAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FSG1000";

        private static readonly LocalizableString Title = "Misplaced SerializeCollection attribute";
        private static readonly LocalizableString MessageFormat = "'[SerializeCollection]' can only be applied to properties of type 'IEnumerable<T>' or 'IMemoryOwner<T>'";
        private static readonly LocalizableString Description = "The '[SerializeCollection]' attribute is intended for collections and should only be applied to properties that implement 'IEnumerable<T>' or 'IMemoryOwner<T>'.";
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

            var attribute = symbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Equals(serializeCollectionAttribute, SymbolEqualityComparer.Default) ?? false);

            if (attribute == null)
            {
                return;
            }

            if (symbol.HasIgnoreAttribute())
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

            var iMemoryOwnerT = context.Compilation.GetTypeByMetadataName("System.Buffers.IMemoryOwner`1");
            bool isMemoryOwner = false;
            if (iMemoryOwnerT != null)
            {
                isMemoryOwner = typeSymbol is INamedTypeSymbol memoryOwnerType &&
                    memoryOwnerType.IsGenericType &&
                    memoryOwnerType.ConstructedFrom.Equals(iMemoryOwnerT, SymbolEqualityComparer.Default);
                if (!isMemoryOwner)
                {
                    isMemoryOwner = typeSymbol.AllInterfaces.Any(i =>
                        i.IsGenericType && i.ConstructedFrom.Equals(iMemoryOwnerT, SymbolEqualityComparer.Default));
                }
            }

            if (isIEnumerable || isMemoryOwner)
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
