#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InvalidSerializeCollectionTargetAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0005";

        private static readonly LocalizableString Title = "Invalid SerializeCollection attribute target";
        private static readonly LocalizableString MessageFormat = "The [SerializeCollection] attribute can only be applied to collection types, but it is used on type '{0}'";
        private static readonly LocalizableString Description = "The [SerializeCollection] attribute is intended for collection types only. Applying it to non-collection types can lead to unexpected behavior.";
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

                ITypeSymbol? memberType = null;
                if (symbol is IPropertySymbol propertySymbol)
                {
                    memberType = propertySymbol.Type;
                }
                else if (symbol is IFieldSymbol fieldSymbol)
                {
                    memberType = fieldSymbol.Type;
                }

                if (memberType != null && !IsCollectionType(memberType, context.Compilation))
                {
                    var diagnostic = Diagnostic.Create(Rule, symbol.Locations[0], memberType.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static bool IsCollectionType(ITypeSymbol type, Compilation compilation)
        {
            if (type.SpecialType == SpecialType.System_String)
            {
                return false;
            }

            var ienumerableType = compilation.GetTypeByMetadataName("System.Collections.IEnumerable");
            if (ienumerableType == null)
            {
                // Should not happen in a valid compilation
                return false;
            }

            return type.AllInterfaces.Contains(ienumerableType, SymbolEqualityComparer.Default);
        }
    }
}
