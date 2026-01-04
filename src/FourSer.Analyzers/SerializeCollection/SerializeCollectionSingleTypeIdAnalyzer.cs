using System.Collections.Immutable;
using FourSer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SerializeCollectionSingleTypeIdAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FSG1014";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.SerializeCollectionSingleTypeId_Title), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.SerializeCollectionSingleTypeId_MessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.SerializeCollectionSingleTypeId_Description), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property, SymbolKind.Field);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var symbol = context.Symbol;

            var serializeCollectionAttribute = symbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "SerializeCollectionAttribute");
            if (serializeCollectionAttribute == null)
            {
                return;
            }

            if (symbol.HasIgnoreAttribute())
            {
                return;
            }

            if (!serializeCollectionAttribute.NamedArguments.Any(na => na.Key == "PolymorphicMode" && na.Value.Value is 1))
            {
                return;
            }

            var polymorphicOptions = symbol.GetAttributes().Where(ad => ad.AttributeClass?.Name == "PolymorphicOptionAttribute").ToList();
            if (!polymorphicOptions.Any())
            {
                return;
            }

            var firstOptionIdType = polymorphicOptions[0].ConstructorArguments[0].Type;
            if (firstOptionIdType == null)
            {
                return;
            }

            var typeIdTypeArgument = serializeCollectionAttribute.NamedArguments.FirstOrDefault(na => na.Key == "TypeIdType");
            if (typeIdTypeArgument.Value.Value is ITypeSymbol typeIdType)
            {
                if (!SymbolEqualityComparer.Default.Equals(typeIdType, firstOptionIdType))
                {
                    var location = serializeCollectionAttribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();
                    context.ReportDiagnostic(Diagnostic.Create(Rule, location, typeIdType.Name, firstOptionIdType.Name));
                }
            }
        }
    }
}
