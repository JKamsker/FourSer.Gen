#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InvalidPolymorphicTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0009";

        private static readonly LocalizableString Title = "Invalid polymorphic option type";
        private static readonly LocalizableString MessageFormat = "The type '{0}' specified in [PolymorphicOption] is not assignable to the member's type '{1}'";
        private static readonly LocalizableString Description = "The type specified in a [PolymorphicOption] attribute must be assignable to the type of the member it decorates.";
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

            if (polymorphicOptionAttributes.Count == 0)
            {
                return;
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

            if (memberType == null)
            {
                return;
            }

            foreach (var attribute in polymorphicOptionAttributes)
            {
                if (attribute.ConstructorArguments.Length < 2) continue;

                if (attribute.ConstructorArguments[1].Value is ITypeSymbol attributeType)
                {
                    if (!IsAssignable(attributeType, memberType))
                    {
                        var diagnostic = Diagnostic.Create(Rule, attribute.ApplicationSyntaxReference!.GetSyntax().GetLocation(), attributeType.Name, memberType.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool IsAssignable(ITypeSymbol fromType, ITypeSymbol toType)
        {
            if (SymbolEqualityComparer.Default.Equals(fromType, toType))
            {
                return true;
            }

            if (toType.TypeKind == TypeKind.Interface)
            {
                return fromType.AllInterfaces.Contains(toType, SymbolEqualityComparer.Default);
            }

            var baseType = fromType.BaseType;
            while (baseType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(baseType, toType))
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }

            return false;
        }
    }
}
