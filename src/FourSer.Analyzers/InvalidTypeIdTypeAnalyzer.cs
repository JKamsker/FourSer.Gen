#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InvalidTypeIdTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0013";

        private static readonly LocalizableString Title = "Invalid TypeIdType";
        private static readonly LocalizableString MessageFormat = "The type '{0}' specified in TypeIdType is not a valid integer or enum type.";
        private static readonly LocalizableString Description = "The type provided to the TypeIdType property must be an integer or an enum type.";
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

                var attributes = symbol.GetAttributes();

                var serializePolymorphicAttribute = attributes.FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializePolymorphicAttribute");
                var serializeCollectionAttribute = attributes.FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializeCollectionAttribute");

                AttributeData? attribute = serializePolymorphicAttribute ?? serializeCollectionAttribute;

                if (attribute == null)
                {
                    continue;
                }

                var typeIdTypeArgument = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == "TypeIdType");

                if (typeIdTypeArgument.Key == null)
                {
                    continue;
                }

                if (typeIdTypeArgument.Value.Value is ITypeSymbol typeIdType)
                {
                    if (!IsValidTypeIdType(typeIdType))
                    {
                        var diagnostic = Diagnostic.Create(Rule, attribute.ApplicationSyntaxReference!.GetSyntax().GetLocation(), typeIdType.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool IsValidTypeIdType(ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Enum)
            {
                return true;
            }

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
