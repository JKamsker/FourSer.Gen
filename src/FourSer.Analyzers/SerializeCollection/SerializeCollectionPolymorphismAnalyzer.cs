using System.Collections.Immutable;
using FourSer.Analyzers.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.SerializeCollection
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SerializeCollectionPolymorphismAnalyzer : DiagnosticAnalyzer
    {
        public const string TypeIdTypeMismatchDiagnosticId = "FSG1010";
        public const string IndividualTypeIdsWithTypeIdPropertyDiagnosticId = "FSG1011";
        public const string ConflictingPolymorphicSettingsDiagnosticId = "FSG1013";
        public const string TypeIdTypePolymorphicOptionTypeMismatchDiagnosticId = "FSG1012";

        private static readonly LocalizableString Title = "Invalid polymorphic settings";

        private static readonly LocalizableString TypeIdTypeMismatchMessageFormat =
            "'TypeIdType' must match the type of the property specified in 'TypeIdProperty'";

        private static readonly LocalizableString IndividualTypeIdsWithTypeIdPropertyMessageFormat =
            "'TypeIdProperty' cannot be set when 'PolymorphicMode' is 'IndividualTypeIds'";

        private static readonly LocalizableString ConflictingPolymorphicSettingsMessageFormat =
            "Conflicting polymorphic settings on '[SerializeCollection]' and '[SerializePolymorphic]'";

        private static readonly LocalizableString TypeIdTypePolymorphicOptionTypeMismatchMessageFormat =
            "'TypeIdProperty' type must match the type of the first '[PolymorphicOption]' id type";

        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor TypeIdTypeMismatchRule = new DiagnosticDescriptor
        (
            TypeIdTypeMismatchDiagnosticId,
            Title,
            TypeIdTypeMismatchMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        internal static readonly DiagnosticDescriptor IndividualTypeIdsWithTypeIdPropertyRule = new DiagnosticDescriptor
        (
            IndividualTypeIdsWithTypeIdPropertyDiagnosticId,
            Title,
            IndividualTypeIdsWithTypeIdPropertyMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        internal static readonly DiagnosticDescriptor ConflictingPolymorphicSettingsRule = new DiagnosticDescriptor
        (
            ConflictingPolymorphicSettingsDiagnosticId,
            Title,
            ConflictingPolymorphicSettingsMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        internal static readonly DiagnosticDescriptor TypeIdTypePolymorphicOptionTypeMismatchRule = new DiagnosticDescriptor
        (
            TypeIdTypePolymorphicOptionTypeMismatchDiagnosticId,
            Title,
            TypeIdTypePolymorphicOptionTypeMismatchMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create
        (
            TypeIdTypeMismatchRule,
            IndividualTypeIdsWithTypeIdPropertyRule,
            ConflictingPolymorphicSettingsRule,
            TypeIdTypePolymorphicOptionTypeMismatchRule
        );

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeAttribute, SymbolKind.Property);
            context.RegisterSymbolAction(AnalyzeAttribute, SymbolKind.Field);
        }

        private void AnalyzeAttribute(SymbolAnalysisContext context)
        {
            var symbol = context.Symbol;
            var serializeCollectionAttribute = symbol.GetAttributes()
                .FirstOrDefault(ad => ad.AttributeClass?.Name == "SerializeCollectionAttribute");

            if (serializeCollectionAttribute?.ApplicationSyntaxReference == null)
            {
                return;
            }

            if (symbol.HasIgnoreAttribute())
            {
                return;
            }

            var attributeSyntax = (AttributeSyntax?)serializeCollectionAttribute.ApplicationSyntaxReference.GetSyntax
                (context.CancellationToken);
            if (attributeSyntax == null) return;

            var arguments = attributeSyntax.ArgumentList?.Arguments ?? [];

            var namedArguments = serializeCollectionAttribute.NamedArguments.ToDictionary(na => na.Key, na => na.Value);

            var typeIdPropertyArg = arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "TypeIdProperty");
            var typeIdTypeArg = arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "TypeIdType");

            // FSG1011: TypeIdProperty cannot be set when PolymorphicMode is IndividualTypeIds
            if (namedArguments.TryGetValue("PolymorphicMode", out var polymorphicModeValue) && typeIdPropertyArg != null)
            {
                if (polymorphicModeValue.Value is int mode && mode == 2 /* IndividualTypeIds */)
                {
                    context.ReportDiagnostic
                        (Diagnostic.Create(IndividualTypeIdsWithTypeIdPropertyRule, typeIdPropertyArg.GetLocation()));
                }
            }


            if (namedArguments.TryGetValue("TypeIdProperty", out var typeIdPropertyValue))
            {
                var referenceName = typeIdPropertyValue.Value as string;
                if (string.IsNullOrEmpty(referenceName)) return;

                var referencedSymbol = symbol.ContainingType.GetMembers(referenceName!)
                    .FirstOrDefault(m => (m is IPropertySymbol or IFieldSymbol) && !m.HasIgnoreAttribute());
                if (referencedSymbol == null) return;
                var fieldOrPropertyType = referencedSymbol switch
                {
                    IPropertySymbol propertySymbol => propertySymbol.Type,
                    IFieldSymbol fieldSymbol => fieldSymbol.Type,
                    _ => null
                };

                // FSG1010
                AnalyzeTypeIdType(context, namedArguments, fieldOrPropertyType, arguments);

                // FSG1012: TypeId property type must match the type of the option type
                AnalyzeOptionsMatchPropertyType
                (
                    context,
                    symbol,
                    fieldOrPropertyType,
                    typeIdPropertyArg,
                    referencedSymbol
                );
            }

            // FSG1013: Check for conflicting polymorphic settings
            var serializePolymorphicAttribute = symbol.GetAttributes()
                .FirstOrDefault(ad => ad.AttributeClass?.Name == "SerializePolymorphicAttribute");
            if (serializePolymorphicAttribute?.ApplicationSyntaxReference != null)
            {
                AnalyzePolymorphicOptionTypeUniformness(context, serializePolymorphicAttribute, typeIdPropertyArg, typeIdTypeArg);
            }
        }

        private static void AnalyzePolymorphicOptionTypeUniformness
        (
            SymbolAnalysisContext context,
            AttributeData serializePolymorphicAttribute,
            AttributeArgumentSyntax? typeIdPropertyArg,
            AttributeArgumentSyntax? typeIdTypeArg
        )
        {
            var polyAttributeSyntax = serializePolymorphicAttribute
                .ApplicationSyntaxReference
                ?.GetSyntax(context.CancellationToken) as AttributeSyntax;
            
            if (polyAttributeSyntax == null) return;

            var polyArguments = polyAttributeSyntax.ArgumentList?.Arguments
                ?? new SeparatedSyntaxList<AttributeArgumentSyntax>();

            var polyTypeIdPropertyArg = polyArguments.FirstOrDefault
                (a => a.NameEquals?.Name.Identifier.ValueText == "PropertyName");
            
            var polyTypeIdTypeArg = polyArguments.FirstOrDefault
                (a => a.NameEquals?.Name.Identifier.ValueText == "TypeIdType");

            if ((typeIdPropertyArg != null && polyTypeIdPropertyArg != null)
                || (typeIdTypeArg != null && polyTypeIdTypeArg != null))
            {
                context.ReportDiagnostic
                    (Diagnostic.Create(ConflictingPolymorphicSettingsRule, polyAttributeSyntax.GetLocation()));
            }
        }

        // FSG1012: TypeIdProperty type must match the type of the first PolymorphicOption id type
        private static void AnalyzeOptionsMatchPropertyType
        (
            SymbolAnalysisContext context,
            ISymbol symbol,
            ITypeSymbol? fieldOrPropertyType,
            AttributeArgumentSyntax? typeIdPropertyArg,
            ISymbol referencedSymbol
        )
        {
            var optionType = GetPolymorphicOptionType(symbol);
            if (optionType == null)
            {
                return;
            }

            if (SymbolEqualityComparer.Default.Equals(optionType, fieldOrPropertyType))
            {
                return;
            }

            context.ReportDiagnostic
            (
                Diagnostic.Create
                (
                    TypeIdTypePolymorphicOptionTypeMismatchRule,
                    typeIdPropertyArg?.GetLocation()
                )
            );
            
            context.ReportDiagnostic
            (
                Diagnostic.Create
                (
                    TypeIdTypePolymorphicOptionTypeMismatchRule,
                    referencedSymbol?.GetLocation(context.CancellationToken)
                )
            );
        }

        // FSG1010: TypeIdType must match the type of the property specified in TypeIdProperty
        private static void AnalyzeTypeIdType
        (
            SymbolAnalysisContext context,
            Dictionary<string, TypedConstant> namedArguments,
            ITypeSymbol? fieldOrPropertyType,
            SeparatedSyntaxList<AttributeArgumentSyntax> arguments
        )
        {
            if (!namedArguments.TryGetValue("TypeIdType", out var typeIdTypeConstant))
            {
                return;
            }

            if (typeIdTypeConstant is not { Kind: TypedConstantKind.Type, Value: ITypeSymbol typeIdType })
            {
                return;
            }

            if (fieldOrPropertyType == null || SymbolEqualityComparer.Default.Equals(fieldOrPropertyType, typeIdType))
            {
                return;
            }

            // Find the syntax for the location of the diagnostic
            var typeIdTypeArg = arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "TypeIdType");
            if (typeIdTypeArg != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(TypeIdTypeMismatchRule, typeIdTypeArg.GetLocation()));
            }
        }

        // Gets first ``[PolymorphicOption((byte)1, typeof(CatBase))]`` id type from property or field
        private static ITypeSymbol? GetPolymorphicOptionType(ISymbol symbol)
        {
            var polymorphicOptionAttributes = symbol.GetAttributes()
                .Where(ad => ad.AttributeClass?.Name == "PolymorphicOptionAttribute");

            foreach (var attribute in polymorphicOptionAttributes)
            {
                if (attribute.ConstructorArguments.Length > 1 &&
                    attribute.ConstructorArguments[0].Type is { } type)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
