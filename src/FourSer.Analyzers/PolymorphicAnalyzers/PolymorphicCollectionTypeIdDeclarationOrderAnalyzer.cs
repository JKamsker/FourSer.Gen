#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace FourSer.Analyzers.PolymorphicAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PolymorphicCollectionTypeIdDeclarationOrderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0002";
        public const string MissingTypeIdDiagnosticId = "FS0018";

        private static readonly LocalizableString Title =
            "Polymorphic collection TypeId property must be declared before the collection";

        private static readonly LocalizableString MessageFormat =
            "The property '{0}' must be declared before the collection '{1}' because it is used as the TypeId property.";

        private static readonly LocalizableString Description =
            "For polymorphic collections with a specified TypeId property, the TypeId property must be declared before the collection property to ensure correct deserialization order.";

        private static readonly LocalizableString MissingTypeIdTitle =
            "TypeId property not found for polymorphic collection";

        private static readonly LocalizableString MissingTypeIdMessageFormat =
            "The TypeId property '{0}' specified for collection '{1}' was not found in the class.";

        private static readonly LocalizableString MissingTypeIdDescription =
            "The TypeId property specified in the SerializeCollectionAttribute must exist as a property in the same class.";

        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description
        );

        private static readonly DiagnosticDescriptor MissingTypeIdRule = new(
            MissingTypeIdDiagnosticId,
            MissingTypeIdTitle,
            MissingTypeIdMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: MissingTypeIdDescription
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, MissingTypeIdRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
        }

        private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
        {
            var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;

            var typeDeclaration = propertyDeclaration.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            if (typeDeclaration == null)
            {
                return;
            }

            var semanticModel = context.SemanticModel;
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
            if (typeSymbol == null)
            {
                return;
            }

            var generateSerializerAttribute = context.Compilation.GetTypeByMetadataName("FourSer.Contracts.GenerateSerializerAttribute");
            if (generateSerializerAttribute == null)
            {
                return;
            }

            bool hasGenerateSerializerAttribute = typeSymbol.GetAttributes()
                .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, generateSerializerAttribute));

            if (!hasGenerateSerializerAttribute)
            {
                return;
            }

            var serializeCollectionAttributeSyntax = propertyDeclaration.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(attr => semanticModel.GetTypeInfo(attr).Type?.ToDisplayString() == "FourSer.Contracts.SerializeCollectionAttribute");

            if (serializeCollectionAttributeSyntax == null)
            {
                return;
            }

            var typeIdPropertyArgument = serializeCollectionAttributeSyntax.ArgumentList?.Arguments
                .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == "TypeIdProperty");

            if (typeIdPropertyArgument?.Expression == null)
            {
                return;
            }

            var argumentExpression = typeIdPropertyArgument.Expression;
            string? typeIdPropertyName = null;
            Location? diagnosticLocation = null;

            if (argumentExpression is LiteralExpressionSyntax literalExpression)
            {
                typeIdPropertyName = semanticModel.GetConstantValue(literalExpression).Value as string;
                diagnosticLocation = literalExpression.GetLocation();
            }
            else if (argumentExpression is InvocationExpressionSyntax invocationExpression &&
                     invocationExpression.Expression is IdentifierNameSyntax identifierName &&
                     identifierName.Identifier.ValueText == "nameof" &&
                     invocationExpression.ArgumentList.Arguments.Count == 1)
            {
                var nameofArgument = invocationExpression.ArgumentList.Arguments[0];
                if (nameofArgument.Expression is IdentifierNameSyntax propertyIdentifier)
                {
                    typeIdPropertyName = propertyIdentifier.Identifier.ValueText;
                    diagnosticLocation = propertyIdentifier.GetLocation();
                }
            }

            if (string.IsNullOrEmpty(typeIdPropertyName) || diagnosticLocation == null)
            {
                return;
            }

            var typeIdProperty = typeSymbol.GetMembers(typeIdPropertyName).FirstOrDefault();
            var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration) as IPropertySymbol;
            if (propertySymbol == null)
            {
                return;
            }

            if (typeIdProperty == null)
            {
                var diagnostic = Diagnostic.Create(MissingTypeIdRule, diagnosticLocation, typeIdPropertyName, propertySymbol.Name);
                context.ReportDiagnostic(diagnostic);
                return;
            }

            if (typeIdProperty.Locations.First().SourceSpan.Start > propertySymbol.Locations.First().SourceSpan.Start)
            {
                var diagnostic = Diagnostic.Create(Rule, diagnosticLocation, typeIdPropertyName, propertySymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}