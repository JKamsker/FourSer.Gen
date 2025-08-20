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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SerializeCollectionConflictingSizeCodeFixProvider)), Shared]
    public class SerializeCollectionConflictingSizeCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            SerializeCollectionConflictingSizeAnalyzer.UnlimitedConflictDiagnosticId,
            SerializeCollectionConflictingSizeAnalyzer.CountSizeConflictDiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var argument = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<AttributeArgumentSyntax>();

            if (argument != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Remove conflicting argument",
                        createChangedSolution: c => RemoveArgumentAsync(context.Document, argument, c),
                        equivalenceKey: "RemoveArgument"),
                    diagnostic);
            }
        }

        private async Task<Solution> RemoveArgumentAsync(Document document, AttributeArgumentSyntax argumentToRemove, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var attributeArgumentList = argumentToRemove.Parent as AttributeArgumentListSyntax;
            if (attributeArgumentList == null)
            {
                return document.Project.Solution;
            }

            var newArguments = attributeArgumentList.Arguments.Remove(argumentToRemove);
            var newAttributeArgumentList = attributeArgumentList.WithArguments(newArguments);

            var newRoot = root.ReplaceNode(attributeArgumentList, newAttributeArgumentList);

            return document.WithSyntaxRoot(newRoot).Project.Solution;
        }
    }
}
