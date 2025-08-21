#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FourSer.Analyzers.Helpers
{
    internal static class AnalyzerHelper
    {
        public static Location? GetNamedArgumentLocation(AttributeData attributeData, string argumentName)
        {
            var attributeSyntax = attributeData.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
            if (attributeSyntax?.ArgumentList == null)
            {
                return null;
            }

            var argument = attributeSyntax.ArgumentList.Arguments
                .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == argumentName);

            return argument?.GetLocation();
        }
    }
}
