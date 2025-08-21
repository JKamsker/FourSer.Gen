using System.Collections.Immutable;
using System.Composition;
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
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null) return document.Project.Solution;

            var property = argument.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
            if (property != null)
            {
                var newProperty = CreateNewProperty(propertyName);
                var newRoot = root.InsertNodesBefore(property, new[] { newProperty });
                return document.WithSyntaxRoot(newRoot).Project.Solution;
            }

            var field = argument.FirstAncestorOrSelf<FieldDeclarationSyntax>();
            if (field != null)
            {
                var newProperty = CreateNewProperty(propertyName);
                var newRoot = root.InsertNodesBefore(field, new[] { newProperty });
                return document.WithSyntaxRoot(newRoot).Project.Solution;
            }

            return document.Project.Solution;
        }

        private static PropertyDeclarationSyntax CreateNewProperty(string propertyName)
        {
            return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName("int"), propertyName)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
                {
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                })));
        }
    }
}
