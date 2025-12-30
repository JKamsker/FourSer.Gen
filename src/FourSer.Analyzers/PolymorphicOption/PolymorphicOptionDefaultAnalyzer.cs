using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FourSer.Analyzers.PolymorphicOption;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PolymorphicOptionDefaultAnalyzer : DiagnosticAnalyzer
{
    public const string MultipleDefaultsDiagnosticId = "FSG3003";

    private static readonly LocalizableString Title =
        new LocalizableResourceString(nameof(Resources.PolymorphicOptionMultipleDefaults_Title), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableString MessageFormat =
        new LocalizableResourceString(nameof(Resources.PolymorphicOptionMultipleDefaults_MessageFormat), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableString Description =
        new LocalizableResourceString(nameof(Resources.PolymorphicOptionMultipleDefaults_Description), Resources.ResourceManager, typeof(Resources));

    private const string Category = "Usage";

    internal static readonly DiagnosticDescriptor MultipleDefaultsRule = new
    (
        MultipleDefaultsDiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MultipleDefaultsRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property, SymbolKind.Field);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var attributes = context.Symbol.GetAttributes().Where(ad => ad.AttributeClass?.Name == "PolymorphicOptionAttribute").ToList();

        if (attributes.Count < 2)
        {
            return;
        }

        var defaultAttributes = attributes.Where(IsDefaultOption).ToList();
        if (defaultAttributes.Count <= 1)
        {
            return;
        }

        foreach (var attribute in defaultAttributes.Skip(1))
        {
            var location = GetIsDefaultArgumentLocation(attribute, context.CancellationToken) ?? attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)?.GetLocation();
            if (location is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(MultipleDefaultsRule, location));
            }
        }
    }

    private static bool IsDefaultOption(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length > 2 &&
            attribute.ConstructorArguments[2].Value is bool ctorValue)
        {
            return ctorValue;
        }

        var namedArgument = attribute.NamedArguments.FirstOrDefault(arg => string.Equals(arg.Key, "IsDefault", StringComparison.OrdinalIgnoreCase));
        if (namedArgument.Value.Value is bool namedValue)
        {
            return namedValue;
        }

        return false;
    }

    private static Location? GetIsDefaultArgumentLocation(AttributeData attribute, CancellationToken cancellationToken)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is not AttributeSyntax attributeSyntax ||
            attributeSyntax.ArgumentList is null)
        {
            return null;
        }

        foreach (var argument in attributeSyntax.ArgumentList.Arguments)
        {
            var name = argument.NameEquals?.Name.Identifier.ValueText ?? argument.NameColon?.Name.Identifier.ValueText;
            if (string.Equals(name, "isDefault", StringComparison.OrdinalIgnoreCase))
            {
                return argument.GetLocation();
            }
        }

        var isDefaultIndex = attribute.AttributeConstructor?.Parameters
            .Select((p, i) => (Parameter: p, Index: i))
            .FirstOrDefault(p => string.Equals(p.Parameter.Name, "isDefault", StringComparison.OrdinalIgnoreCase))
            .Index;

        if (isDefaultIndex is not null && isDefaultIndex.Value < attributeSyntax.ArgumentList.Arguments.Count)
        {
            return attributeSyntax.ArgumentList.Arguments[isDefaultIndex.Value].GetLocation();
        }

        return attributeSyntax.GetLocation();
    }
}
