using System.Collections.Immutable;
using System.Composition;
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
            if (root == null) return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var attribute = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<AttributeSyntax>().FirstOrDefault();
            if (attribute == null) return;

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

        private async Task<Solution> RemoveAttributeAsync(Document document, AttributeSyntax attributeToRemove, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null) return document.Project.Solution;

            if (attributeToRemove.Parent is not AttributeListSyntax attributeList) return document.Project.Solution;

            SyntaxNode newRoot;

            if (attributeList.Attributes.Count > 1)
            {
                var newAttributeList = attributeList.WithAttributes(attributeList.Attributes.Remove(attributeToRemove));
                newRoot = root.ReplaceNode(attributeList, newAttributeList);
            }
            else
            {
                var member = attributeList.Parent as MemberDeclarationSyntax;
                if (member != null)
                {
                    var newMember = member.WithAttributeLists(member.AttributeLists.Remove(attributeList));
                    newRoot = root.ReplaceNode(member, newMember);
                }
                else
                {
                    newRoot = root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepExteriorTrivia);
                }
            }

            return document.WithSyntaxRoot(newRoot).Project.Solution;
        }

        private async Task<Solution> ReplaceWithPolymorphicAttributeAsync(Document document, AttributeSyntax attribute, CancellationToken cancellationToken)
        {
            var newAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("SerializePolymorphic"));
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            if (oldRoot == null) return document.Project.Solution;

            var newRoot = oldRoot.ReplaceNode(attribute, newAttribute);
            return document.WithSyntaxRoot(newRoot).Project.Solution;
        }
    }
}
