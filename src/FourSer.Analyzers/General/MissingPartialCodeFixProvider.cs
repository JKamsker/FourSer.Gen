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

namespace FourSer.Analyzers.General
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingPartialCodeFixProvider)), Shared]
    public class MissingPartialCodeFixProvider : CodeFixProvider
    {
        private const string title = "Add partial modifier";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(MissingPartialAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            if (declaration == null) return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedSolution: c => AddPartialModifierAsync(context.Document, declaration, c),
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Solution> AddPartialModifierAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            if (oldRoot == null) return document.Project.Solution;

            var newModifiers = typeDecl.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
            var newTypeDecl = typeDecl.WithModifiers(newModifiers);

            var newRoot = oldRoot.ReplaceNode(typeDecl, newTypeDecl);
            var newDocument = document.WithSyntaxRoot(newRoot);

            return newDocument.Project.Solution;
        }
    }
}
