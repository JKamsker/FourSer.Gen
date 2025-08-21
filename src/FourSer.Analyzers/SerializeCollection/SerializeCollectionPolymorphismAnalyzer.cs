using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SerializeCollectionPolymorphismAnalyzer : DiagnosticAnalyzer
    {
        public const string TypeIdTypeMismatchDiagnosticId = "FSG1010";
        public const string IndividualTypeIdsWithTypeIdPropertyDiagnosticId = "FSG1011";
        public const string ConflictingPolymorphicSettingsDiagnosticId = "FSG1013";

        private static readonly LocalizableString Title = "Invalid polymorphic settings";
        private static readonly LocalizableString TypeIdTypeMismatchMessageFormat = "'TypeIdType' must match the type of the property specified in 'TypeIdProperty'";
        private static readonly LocalizableString IndividualTypeIdsWithTypeIdPropertyMessageFormat = "'TypeIdProperty' cannot be set when 'PolymorphicMode' is 'IndividualTypeIds'";
        private static readonly LocalizableString ConflictingPolymorphicSettingsMessageFormat = "Conflicting polymorphic settings on '[SerializeCollection]' and '[SerializePolymorphic]'";
        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor TypeIdTypeMismatchRule = new DiagnosticDescriptor(TypeIdTypeMismatchDiagnosticId, Title, TypeIdTypeMismatchMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
        internal static readonly DiagnosticDescriptor IndividualTypeIdsWithTypeIdPropertyRule = new DiagnosticDescriptor(IndividualTypeIdsWithTypeIdPropertyDiagnosticId, Title, IndividualTypeIdsWithTypeIdPropertyMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
        internal static readonly DiagnosticDescriptor ConflictingPolymorphicSettingsRule = new DiagnosticDescriptor(ConflictingPolymorphicSettingsDiagnosticId, Title, ConflictingPolymorphicSettingsMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(TypeIdTypeMismatchRule, IndividualTypeIdsWithTypeIdPropertyRule, ConflictingPolymorphicSettingsRule);

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
            var serializeCollectionAttribute = symbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "SerializeCollectionAttribute");

            if (serializeCollectionAttribute == null || serializeCollectionAttribute.ApplicationSyntaxReference == null)
            {
                return;
            }

            var attributeSyntax = (AttributeSyntax)serializeCollectionAttribute.ApplicationSyntaxReference.GetSyntax(context.CancellationToken);
            var arguments = attributeSyntax.ArgumentList?.Arguments ?? new SeparatedSyntaxList<AttributeArgumentSyntax>();

            var namedArguments = serializeCollectionAttribute.NamedArguments.ToDictionary(na => na.Key, na => na.Value);

            var typeIdPropertyArg = arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "TypeIdProperty");
            var typeIdTypeArg = arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "TypeIdType");

            // FSG1011
            if (namedArguments.TryGetValue("PolymorphicMode", out var polymorphicModeValue) && typeIdPropertyArg != null)
            {
                if (polymorphicModeValue.Value is int mode && mode == 2 /* IndividualTypeIds */)
                {
                    context.ReportDiagnostic(Diagnostic.Create(IndividualTypeIdsWithTypeIdPropertyRule, typeIdPropertyArg.GetLocation()));
                }
            }

            // FSG1010
            if (namedArguments.TryGetValue("TypeIdProperty", out var typeIdPropertyValue) && typeIdTypeArg != null)
            {
                var referenceName = typeIdPropertyValue.Value as string;
                if (string.IsNullOrEmpty(referenceName)) return;

                var referencedSymbol = symbol.ContainingType.GetMembers(referenceName).FirstOrDefault();
                if (referencedSymbol == null) return;

                var typeOfExpression = typeIdTypeArg.Expression as TypeOfExpressionSyntax;
                if (typeOfExpression != null)
                {
                    var typeSyntax = typeOfExpression.Type;
                    var model = context.Compilation.GetSemanticModel(typeSyntax.SyntaxTree);
                    var typeInfo = model.GetTypeInfo(typeSyntax, context.CancellationToken);
                    var typeIdType = typeInfo.Type;

                    if (referencedSymbol is IPropertySymbol propertySymbol && !SymbolEqualityComparer.Default.Equals(propertySymbol.Type, typeIdType))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(TypeIdTypeMismatchRule, typeIdTypeArg.GetLocation()));
                    }
                    else if (referencedSymbol is IFieldSymbol fieldSymbol && !SymbolEqualityComparer.Default.Equals(fieldSymbol.Type, typeIdType))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(TypeIdTypeMismatchRule, typeIdTypeArg.GetLocation()));
                    }
                }
            }

            // FSG1013
            var serializePolymorphicAttribute = symbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "SerializePolymorphicAttribute");
            if (serializePolymorphicAttribute != null)
            {
                var polyAttributeSyntax = (AttributeSyntax)serializePolymorphicAttribute.ApplicationSyntaxReference.GetSyntax(context.CancellationToken);
                var polyArguments = polyAttributeSyntax.ArgumentList?.Arguments ?? new SeparatedSyntaxList<AttributeArgumentSyntax>();

                var polyTypeIdPropertyArg = polyArguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "PropertyName");
                var polyTypeIdTypeArg = polyArguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "TypeIdType");

                if ((typeIdPropertyArg != null && polyTypeIdPropertyArg != null) || (typeIdTypeArg != null && polyTypeIdTypeArg != null))
                {
                    context.ReportDiagnostic(Diagnostic.Create(ConflictingPolymorphicSettingsRule, polyAttributeSyntax.GetLocation()));
                }
            }
        }
    }
}
