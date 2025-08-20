using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using FourSer.Analyzers.Helpers;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SerializeCollectionCountReferenceAnalyzer : DiagnosticAnalyzer
    {
        public const string NotFoundDiagnosticId = "FSG1004";
        public const string WrongTypeDiagnosticId = "FSG1005";
        public const string DeclaredAfterPropertyDiagnosticId = "FSG1006";

        private static readonly LocalizableString Title = "Invalid CountSizeReference";
        private static readonly LocalizableString NotFoundMessageFormat = "The property '{0}' specified in 'CountSizeReference' was not found";
        private static readonly LocalizableString WrongTypeMessageFormat = "The property '{0}' must be of an integral or enum type";
        private static readonly LocalizableString DeclaredAfterMessageFormat = "The property '{0}' must be declared before the collection property";
        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor NotFoundRule = new DiagnosticDescriptor(NotFoundDiagnosticId, Title, NotFoundMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
        internal static readonly DiagnosticDescriptor WrongTypeRule = new DiagnosticDescriptor(WrongTypeDiagnosticId, Title, WrongTypeMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
        internal static readonly DiagnosticDescriptor DeclaredAfterRule = new DiagnosticDescriptor(DeclaredAfterPropertyDiagnosticId, Title, DeclaredAfterMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(NotFoundRule, WrongTypeRule, DeclaredAfterRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeAttribute, SymbolKind.Property);
            context.RegisterSymbolAction(AnalyzeAttribute, SymbolKind.Field);
        }

        private void AnalyzeAttribute(SymbolAnalysisContext context)
        {
            var symbol = context.Symbol;
            var attribute = symbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "SerializeCollectionAttribute");

            if (attribute == null || attribute.ApplicationSyntaxReference == null)
            {
                return;
            }

            var countSizeReferenceArg = attribute.NamedArguments.FirstOrDefault(na => na.Key == "CountSizeReference");
            if (countSizeReferenceArg.Key == null)
            {
                return;
            }

            var referenceName = countSizeReferenceArg.Value.Value as string;
            if (string.IsNullOrEmpty(referenceName))
            {
                return;
            }

            var containingType = symbol.ContainingType;
            var referencedSymbol = containingType.GetMembers(referenceName).FirstOrDefault();

            var attributeSyntax = (AttributeSyntax)attribute.ApplicationSyntaxReference.GetSyntax(context.CancellationToken);
            var argumentSyntax = attributeSyntax.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "CountSizeReference");

            if (argumentSyntax == null)
            {
                return;
            }

            if (referencedSymbol == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(NotFoundRule, argumentSyntax.GetLocation(), referenceName));
                return;
            }

            if (referencedSymbol is IPropertySymbol propertySymbol)
            {
                if (!IsValidType(propertySymbol.Type))
                {
                    context.ReportDiagnostic(Diagnostic.Create(WrongTypeRule, argumentSyntax.GetLocation(), referenceName));
                }
            }
            else if (referencedSymbol is IFieldSymbol fieldSymbol)
            {
                if (!IsValidType(fieldSymbol.Type))
                {
                    context.ReportDiagnostic(Diagnostic.Create(WrongTypeRule, argumentSyntax.GetLocation(), referenceName));
                }
            }

            var symbolDeclaration = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            var referencedSymbolDeclaration = referencedSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

            if (symbolDeclaration != null && referencedSymbolDeclaration != null && symbolDeclaration.Span.Start < referencedSymbolDeclaration.Span.Start)
            {
                context.ReportDiagnostic(Diagnostic.Create(DeclaredAfterRule, argumentSyntax.GetLocation(), referenceName));
            }
        }

        private bool IsValidType(ITypeSymbol type)
        {
            return type.IsIntegralType() || type.TypeKind == TypeKind.Enum;
        }
    }
}
