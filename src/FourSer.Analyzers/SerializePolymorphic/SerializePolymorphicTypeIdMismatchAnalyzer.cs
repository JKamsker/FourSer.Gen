using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;
using FourSer.Analyzers.Helpers;

namespace FourSer.Analyzers.SerializePolymorphic
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SerializePolymorphicTypeIdMismatchAnalyzer : DiagnosticAnalyzer
    {
        public const string TypeIdTypeMismatchDiagnosticId = "FSG2004";
        public const string ConflictingSettingsDiagnosticId = "FSG2005";

        private static readonly LocalizableString Title = "Invalid polymorphic settings";
        private static readonly LocalizableString TypeIdTypeMismatchMessageFormat = "'TypeIdType' must match the type of the property specified in 'PropertyName'";
        private static readonly LocalizableString ConflictingSettingsMessageFormat = "Conflicting polymorphic settings on '[SerializeCollection]' and '[SerializePolymorphic]'";
        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor TypeIdTypeMismatchRule = new DiagnosticDescriptor(TypeIdTypeMismatchDiagnosticId, Title, TypeIdTypeMismatchMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
        internal static readonly DiagnosticDescriptor ConflictingSettingsRule = new DiagnosticDescriptor(ConflictingSettingsDiagnosticId, Title, ConflictingSettingsMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(TypeIdTypeMismatchRule, ConflictingSettingsRule);

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
            var serializePolymorphicAttribute = symbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "SerializePolymorphicAttribute");

            if (serializePolymorphicAttribute == null || serializePolymorphicAttribute.ApplicationSyntaxReference == null)
            {
                return;
            }

            var attributeSyntax = (AttributeSyntax)serializePolymorphicAttribute.ApplicationSyntaxReference.GetSyntax(context.CancellationToken);
            var arguments = attributeSyntax.ArgumentList?.Arguments ?? new SeparatedSyntaxList<AttributeArgumentSyntax>();

            var namedArguments = serializePolymorphicAttribute.NamedArguments.ToDictionary(na => na.Key, na => na.Value);

            var typeIdTypeArg = arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "TypeIdType");

            string referenceName = null;
            if (namedArguments.TryGetValue("PropertyName", out var propertyNameValue))
            {
                referenceName = propertyNameValue.Value as string;
            }
            else if (serializePolymorphicAttribute.ConstructorArguments.Any())
            {
                referenceName = serializePolymorphicAttribute.ConstructorArguments[0].Value as string;
            }

            // FSG2004
            if (!string.IsNullOrEmpty(referenceName) && typeIdTypeArg != null)
            {
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

            // FSG2005
            var serializeCollectionAttribute = symbol.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "SerializeCollectionAttribute");
            if (serializeCollectionAttribute != null && serializeCollectionAttribute.ApplicationSyntaxReference != null)
            {
                var collectionAttributeSyntax = (AttributeSyntax)serializeCollectionAttribute.ApplicationSyntaxReference.GetSyntax(context.CancellationToken);
                var collectionArguments = collectionAttributeSyntax.ArgumentList?.Arguments ?? new SeparatedSyntaxList<AttributeArgumentSyntax>();

                var collectionTypeIdPropertyArg = collectionArguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "TypeIdProperty");
                var collectionTypeIdTypeArg = collectionArguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "TypeIdType");

                var propertyNameArg = arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "PropertyName");

                if ((propertyNameArg != null && collectionTypeIdPropertyArg != null) || (typeIdTypeArg != null && collectionTypeIdTypeArg != null))
                {
                    context.ReportDiagnostic(Diagnostic.Create(ConflictingSettingsRule, attributeSyntax.GetLocation()));
                }
            }
        }
    }
}
