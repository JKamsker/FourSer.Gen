#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CountSizeReferenceOrderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0007";

        private static readonly LocalizableString Title = "CountSizeReference target must be declared before the collection";
        private static readonly LocalizableString MessageFormat = "The property '{0}' referenced by CountSizeReference must be declared before the collection property '{1}'";
        private static readonly LocalizableString Description = "The property referenced by CountSizeReference must be declared before the collection property that uses it to ensure correct serialization logic generation.";
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
            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private void AnalyzeNamedType(SymbolAnalysisContext context)
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

            foreach (var member in namedTypeSymbol.GetMembers())
            {
                if (member is not IPropertySymbol propertySymbol)
                {
                    continue;
                }

                var serializeCollectionAttribute = member.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializeCollectionAttribute");

                if (serializeCollectionAttribute == null)
                {
                    continue;
                }

                var countSizeRefArg = serializeCollectionAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "CountSizeReference");
                if (countSizeRefArg.Key == null || countSizeRefArg.Value.Value is not string countPropertyName)
                {
                    continue;
                }

                var countProperty = namedTypeSymbol.GetMembers(countPropertyName).FirstOrDefault();
                if (countProperty == null)
                {
                    // This is handled by another analyzer (e.g. CountSizeReferenceExistenceAnalyzer)
                    continue;
                }

                if (countProperty.Locations.First().SourceSpan.Start > propertySymbol.Locations.First().SourceSpan.Start)
                {
                    var diagnostic = Diagnostic.Create(Rule, propertySymbol.Locations[0], countPropertyName, propertySymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}