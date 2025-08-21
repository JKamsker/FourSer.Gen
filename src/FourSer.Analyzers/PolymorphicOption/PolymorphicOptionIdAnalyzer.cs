using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace FourSer.Analyzers.PolymorphicOption
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PolymorphicOptionIdAnalyzer : DiagnosticAnalyzer
    {
        public const string DuplicateIdDiagnosticId = "FSG3000";
        public const string MixedIdTypesDiagnosticId = "FSG3001";

        private static readonly LocalizableString Title = "Invalid PolymorphicOption Id";
        private static readonly LocalizableString DuplicateIdMessageFormat = "Duplicate PolymorphicOption Id '{0}'";
        private static readonly LocalizableString MixedIdTypesMessageFormat = "PolymorphicOption Ids must have a consistent type";
        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor DuplicateIdRule = new DiagnosticDescriptor(DuplicateIdDiagnosticId, Title, DuplicateIdMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
        internal static readonly DiagnosticDescriptor MixedIdTypesRule = new DiagnosticDescriptor(MixedIdTypesDiagnosticId, Title, MixedIdTypesMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DuplicateIdRule, MixedIdTypesRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        }

        private void AnalyzeProperty(SymbolAnalysisContext context)
        {
            var symbol = (IPropertySymbol)context.Symbol;
            AnalyzeSymbol(context, symbol);
        }

        private void AnalyzeField(SymbolAnalysisContext context)
        {
            var symbol = (IFieldSymbol)context.Symbol;
            AnalyzeSymbol(context, symbol);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context, ISymbol symbol)
        {
            var attributes = symbol.GetAttributes().Where(ad => ad.AttributeClass?.Name == "PolymorphicOptionAttribute").ToList();

            if (attributes.Count < 2)
            {
                return;
            }

            var ids = new HashSet<object>();
            ITypeSymbol? firstIdType = null;

            foreach (var attribute in attributes)
            {
                if (attribute.ConstructorArguments.Length > 0)
                {
                    var idArg = attribute.ConstructorArguments[0];
                    if (idArg.IsNull || idArg.Value == null) continue;

                    if (firstIdType == null)
                    {
                        firstIdType = idArg.Type;
                    }
                    else if (idArg.Type != null && !SymbolEqualityComparer.Default.Equals(firstIdType, idArg.Type))
                    {
                        if (attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is AttributeSyntax attrSyntax &&
                            attrSyntax.ArgumentList != null &&
                            attrSyntax.ArgumentList.Arguments.Any())
                        {
                            var argumentSyntax = attrSyntax.ArgumentList.Arguments[0];
                            context.ReportDiagnostic(Diagnostic.Create(MixedIdTypesRule, argumentSyntax.GetLocation()));
                        }
                    }

                    if (!ids.Add(idArg.Value))
                    {
                        if (attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is AttributeSyntax attrSyntax &&
                            attrSyntax.ArgumentList != null &&
                            attrSyntax.ArgumentList.Arguments.Any())
                        {
                            var argumentSyntax = attrSyntax.ArgumentList.Arguments[0];
                            context.ReportDiagnostic(Diagnostic.Create(DuplicateIdRule, argumentSyntax.GetLocation(), idArg.Value));
                        }
                    }
                }
            }
        }
    }
}
