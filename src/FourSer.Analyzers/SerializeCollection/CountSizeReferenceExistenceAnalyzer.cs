#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CountSizeReferenceExistenceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0006";

        private static readonly LocalizableString Title = "Non-existent CountSizeReference property";
        private static readonly LocalizableString MessageFormat = "The property '{0}' specified in CountSizeReference does not exist on type '{1}'";
        private static readonly LocalizableString Description = "The property specified in the CountSizeReference argument of the SerializeCollection attribute must exist on the containing type.";
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

            foreach (var symbol in namedTypeSymbol.GetMembers())
            {
                if (symbol is not IPropertySymbol && symbol is not IFieldSymbol)
                {
                    continue;
                }

                var serializeCollectionAttribute = symbol.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializeCollectionAttribute");

                if (serializeCollectionAttribute == null)
                {
                    continue;
                }

                var countSizeReferenceArgument = serializeCollectionAttribute.NamedArguments
                    .FirstOrDefault(arg => arg.Key == "CountSizeReference");

                if (countSizeReferenceArgument.Key == null)
                {
                    // The argument is not present, so there's nothing to check.
                    return;
                }

                var referencedPropertyName = countSizeReferenceArgument.Value.Value as string;
                if (string.IsNullOrEmpty(referencedPropertyName))
                {
                    // The argument is empty, which is not valid, but a different analyzer might handle this.
                    // For now, we only care if it points to a non-existent property.
                    return;
                }

                var containingType = symbol.ContainingType;
                var referencedProperty = containingType.GetMembers(referencedPropertyName).FirstOrDefault();

                if (referencedProperty == null)
                {
                    var location = AnalyzerHelper.GetNamedArgumentLocation(serializeCollectionAttribute, "CountSizeReference") ?? symbol.Locations[0];
                    var diagnostic = Diagnostic.Create(Rule, location, referencedPropertyName, containingType.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
