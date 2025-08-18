using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IncompatibleTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FS0002";

        private static readonly LocalizableString Title = "Incompatible type for serialization";
        private static readonly LocalizableString MessageFormat = "Type '{0}' of member '{1}' is not serializable";
        private static readonly LocalizableString Description = "Properties and fields of a serializable type must also be serializable (primitive type, implement ISerializable<T>, or be a collection of serializable types).";
        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            var hasGenerateSerializerAttribute = namedTypeSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute");

            if (!hasGenerateSerializerAttribute)
            {
                return;
            }

            foreach (var member in namedTypeSymbol.GetMembers())
            {
                if (member.IsImplicitlyDeclared || member.IsStatic)
                {
                    continue;
                }

                if (member is IPropertySymbol property)
                {
                    if (!IsSerializable(property.Type))
                    {
                        var diagnostic = Diagnostic.Create(Rule, property.Locations[0], property.Type.Name, property.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                else if (member is IFieldSymbol field)
                {
                    if (!IsSerializable(field.Type))
                    {
                        var diagnostic = Diagnostic.Create(Rule, field.Locations[0], field.Type.Name, field.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool IsSerializable(ITypeSymbol type)
        {
            if (type.IsPrimitive() || type.SpecialType == SpecialType.System_String)
            {
                return true;
            }

            if (type.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == "FourSer.Contracts.GenerateSerializerAttribute"))
            {
                return true;
            }

            if (type.AllInterfaces.Any(i => i.ToDisplayString().StartsWith("FourSer.Contracts.ISerializable")))
            {
                return true;
            }

            // Check for collection types
            var ienumerable_t = type.AllInterfaces.FirstOrDefault(i => i.IsGenericType && i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType && namedType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
            {
                ienumerable_t = namedType;
            }

            if (ienumerable_t != null)
            {
                return IsSerializable(ienumerable_t.TypeArguments.First());
            }

            if (type is IArrayTypeSymbol arrayType)
            {
                return IsSerializable(arrayType.ElementType);
            }

            return false;
        }
    }

    public static class TypeSymbolExtensions
    {
        public static bool IsPrimitive(this ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Char:
                case SpecialType.System_Double:
                case SpecialType.System_Single:
                case SpecialType.System_Int16:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt16:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                    return true;
                default:
                    return false;
            }
        }
    }
}
