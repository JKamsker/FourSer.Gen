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
        public const string AssignabilityDiagnosticId = "FS0009";
        public const string SerializableDiagnosticId = "FS0016";

        private static readonly LocalizableString AssignabilityTitle = "Invalid polymorphic option type";
        private static readonly LocalizableString AssignabilityMessageFormat = "The type '{0}' specified in [PolymorphicOption] is not assignable to the member's type '{1}'";
        private static readonly LocalizableString AssignabilityDescription = "The type specified in a [PolymorphicOption] attribute must be assignable to the type of the member it decorates.";

        private static readonly LocalizableString SerializableTitle = "Non-serializable polymorphic option type";
        private static readonly LocalizableString SerializableMessageFormat = "The type '{0}' specified in [PolymorphicOption] must be marked with [GenerateSerializer]";
        private static readonly LocalizableString SerializableDescription = "The type specified in a [PolymorphicOption] attribute must be a serializable type.";

        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor AssignabilityRule = new DiagnosticDescriptor(
            AssignabilityDiagnosticId,
            AssignabilityTitle,
            AssignabilityMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: AssignabilityDescription);

        internal static readonly DiagnosticDescriptor SerializableRule = new DiagnosticDescriptor(
            SerializableDiagnosticId,
            SerializableTitle,
            SerializableMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: SerializableDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(AssignabilityRule, SerializableRule);

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

                var polymorphicOptionAttributes = symbol.GetAttributes()
                    .Where(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.PolymorphicOptionAttribute")
                    .ToList();

                if (polymorphicOptionAttributes.Count == 0)
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

                if (memberType == null)
                {
                    continue;
                }

                var typeToCheck = memberType;
                if (memberType is INamedTypeSymbol namedMemberTypeSymbol &&
                    namedMemberTypeSymbol.IsGenericType &&
                    namedMemberTypeSymbol.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" &&
                    namedMemberTypeSymbol.TypeArguments.Length == 1)
                {
                    typeToCheck = namedMemberTypeSymbol.TypeArguments[0];
                }
                else if (memberType is IArrayTypeSymbol arrayTypeSymbol)
                {
                    typeToCheck = arrayTypeSymbol.ElementType;
                }


                foreach (var attribute in polymorphicOptionAttributes)
                {
                    if (attribute.ConstructorArguments.Length < 2) continue;

                    if (attribute.ConstructorArguments[1].Value is ITypeSymbol attributeType)
                    {
                        // Check for assignability
                        if (!IsAssignable(attributeType, typeToCheck))
                        {
                            var diagnostic = Diagnostic.Create(AssignabilityRule, attribute.ApplicationSyntaxReference!.GetSyntax().GetLocation(), attributeType.Name, typeToCheck.Name);
                            context.ReportDiagnostic(diagnostic);
                        }

                        // Check for [GenerateSerializer]
                        var hasGenerateSerializer = attributeType.GetAttributes()
                            .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, generateSerializerAttribute));
                        if (!hasGenerateSerializer)
                        {
                            var diagnostic = Diagnostic.Create(SerializableRule, attribute.ApplicationSyntaxReference!.GetSyntax().GetLocation(), attributeType.Name);
                            context.ReportDiagnostic(diagnostic);
                        }
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
