using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FourSer.Analyzers.SerializeCollection
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SerializeCollectionTypeIdPropertyCodeFixProvider)), Shared]
    public class SerializeCollectionTypeIdPropertyCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            SerializeCollectionTypeIdPropertyAnalyzer.NotFoundDiagnosticId,
            SerializeCollectionTypeIdPropertyAnalyzer.WrongTypeDiagnosticId,
            SerializeCollectionTypeIdPropertyAnalyzer.DeclaredAfterPropertyDiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var node = root.FindNode(diagnosticSpan);
            if (node == null) return;

            var argument = node.FirstAncestorOrSelf<AttributeArgumentSyntax>();

            if (argument != null)
            {
                if (diagnostic.Id == SerializeCollectionTypeIdPropertyAnalyzer.NotFoundDiagnosticId)
                {
                    var propertyName = argument.Expression.ToString().Trim('"');
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: $"Create property '{propertyName}'",
                            createChangedSolution: c => CreatePropertyAsync(context.Document, argument, propertyName, c),
                            equivalenceKey: "CreateProperty"),
                        diagnostic);
                }
            }
        }

        private async Task<Solution> CreatePropertyAsync(Document document, AttributeArgumentSyntax argument, string propertyName, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null) return document.Project.Solution;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel == null) return document.Project.Solution;

            var memberDeclaration = argument.FirstAncestorOrSelf<MemberDeclarationSyntax>();
            if (memberDeclaration == null) return document.Project.Solution;

            var symbol = semanticModel.GetDeclaredSymbol(memberDeclaration, cancellationToken);
            if (symbol == null) return document.Project.Solution;

            var propertyType = "int"; // Default

            var polymorphicOptionAttributes = symbol.GetAttributes()
                .Where(ad => ad.AttributeClass?.Name == "PolymorphicOptionAttribute")
                .ToList();

            if (polymorphicOptionAttributes.Any())
            {
                var firstOption = polymorphicOptionAttributes.First();
                if (firstOption.ConstructorArguments.Any())
                {
                    var typeIdArgument = firstOption.ConstructorArguments[0];
                    if (typeIdArgument.Type?.SpecialType == SpecialType.System_Byte)
                    {
                        propertyType = "byte";
                    }
                }
            }


            var property = argument.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
            if (property != null)
            {
                var newProperty = CreateNewProperty(propertyName, propertyType);
                var newRoot = root.InsertNodesBefore(property, new[] { newProperty });
                return document.WithSyntaxRoot(newRoot).Project.Solution;
            }

            var field = argument.FirstAncestorOrSelf<FieldDeclarationSyntax>();
            if (field != null)
            {
                var newProperty = CreateNewProperty(propertyName, propertyType);
                var newRoot = root.InsertNodesBefore(field, new[] { newProperty });
                return document.WithSyntaxRoot(newRoot).Project.Solution;
            }

            return document.Project.Solution;
        }

        private static PropertyDeclarationSyntax CreateNewProperty(string propertyName, string propertyType)
        {
            return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(propertyType), propertyName)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
                {
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                })));
        }
    }
}
