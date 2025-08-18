#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CountSizeReferenceOrderAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0017";

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
            context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);
        }

        private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
        {
            var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
            
            // Check if the containing type has GenerateSerializerAttribute
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

            // Find SerializeCollectionAttribute on this property
            var serializeCollectionAttribute = propertyDeclaration.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(attr => semanticModel.GetTypeInfo(attr).Type?.ToDisplayString() == "FourSer.Contracts.SerializeCollectionAttribute");

            if (serializeCollectionAttribute == null)
            {
                return;
            }

            // Find the CountSizeReference argument
            var countSizeRefArgument = serializeCollectionAttribute.ArgumentList?.Arguments
                .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == "CountSizeReference");

            if (countSizeRefArgument?.Expression == null)
            {
                return;
            }

            var argumentExpression = countSizeRefArgument.Expression;
            string? countPropertyName = null;
            Location? diagnosticLocation = null;

            // Handle both literal expressions ("Count") and nameof expressions (nameof(Count))
            if (argumentExpression is LiteralExpressionSyntax literalExpression)
            {
                countPropertyName = semanticModel.GetConstantValue(literalExpression).Value as string;
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
                    countPropertyName = propertyIdentifier.Identifier.ValueText;
                    diagnosticLocation = propertyIdentifier.GetLocation();
                }
            }

            if (string.IsNullOrEmpty(countPropertyName) || diagnosticLocation == null)
            {
                return;
            }

            // Find the referenced property in the type
            var countProperty = typeSymbol.GetMembers(countPropertyName).FirstOrDefault();
            if (countProperty == null)
            {
                // This is handled by another analyzer (e.g. CountSizeReferenceExistenceAnalyzer)
                return;
            }

            var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration) as IPropertySymbol;
            if (propertySymbol == null)
            {
                return;
            }

            // Check if the count property is declared after the collection property
            if (countProperty.Locations.First().SourceSpan.Start > propertySymbol.Locations.First().SourceSpan.Start)
            {
                // Report diagnostic on the CountSizeReference argument location
                var diagnostic = Diagnostic.Create(Rule, diagnosticLocation, countPropertyName, propertySymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}