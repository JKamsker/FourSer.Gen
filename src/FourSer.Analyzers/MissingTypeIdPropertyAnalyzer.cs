#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MissingTypeIdPropertyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0014";

        private static readonly LocalizableString Title = "Missing TypeIdProperty";
        private static readonly LocalizableString MessageFormat = "The 'TypeIdProperty' must be set when using PolymorphicMode.SingleTypeId.";
        private static readonly LocalizableString Description = "When PolymorphicMode is set to SingleTypeId, the TypeIdProperty must be specified to identify the property that holds the type ID for the collection.";
        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property, SymbolKind.Field);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var symbol = context.Symbol;

            var serializeCollectionAttribute = symbol.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializeCollectionAttribute");

            if (serializeCollectionAttribute == null)
            {
                return;
            }

            var polymorphicModeArg = serializeCollectionAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "PolymorphicMode");
            if (polymorphicModeArg.Key == null)
            {
                return;
            }

            // PolymorphicMode.SingleTypeId has a value of 1 in the enum.
            if (polymorphicModeArg.Value.Value is int mode && mode == 1)
            {
                var typeIdPropertyArg = serializeCollectionAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "TypeIdProperty");
                if (typeIdPropertyArg.Key == null || typeIdPropertyArg.Value.Value is not string value || string.IsNullOrEmpty(value))
                {
                    var diagnostic = Diagnostic.Create(Rule, serializeCollectionAttribute.ApplicationSyntaxReference!.GetSyntax().GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
