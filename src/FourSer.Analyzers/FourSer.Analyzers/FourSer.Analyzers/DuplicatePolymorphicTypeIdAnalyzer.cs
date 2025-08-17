#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DuplicatePolymorphicTypeIdAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0010";

        private static readonly LocalizableString Title = "Duplicate polymorphic TypeId";
        private static readonly LocalizableString MessageFormat = "The TypeId '{0}' is used more than once. Polymorphic option TypeIds must be unique for a given member.";
        private static readonly LocalizableString Description = "All [PolymorphicOption] attributes on a single member must have unique TypeIds.";
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
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property, SymbolKind.Field);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var symbol = context.Symbol;

            var polymorphicOptionAttributes = symbol.GetAttributes()
                .Where(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.PolymorphicOptionAttribute")
                .ToList();

            if (polymorphicOptionAttributes.Count < 2)
            {
                return;
            }

            var seenTypeIds = new HashSet<object?>();
            foreach (var attribute in polymorphicOptionAttributes)
            {
                if (attribute.ConstructorArguments.Length > 0)
                {
                    var typeIdArgument = attribute.ConstructorArguments[0];
                    if (typeIdArgument.Value != null)
                    {
                        if (!seenTypeIds.Add(typeIdArgument.Value))
                        {
                            var diagnostic = Diagnostic.Create(Rule, attribute.ApplicationSyntaxReference!.GetSyntax().GetLocation(), typeIdArgument.Value);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }
    }
}
