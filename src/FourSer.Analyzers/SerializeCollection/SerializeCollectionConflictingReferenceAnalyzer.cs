// <copyright file="SerializeCollectionConflictingReferenceAnalyzer.cs" company="Four serpentine">
// Copyright (c) Four serpentine. All rights reserved.
// </copyright>

namespace FourSer.Analyzers.SerializeCollection
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using FourSer.Analyzers.Helpers;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;

    /// <summary>
    /// An analyzer to prevent a property from being used for multiple different kinds of references.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SerializeCollectionConflictingReferenceAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic identifier.
        /// </summary>
        public const string DiagnosticId = "FSER009";

        private const string Category = "Serialization";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.FSER009_Title), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.FSER009_MessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.FSER009_Description), Resources.ResourceManager, typeof(Resources));

        internal static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
            var referenceUsage = new Dictionary<string, (string ReferenceType, Location Location)>();

            foreach (var member in namedTypeSymbol.GetMembers())
            {
                var attribute = member.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.ToDisplayString() == "FourSer.Contracts.SerializeCollectionAttribute");

                if (attribute == null)
                {
                    continue;
                }

                CheckAndReport(context, attribute, "CountSizeReference", referenceUsage);
                CheckAndReport(context, attribute, "TypeIdProperty", referenceUsage);
            }
        }

        private static void CheckAndReport(SymbolAnalysisContext context, AttributeData attribute, string referenceTypeName, Dictionary<string, (string ReferenceType, Location Location)> referenceUsage)
        {
            var argument = attribute.NamedArguments.FirstOrDefault(x => x.Key == referenceTypeName);
            if (argument.Key == null)
            {
                return;
            }

            if (argument.Value.Value is not string propertyName || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) as Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax;
            var argumentSyntax = attributeSyntax?.ArgumentList?.Arguments
                .FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == referenceTypeName);

            var location = argumentSyntax?.GetLocation() ?? attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;

            if (referenceUsage.TryGetValue(propertyName, out var usage))
            {
                var diagnostic = Diagnostic.Create(Rule, location, propertyName, usage.ReferenceType, referenceTypeName);
                context.ReportDiagnostic(diagnostic);
            }
            else
            {
                referenceUsage[propertyName] = (referenceTypeName, location);
            }
        }
    }
}
