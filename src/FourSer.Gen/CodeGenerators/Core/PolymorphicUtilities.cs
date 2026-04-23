using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using System.Collections.Immutable;

namespace FourSer.Gen.CodeGenerators.Core;

public static class PolymorphicUtilities
{
    public static bool TryGetDefaultOption(ImmutableArray<PolymorphicOption> options, out PolymorphicOption option)
    {
        if (options.IsDefaultOrEmpty)
        {
            option = default;
            return false;
        }

        var defaultOption = default(PolymorphicOption);
        var defaultCount = 0;
        foreach (var candidate in options)
        {
            if (!candidate.IsDefault)
            {
                continue;
            }

            defaultOption = candidate;
            defaultCount++;
            if (defaultCount > 1)
            {
                break;
            }
        }

        option = defaultCount == 1
            ? defaultOption
            : options[0];

        return true;
    }

    public static bool TryGetDefaultOption(PolymorphicInfo info, out PolymorphicOption option)
    {
        return TryGetDefaultOption(info.Options.Array, out option);
    }

    public static void EmitFirstCollectionItemAccess(
        IndentedStringBuilder sb,
        MemberToGenerate member,
        string collectionAccessExpression,
        string firstItemVariableName)
    {
        if (member.CollectionTypeInfo?.SupportsIndexing == true || member.IsList)
        {
            sb.WriteLine($"var {firstItemVariableName} = {collectionAccessExpression}[0];");
            return;
        }

        var elementTypeName = member.ListTypeArgument?.TypeName ?? member.CollectionTypeInfo?.ElementTypeName
            ?? throw new InvalidOperationException("Polymorphic collection members require element type information.");
        var enumeratorVariableName = $"{firstItemVariableName}Enumerator";
        sb.WriteLine
        (
            $"using var {enumeratorVariableName} = ((global::System.Collections.Generic.IEnumerable<{elementTypeName}>){collectionAccessExpression}).GetEnumerator();"
        );
        sb.WriteLine($"if (!{enumeratorVariableName}.MoveNext())");
        using (sb.BeginBlock())
        {
            sb.WriteLine("throw new System.InvalidOperationException(\"Collection must contain at least one item.\");");
        }

        sb.WriteLine($"var {firstItemVariableName} = {enumeratorVariableName}.Current;");
    }

    /// <summary>
    ///     Formats the key for a polymorphic switch case.
    /// </summary>
    public static string FormatTypeIdKey(object key, PolymorphicInfo info)
    {
        if (info.EnumUnderlyingType is not null)
        {
            return $"({info.TypeIdType}){key}";
        }

        if (info.TypeIdType.EndsWith("Enum")) // A bit of a heuristic, but covers named enums
        {
            return $"{info.TypeIdType}.{key}";
        }

        return key.ToString();
    }

    public static string FormatOptionTypePattern(PolymorphicOption option)
    {
        return $"{TypeHelper.GetGlobalTypeName(option.Type)} _";
    }

    public static string FormatTypedTypeIdValue(object key, PolymorphicInfo info, string targetTypeName)
    {
        return $"({targetTypeName})({FormatTypeIdKey(key, info)})";
    }

    public static string GenerateTypeIdSizeExpression(PolymorphicInfo info)
    {
        var underlyingType = info.EnumUnderlyingType ?? info.TypeIdType;
        return $"sizeof({underlyingType})";
    }

    /// <summary>
    // / Generates the code to get the Type ID value for a switch statement.
    /// If the TypeIdProperty is specified, it uses that. Otherwise, it reads the ID from the data stream.
    /// </summary>
    public static string GenerateTypeIdVariable
        (IndentedStringBuilder sb, PolymorphicInfo info, string? typeIdProperty, bool isDeserialization)
    {
        if (!string.IsNullOrEmpty(typeIdProperty))
        {
            return $"obj.{typeIdProperty}";
        }

        if (isDeserialization)
        {
            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(info.EnumUnderlyingType ?? info.TypeIdType);
            sb.WriteLine($"var typeId = global::FourSer.Gen.Helpers.RoSpanReaderHelpers.Read{typeIdTypeName}(ref data);");
            return "typeId";
        }

        // For serialization and size calculation, the type ID is handled differently (usually inside the switch cases based on the object type).
        // This method is primarily for deserialization when the ID is external.
        return string.Empty;
    }

    /// <summary>
    ///     Generates a complete switch statement for polymorphic types.
    /// </summary>
    /// <param name="sb">The string builder to append the code to.</param>
    /// <param name="info">The polymorphic information for the member.</param>
    /// <param name="switchVariable">The variable or property to switch on.</param>
    /// <param name="caseHandler">
    ///     An action that generates the code inside each case block.
    ///     The action receives the polymorphic option and the formatted key.
    /// </param>
    /// <param name="defaultCaseHandler">An action that generates the code for the default case.</param>
    public static void GeneratePolymorphicSwitch
    (
        IndentedStringBuilder sb,
        PolymorphicInfo info,
        string switchVariable,
        Action<PolymorphicOption, string> caseHandler,
        Action defaultCaseHandler
    )
    {
        sb.WriteLine($"switch (({info.TypeIdType}){switchVariable})");

        using var _ = sb.BeginBlock();
        foreach (var option in info.Options)
        {
            var key = FormatTypeIdKey(option.Key, info);
            sb.WriteLine($"case {key}:");
            caseHandler(option, key);
        }

        sb.WriteLine("default:");
        defaultCaseHandler();
    }
}
