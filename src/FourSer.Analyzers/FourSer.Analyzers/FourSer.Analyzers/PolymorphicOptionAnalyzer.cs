#nullable enable
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PolymorphicOptionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0003";
        public const string MismatchedTypesDiagnosticId = "FS0004";

        private static readonly LocalizableString Title = "Inconsistent polymorphic option type";
        private static readonly LocalizableString MessageFormat = "The type of the polymorphic option ID '{0}' does not match the expected type '{1}'";
        private static readonly LocalizableString Description = "All polymorphic option IDs on a property must have the same type, and this type must match the one specified in the SerializePolymorphic attribute.";
        private const string Category = "Usage";

        private static readonly LocalizableString MismatchedTypesTitle = "Mismatched polymorphic option types";
        private static readonly LocalizableString MismatchedTypesMessageFormat = "Mismatched polymorphic option types: '{0}' and '{1}'";
        private static readonly LocalizableString MismatchedTypesDescription = "All polymorphic option IDs on a property must have the same type.";


        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);
        internal static readonly DiagnosticDescriptor MismatchedTypesRule = new DiagnosticDescriptor(MismatchedTypesDiagnosticId, MismatchedTypesTitle, MismatchedTypesMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: MismatchedTypesDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, MismatchedTypesRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property, SymbolKind.Field);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var symbol = context.Symbol;

            var serializePolymorphicAttribute = symbol.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializePolymorphicAttribute");

            if (serializePolymorphicAttribute == null)
            {
                return;
            }

            var polymorphicOptions = symbol.GetAttributes()
                .Where(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.PolymorphicOptionAttribute")
                .ToList();

            if (polymorphicOptions.Count == 0)
            {
                return;
            }

            // Determine the expected type
            ITypeSymbol? expectedType = null;
            var typeIdTypeArgument = serializePolymorphicAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "TypeIdType").Value;
            if (typeIdTypeArgument.Value is ITypeSymbol typeSymbol)
            {
                expectedType = typeSymbol;
            }
            else if (serializePolymorphicAttribute.ConstructorArguments.Any())
            {
                var propertyName = serializePolymorphicAttribute.ConstructorArguments.FirstOrDefault().Value as string;
                if (!string.IsNullOrEmpty(propertyName))
                {
                    var containingType = symbol.ContainingType;
                    var property = containingType.GetMembers(propertyName).FirstOrDefault();
                    if (property is IPropertySymbol propertySymbol)
                    {
                        expectedType = propertySymbol.Type;
                    }
                    else if (property is IFieldSymbol fieldSymbol)
                    {
                        expectedType = fieldSymbol.Type;
                    }
                }
            }

            if (expectedType == null)
            {
                // Default to int if not specified
                expectedType = context.Compilation.GetSpecialType(SpecialType.System_Int32);
            }


            ITypeSymbol? firstOptionType = null;

            foreach (var option in polymorphicOptions)
            {
                if (option.ConstructorArguments.Length > 0)
                {
                    var optionId = option.ConstructorArguments[0];
                    var currentOptionType = optionId.Type;

                    if (currentOptionType == null) continue;

                    if (firstOptionType == null)
                    {
                        firstOptionType = currentOptionType;
                    }
                    else if (!SymbolEqualityComparer.Default.Equals(firstOptionType, currentOptionType))
                    {
                        var diagnostic = Diagnostic.Create(MismatchedTypesRule, option.ApplicationSyntaxReference!.GetSyntax().GetLocation(), symbol.Name, firstOptionType.Name, currentOptionType.Name);
                        context.ReportDiagnostic(diagnostic);
                        return; // Stop after finding the first mismatch
                    }
                }
            }

            if (firstOptionType != null && !SymbolEqualityComparer.Default.Equals(firstOptionType, expectedType))
            {
                 var diagnostic = Diagnostic.Create(Rule, serializePolymorphicAttribute.ApplicationSyntaxReference!.GetSyntax().GetLocation(), firstOptionType.Name, expectedType.Name);
                 context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
