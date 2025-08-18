#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CountSizeReferenceTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0007";

        private static readonly LocalizableString Title = "Invalid CountSizeReference property type";
        private static readonly LocalizableString MessageFormat = "The property '{0}' specified in CountSizeReference must be an integer type, but its type is '{1}'";
        private static readonly LocalizableString Description = "The property specified in the CountSizeReference argument of the SerializeCollection attribute must be of an integer type.";
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

                var countSizeReferenceArgument = serializeCollectionAttribute.NamedArguments
                    .FirstOrDefault(arg => arg.Key == "CountSizeReference");

                if (countSizeReferenceArgument.Key == null)
                {
                    return;
                }

                var referencedPropertyName = countSizeReferenceArgument.Value.Value as string;
                if (string.IsNullOrEmpty(referencedPropertyName))
                {
                    return;
                }

                var containingType = symbol.ContainingType;
                var referencedMember = containingType.GetMembers(referencedPropertyName).FirstOrDefault();

                if (referencedMember == null)
                {
                    // This is handled by the CountSizeReferenceExistenceAnalyzer.
                    return;
                }

                ITypeSymbol? referencedType = null;
                if (referencedMember is IPropertySymbol propertySymbol)
                {
                    referencedType = propertySymbol.Type;
                }
                else if (referencedMember is IFieldSymbol fieldSymbol)
                {
                    referencedType = fieldSymbol.Type;
                }

                if (referencedType != null && !IsIntegerType(referencedType))
                {
                    var location = AnalyzerHelper.GetNamedArgumentLocation(serializeCollectionAttribute, "CountSizeReference") ?? referencedMember.Locations[0];
                    var diagnostic = Diagnostic.Create(Rule, location, referencedPropertyName, referencedType.Name);
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
