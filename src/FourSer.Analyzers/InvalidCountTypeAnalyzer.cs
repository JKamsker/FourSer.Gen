#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InvalidCountTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0012";

        private static readonly LocalizableString Title = "Invalid CountType";
        private static readonly LocalizableString MessageFormat = "The type '{0}' specified in CountType is not a valid integer type.";
        private static readonly LocalizableString Description = "The type provided to the CountType property of the [SerializeCollection] attribute must be an integer type.";
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

            var serializeCollectionAttribute = symbol.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializeCollectionAttribute");

            if (serializeCollectionAttribute == null)
            {
                return;
            }

            var countTypeArgument = serializeCollectionAttribute.NamedArguments
                .FirstOrDefault(arg => arg.Key == "CountType");

            if (countTypeArgument.Key == null)
            {
                return;
            }

            if (countTypeArgument.Value.Value is ITypeSymbol countType)
            {
                if (!IsIntegerType(countType))
                {
                    var diagnostic = Diagnostic.Create(Rule, serializeCollectionAttribute.ApplicationSyntaxReference!.GetSyntax().GetLocation(), countType.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static bool IsIntegerType(ITypeSymbol type)
        {
            return type.SpecialType switch
            {
                SpecialType.System_Byte => true,
                SpecialType.System_SByte => true,
                SpecialType.System_Int16 => true,
                SpecialType.System_UInt16 => true,
                SpecialType.System_Int32 => true,
                SpecialType.System_UInt32 => true,
                SpecialType.System_Int64 => true,
                SpecialType.System_UInt64 => true,
                _ => false,
            };
        }
    }
}
