#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InvalidPolymorphicOptionContextAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0008";

        private static readonly LocalizableString Title = "Invalid [PolymorphicOption] context";
        private static readonly LocalizableString MessageFormat = "The [PolymorphicOption] attribute is used without a valid polymorphic context. Apply either [SerializePolymorphic] or [SerializeCollection(PolymorphicMode = ...)] to the member '{0}'.";
        private static readonly LocalizableString Description = "The [PolymorphicOption] attribute can only be used on members that are also marked for polymorphic serialization.";
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

            var polymorphicOptionAttributes = symbol.GetAttributes()
                .Where(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.PolymorphicOptionAttribute")
                .ToList();

            if (polymorphicOptionAttributes.Count == 0)
            {
                return;
            }

            // Check for [SerializePolymorphic]
            var hasSerializePolymorphic = symbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializePolymorphicAttribute");

            if (hasSerializePolymorphic)
            {
                return;
            }

            // Check for [SerializeCollection(PolymorphicMode = ...)]
            var serializeCollectionAttribute = symbol.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializeCollectionAttribute");

            if (serializeCollectionAttribute != null)
            {
                var polymorphicModeArgument = serializeCollectionAttribute.NamedArguments
                    .FirstOrDefault(arg => arg.Key == "PolymorphicMode");

                if (polymorphicModeArgument.Key != null)
                {
                    // The enum value for None is 0.
                    if (polymorphicModeArgument.Value.Value is int mode && mode != 0)
                    {
                        return;
                    }
                }
            }

            // If we reach here, there's no valid context for [PolymorphicOption]
            var diagnostic = Diagnostic.Create(Rule, symbol.Locations[0], symbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
