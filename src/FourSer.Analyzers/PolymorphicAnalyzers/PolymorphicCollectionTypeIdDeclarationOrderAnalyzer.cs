using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PolymorphicCollectionTypeIdDeclarationOrderAnalyzer : DiagnosticAnalyzer
    {
        // ... (Rule, DiagnosticId, etc. remain the same) ...
        public const string DiagnosticId = "FSSG002";

        private static readonly LocalizableString Title =
            "Polymorphic collection TypeId property must be declared before the collection";

        private static readonly LocalizableString MessageFormat =
            "The property '{0}' must be declared before the collection '{1}' because it is used as the TypeId property.";

        private static readonly LocalizableString Description =
            "For polymorphic collections with a specified TypeId property, the TypeId property must be declared before the collection property to ensure correct deserialization order.";

        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor
        (
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description
        );

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

            // 1. Get the INamedTypeSymbol for your attributes from the compilation.
            // This is the core improvement.
            var generateSerializerAttribute = context.Compilation.GetTypeByMetadataName("FourSer.Contracts.GenerateSerializerAttribute");
            var serializeCollectionAttribute = context.Compilation.GetTypeByMetadataName("FourSer.Contracts.SerializeCollectionAttribute");

            // If the attributes are not found in the compilation, we can't do anything.
            if (generateSerializerAttribute == null || serializeCollectionAttribute == null)
            {
                return;
            }

            // 2. Check for the class attribute by comparing symbols directly.
            // This is much more efficient and robust than string comparison.
            bool hasGenerateSerializerAttribute = namedTypeSymbol.GetAttributes()
                .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, generateSerializerAttribute));

            if (!hasGenerateSerializerAttribute)
            {
                return;
            }

            // 3. Get all properties once for efficient lookup.
            var properties = namedTypeSymbol.GetMembers().OfType<IPropertySymbol>().ToImmutableArray();

            foreach (var propertySymbol in properties)
            {
                var collectionAttributeData = propertySymbol.GetAttributes()
                    .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, serializeCollectionAttribute));

                if (collectionAttributeData == null)
                {
                    continue;
                }

                // Get the TypeId property name from the attribute argument.
                var typeIdPropertyArg = collectionAttributeData.NamedArguments
                                          .FirstOrDefault(na => na.Key == "TypeIdProperty");

                if (typeIdPropertyArg.Value.Value is not string typeIdPropertyName)
                {
                    continue;
                }

                // 4. Find the TypeId property symbol more directly.
                var typeIdProperty = properties.FirstOrDefault(p => p.Name == typeIdPropertyName);
                var collectionLocation = propertySymbol.Locations.FirstOrDefault();
                
                // Ensure the property exists and has a location.
                if (typeIdProperty == null || collectionLocation == null)
                {
                    continue;
                }
                
                var typeIdPropertyLocation = typeIdProperty.Locations.FirstOrDefault();
                if (typeIdPropertyLocation == null)
                {
                    continue;
                }

                // Compare source code positions to enforce declaration order.
                if (typeIdPropertyLocation.SourceSpan.Start > collectionLocation.SourceSpan.Start)
                {
                    var diagnostic = Diagnostic.Create(Rule, collectionLocation, typeIdPropertyName, propertySymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}