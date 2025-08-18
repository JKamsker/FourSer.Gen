#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MutuallyExclusiveCollectionSizeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0011";

        private static readonly LocalizableString Title = "Mutually exclusive collection size attributes";
        private static readonly LocalizableString MessageFormat = "The collection size attributes 'CountSize', 'CountSizeReference', and 'Unlimited' are mutually exclusive. Only one can be used at a time.";
        private static readonly LocalizableString Description = "The 'CountSize', 'CountSizeReference', and 'Unlimited' properties on the [SerializeCollection] attribute cannot be used together.";
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
            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            var generateSerializerAttribute = context.Compilation.GetTypeByMetadataName("FourSer.Contracts.GenerateSerializerAttribute");
            if (generateSerializerAttribute == null)
            {
                return;
            }

            bool hasGenerateSerializerAttribute = namedTypeSymbol.GetAttributes()
                .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, generateSerializerAttribute));

            if (!hasGenerateSerializerAttribute)
            {
                return;
            }

            foreach (var symbol in namedTypeSymbol.GetMembers())
            {
                if (symbol is not IPropertySymbol && symbol is not IFieldSymbol)
                {
                    continue;
                }

                var serializeCollectionAttribute = symbol.GetAttributes()
                    .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializeCollectionAttribute");

                if (serializeCollectionAttribute == null)
                {
                    continue;
                }

                int setCount = 0;

                var countSizeArg = serializeCollectionAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "CountSize");
                if (countSizeArg.Key != null && countSizeArg.Value.Value is int countSize && countSize != -1)
                {
                    setCount++;
                }

                var countSizeRefArg = serializeCollectionAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "CountSizeReference");
                if (countSizeRefArg.Key != null && countSizeRefArg.Value.Value is string)
                {
                    setCount++;
                }

                var unlimitedArg = serializeCollectionAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "Unlimited");
                if (unlimitedArg.Key != null && unlimitedArg.Value.Value is bool unlimited && unlimited)
                {
                    setCount++;
                }

                if (setCount > 1)
                {
                    var diagnostic = Diagnostic.Create(Rule, serializeCollectionAttribute.ApplicationSyntaxReference!.GetSyntax().GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
