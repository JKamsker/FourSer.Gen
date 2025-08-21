using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FourSer.Analyzers.PolymorphicOption
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PolymorphicOptionIdCodeFixProvider)), Shared]
    public class PolymorphicOptionIdCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            PolymorphicOptionIdAnalyzer.DuplicateIdDiagnosticId,
            PolymorphicOptionIdAnalyzer.MixedIdTypesDiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var argument = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<AttributeArgumentSyntax>();
            if (argument?.Parent?.Parent is not AttributeSyntax attribute) return;

            if (diagnostic.Id == PolymorphicOptionIdAnalyzer.DuplicateIdDiagnosticId)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Remove duplicate PolymorphicOption",
                        createChangedSolution: c => RemoveAttributeAsync(context.Document, attribute, c),
                        equivalenceKey: "RemoveDuplicate"),
                    diagnostic);
            }
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
    }
}
