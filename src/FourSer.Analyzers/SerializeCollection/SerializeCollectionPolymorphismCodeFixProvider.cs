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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SerializeCollectionPolymorphismCodeFixProvider)), Shared]
    public class SerializeCollectionPolymorphismCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            SerializeCollectionPolymorphismAnalyzer.TypeIdTypeMismatchDiagnosticId,
            SerializeCollectionPolymorphismAnalyzer.IndividualTypeIdsWithTypeIdPropertyDiagnosticId,
            SerializeCollectionPolymorphismAnalyzer.ConflictingPolymorphicSettingsDiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var node = root.FindNode(diagnosticSpan);
            var argument = node.FirstAncestorOrSelf<AttributeArgumentSyntax>();

            if (argument != null)
            {
                if (diagnostic.Id == SerializeCollectionPolymorphismAnalyzer.IndividualTypeIdsWithTypeIdPropertyDiagnosticId)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Remove TypeIdProperty",
                            createChangedSolution: c => RemoveArgumentAsync(context.Document, argument, c),
                            equivalenceKey: "RemoveTypeIdProperty"),
                        diagnostic);
                }
            }
        }

        private async Task<Solution> RemoveArgumentAsync(Document document, AttributeArgumentSyntax argumentToRemove, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root == null) return document.Project.Solution;

            if (argumentToRemove.Parent is not AttributeArgumentListSyntax attributeArgumentList)
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
