using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PolymorphicCollectionTypeIdDeclarationOrderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FSSG002";

        private static readonly LocalizableString Title = "Polymorphic collection TypeId property must be declared before the collection";
        private static readonly LocalizableString MessageFormat = "The property '{0}' must be declared before the collection '{1}' because it is used as the TypeId property.";
        private static readonly LocalizableString Description = "For polymorphic collections with a specified TypeId property, the TypeId property must be declared before the collection property to ensure correct deserialization order.";
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

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

            var members = namedTypeSymbol.GetMembers();
            var memberLocations = members.ToDictionary(m => m.Name, m => m.Locations.FirstOrDefault());

            foreach (var member in members)
            {
                if (member is not IPropertySymbol propertySymbol)
                {
                    continue;
                }

                var collectionAttribute = propertySymbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializeCollectionAttribute");
                if (collectionAttribute == null)
                {
                    continue;
                }

                var polymorphicMode = collectionAttribute.NamedArguments.FirstOrDefault(na => na.Key == "PolymorphicMode").Value;
                if (polymorphicMode.Value is not int mode || mode != 1) // 1 is SingleTypeId
                {
                    continue;
                }

                var typeIdPropertyArg = collectionAttribute.NamedArguments.FirstOrDefault(na => na.Key == "TypeIdProperty").Value;
                if (typeIdPropertyArg.Value is not string typeIdPropertyName)
                {
                    continue;
                }

                if (!memberLocations.TryGetValue(typeIdPropertyName, out var typeIdPropertyLocation) || typeIdPropertyLocation == null)
                {
                    continue;
                }

                var collectionLocation = memberLocations[propertySymbol.Name];
                if (collectionLocation == null)
                {
                    continue;
                }

                if (typeIdPropertyLocation.SourceSpan.Start > collectionLocation.SourceSpan.Start)
                {
                    var diagnostic = Diagnostic.Create(Rule, collectionLocation, typeIdPropertyName, propertySymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
