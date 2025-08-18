#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MismatchedTypeIdPropertyTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0015";

        private static readonly LocalizableString Title = "Mismatched TypeIdProperty type";
        private static readonly LocalizableString MessageFormat = "The type of the TypeIdProperty '{0}' is '{1}', which does not match the expected TypeIdType '{2}'.";
        private static readonly LocalizableString Description = "The type of the property specified in TypeIdProperty must match the type specified in TypeIdType.";
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
            if (polymorphicModeArg.Key == null || polymorphicModeArg.Value.Value is not int mode || mode != 1) // SingleTypeId
            {
                return;
            }

            var typeIdPropertyArg = serializeCollectionAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "TypeIdProperty");
            if (typeIdPropertyArg.Key == null || typeIdPropertyArg.Value.Value is not string propertyName || string.IsNullOrEmpty(propertyName))
            {
                return; // Handled by MissingTypeIdPropertyAnalyzer
            }

            var containingType = symbol.ContainingType;
            var typeIdProperty = containingType.GetMembers(propertyName).FirstOrDefault();
            if (typeIdProperty == null)
            {
                return; // Handled by a different analyzer (FS0006 for CountSizeReference, could be a new one for this)
            }

            ITypeSymbol? actualPropertyType = null;
            if (typeIdProperty is IPropertySymbol propertySymbol)
            {
                actualPropertyType = propertySymbol.Type;
            }
            else if (typeIdProperty is IFieldSymbol fieldSymbol)
            {
                actualPropertyType = fieldSymbol.Type;
            }

            if (actualPropertyType == null)
            {
                return;
            }

            var typeIdTypeArg = serializeCollectionAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "TypeIdType");
            ITypeSymbol expectedTypeIdType = context.Compilation.GetSpecialType(SpecialType.System_Int32); // Default
            if (typeIdTypeArg.Key != null && typeIdTypeArg.Value.Value is ITypeSymbol specifiedType)
            {
                expectedTypeIdType = specifiedType;
            }

            if (!SymbolEqualityComparer.Default.Equals(actualPropertyType, expectedTypeIdType))
            {
                var diagnostic = Diagnostic.Create(Rule, typeIdProperty.Locations[0], propertyName, actualPropertyType.Name, expectedTypeIdType.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
