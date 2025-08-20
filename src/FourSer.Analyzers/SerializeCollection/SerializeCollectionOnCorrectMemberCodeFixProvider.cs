using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FourSer.Analyzers.SerializeCollection
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SerializeCollectionOnCorrectMemberCodeFixProvider)), Shared]
    public class SerializeCollectionOnCorrectMemberCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(SerializeCollectionOnCorrectMemberAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var attribute = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<AttributeSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove [SerializeCollection]",
                    createChangedSolution: c => RemoveAttributeAsync(context.Document, attribute, c),
                    equivalenceKey: "Remove"),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Replace with [SerializePolymorphic]",
                    createChangedSolution: c => ReplaceWithPolymorphicAttributeAsync(context.Document, attribute, c),
                    equivalenceKey: "ReplaceWithPolymorphic"),
                diagnostic);
        }

        private async Task<Solution> RemoveAttributeAsync(Document document, AttributeSyntax attribute, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var attributeList = attribute.Parent as AttributeListSyntax;

            if (attributeList != null && attributeList.Attributes.Count == 1)
            {
                var newRoot = root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepExteriorTrivia);
                return document.WithSyntaxRoot(newRoot).Project.Solution;
            }
            else
            {
                var newRoot = root.RemoveNode(attribute, SyntaxRemoveOptions.KeepExteriorTrivia);
                return document.WithSyntaxRoot(newRoot).Project.Solution;
            }
        }

        private async Task<Solution> ReplaceWithPolymorphicAttributeAsync(Document document, AttributeSyntax attribute, CancellationToken cancellationToken)
        {
            var newAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("SerializePolymorphic"));
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = oldRoot.ReplaceNode(attribute, newAttribute);
            return document.WithSyntaxRoot(newRoot).Project.Solution;
        }
    }
}
