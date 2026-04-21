using System.Collections.Immutable;
using FourSer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.PolymorphicOption
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PolymorphicOptionDefaultAnalyzer : DiagnosticAnalyzer
    {
        public const string MultipleDefaultsDiagnosticId = "FSG3003";

        private static readonly LocalizableString Title = "Invalid PolymorphicOption default";
        private static readonly LocalizableString MessageFormat = "Only one PolymorphicOption can be marked as the default option";
        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor MultipleDefaultsRule = new DiagnosticDescriptor(
            MultipleDefaultsDiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MultipleDefaultsRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
            context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        }

        private static void AnalyzeProperty(SymbolAnalysisContext context)
        {
            AnalyzeSymbol(context, (IPropertySymbol)context.Symbol);
        }

        private static void AnalyzeField(SymbolAnalysisContext context)
        {
            AnalyzeSymbol(context, (IFieldSymbol)context.Symbol);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, ISymbol symbol)
        {
            var attributes = symbol.GetPolymorphicOptionAttributes().ToList();

            if (attributes.Count < 2)
            {
                return;
            }

            var defaultAttributes = attributes.Where(IsDefaultOption).ToList();
            if (defaultAttributes.Count <= 1)
            {
                return;
            }

            foreach (var attribute in defaultAttributes.Skip(1))
            {
                var location = GetIsDefaultLocation(attribute, context.CancellationToken)
                    ?? attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();

                if (location is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(MultipleDefaultsRule, location));
                }
            }
        }

        private static bool IsDefaultOption(AttributeData attribute)
        {
            if (attribute.ConstructorArguments.Length > 2
                && attribute.ConstructorArguments[2].Value is bool constructorValue)
            {
                return constructorValue;
            }

            return attribute.NamedArguments
                .FirstOrDefault(arg => arg.Key == "IsDefault")
                .Value.Value as bool? ?? false;
        }

        private static Location? GetIsDefaultLocation(AttributeData attribute, CancellationToken cancellationToken)
        {
            if (attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is not AttributeSyntax attributeSyntax
                || attributeSyntax.ArgumentList is null)
            {
                return null;
            }

            foreach (var argument in attributeSyntax.ArgumentList.Arguments)
            {
                var name = argument.NameEquals?.Name.Identifier.ValueText ?? argument.NameColon?.Name.Identifier.ValueText;
                if (string.Equals(name, "isDefault", StringComparison.OrdinalIgnoreCase))
                {
                    return argument.GetLocation();
                }
            }

            var defaultParameterIndex = attribute.AttributeConstructor?.Parameters
                .Select((parameter, index) => (parameter, index))
                .FirstOrDefault(x => string.Equals(x.parameter.Name, "isDefault", StringComparison.OrdinalIgnoreCase))
                .index;

            if (defaultParameterIndex is not null && defaultParameterIndex.Value < attributeSyntax.ArgumentList.Arguments.Count)
            {
                return attributeSyntax.ArgumentList.Arguments[defaultParameterIndex.Value].GetLocation();
            }

            return attributeSyntax.GetLocation();
        }
    }
}
