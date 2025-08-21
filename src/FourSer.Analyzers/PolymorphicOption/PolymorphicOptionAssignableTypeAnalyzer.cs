using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.PolymorphicOption
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PolymorphicOptionAssignableTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FSG3002";

        private static readonly LocalizableString Title = "Invalid PolymorphicOption Type";
        private static readonly LocalizableString MessageFormat = "The type '{0}' is not assignable to the property type '{1}'";
        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
            AnalyzeSymbol(context, symbol, symbol.Type);
        }

        private void AnalyzeField(SymbolAnalysisContext context)
        {
            var symbol = (IFieldSymbol)context.Symbol;
            AnalyzeSymbol(context, symbol, symbol.Type);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context, ISymbol symbol, ITypeSymbol memberType)
        {
            var attributes = symbol.GetAttributes().Where(ad => ad.AttributeClass?.Name == "PolymorphicOptionAttribute").ToList();

            if (!attributes.Any())
            {
                return;
            }

            var baseType = GetBaseType(memberType);
            if (baseType == null) return;

            foreach (var attribute in attributes)
            {
                if (attribute.ConstructorArguments.Length > 1)
                {
                    var typeArg = attribute.ConstructorArguments[1];
                    if (typeArg.Value is ITypeSymbol optionType)
                    {
                        if (!context.Compilation.HasImplicitConversion(optionType, baseType))
                        {
                            if (attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is AttributeSyntax attrSyntax &&
                                attrSyntax.ArgumentList != null &&
                                attrSyntax.ArgumentList.Arguments.Count > 1)
                            {
                                var argumentSyntax = attrSyntax.ArgumentList.Arguments[1];
                                context.ReportDiagnostic(Diagnostic.Create(Rule, argumentSyntax.GetLocation(), optionType.Name, baseType.Name));
                            }
                        }
                    }
                }
            }
        }

        private ITypeSymbol? GetBaseType(ITypeSymbol memberType)
        {
            if (memberType is IArrayTypeSymbol arrayType)
            {
                return arrayType.ElementType;
            }

            // Check if the type itself is IEnumerable<T>
            if (memberType is INamedTypeSymbol namedType && 
                namedType.IsGenericType && 
                namedType.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return namedType.TypeArguments.FirstOrDefault();
            }

            // Check if the type implements IEnumerable<T>
            var ienumerable = memberType.AllInterfaces.FirstOrDefault(i =>
                i.IsGenericType && i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

            if (ienumerable != null)
            {
                return ienumerable.TypeArguments.FirstOrDefault();
            }

            return memberType;
        }
    }
}
