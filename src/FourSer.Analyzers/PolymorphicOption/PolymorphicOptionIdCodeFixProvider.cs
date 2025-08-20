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
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var argument = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<AttributeArgumentSyntax>();
            var attribute = argument.Parent.Parent as AttributeSyntax;

            if (attribute != null)
            {
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
        }

        private async Task<Solution> RemoveAttributeAsync(Document document, AttributeSyntax attribute, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var attributeList = attribute.Parent as AttributeListSyntax;

            if (attributeList != null && attributeList.Attributes.Count == 1)
            {
                var newRoot = root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepNoTrivia);
                return document.WithSyntaxRoot(newRoot).Project.Solution;
            }
            else
            {
                var newRoot = root.RemoveNode(attribute, SyntaxRemoveOptions.KeepNoTrivia);
                return document.WithSyntaxRoot(newRoot).Project.Solution;
            }
        }
    }
}
