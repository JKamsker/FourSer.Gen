using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SerializeCollectionConflictingSizeAnalyzer : DiagnosticAnalyzer
    {
        public const string UnlimitedConflictDiagnosticId = "FSG1001";
        public const string CountSizeConflictDiagnosticId = "FSG1002";
        public const string UnlimitedReferenceConflictDiagnosticId = "FSG1003";

        private static readonly LocalizableString UnlimitedTitle = "Conflicting size settings on [SerializeCollection]";
        private static readonly LocalizableString UnlimitedMessageFormat = "Cannot use 'CountSize' when 'Unlimited' is set to true";
        private static readonly LocalizableString UnlimitedDescription = "The 'Unlimited' property cannot be combined with 'CountSize'.";

        private static readonly LocalizableString CountSizeTitle = "Conflicting size settings on [SerializeCollection]";
        private static readonly LocalizableString CountSizeMessageFormat = "Cannot use 'CountSizeReference' when 'CountSize' is set";
        private static readonly LocalizableString CountSizeDescription = "The 'CountSize' property cannot be combined with 'CountSizeReference'.";

        private static readonly LocalizableString UnlimitedReferenceTitle = "Conflicting size settings on [SerializeCollection]";
        private static readonly LocalizableString UnlimitedReferenceMessageFormat = "Cannot use 'CountSizeReference' when 'Unlimited' is set to true";
        private static readonly LocalizableString UnlimitedReferenceDescription = "The 'Unlimited' property cannot be combined with 'CountSizeReference'.";

        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor UnlimitedRule = new DiagnosticDescriptor(UnlimitedConflictDiagnosticId, UnlimitedTitle, UnlimitedMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: UnlimitedDescription);
        internal static readonly DiagnosticDescriptor CountSizeRule = new DiagnosticDescriptor(CountSizeConflictDiagnosticId, CountSizeTitle, CountSizeMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: CountSizeDescription);
        internal static readonly DiagnosticDescriptor UnlimitedReferenceRule = new DiagnosticDescriptor(UnlimitedReferenceConflictDiagnosticId, UnlimitedReferenceTitle, UnlimitedReferenceMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: UnlimitedReferenceDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(UnlimitedRule, CountSizeRule, UnlimitedReferenceRule);

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

            var namedArguments = attribute.NamedArguments.ToDictionary(na => na.Key, na => na.Value);

            bool isUnlimited = namedArguments.TryGetValue("Unlimited", out var unlimitedValue) && unlimitedValue.Value is true;
            bool hasCountSize = namedArguments.ContainsKey("CountSize");
            bool hasCountSizeReference = namedArguments.ContainsKey("CountSizeReference");

            var attributeSyntax = (AttributeSyntax)attribute.ApplicationSyntaxReference.GetSyntax(context.CancellationToken);

            if (isUnlimited)
            {
                if (hasCountSize) ReportDiagnostic(context, attributeSyntax, "CountSize", UnlimitedRule);
                if (hasCountSizeReference) ReportDiagnostic(context, attributeSyntax, "CountSizeReference", UnlimitedReferenceRule);
            }

            if (hasCountSize)
            {
                if (hasCountSizeReference) ReportDiagnostic(context, attributeSyntax, "CountSizeReference", CountSizeRule);
            }
        }

        private void ReportDiagnostic(SymbolAnalysisContext context, AttributeSyntax attributeSyntax, string conflictingArgumentName, DiagnosticDescriptor rule)
        {
            var argumentSyntax = attributeSyntax.ArgumentList?.Arguments
                .OfType<AttributeArgumentSyntax>()
                .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == conflictingArgumentName);

            if (argumentSyntax != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, argumentSyntax.GetLocation(), conflictingArgumentName));
            }
        }
    }
}
