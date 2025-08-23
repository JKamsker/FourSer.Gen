using FourSer.Gen.Helpers;
using FourSer.Gen.Models;

namespace FourSer.Gen.CodeGenerators.Core;

public static class PolymorphicUtilities
{
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

    public static void GenerateWriteTypeIdCode
    (
        IndentedStringBuilder sb,
        PolymorphicOption option,
        PolymorphicInfo info,
        string target = "data",
        string helper = "SpanWriter"
    )
    {
        var key = FormatTypeIdKey(option.Key, info);
        var underlyingType = info.EnumUnderlyingType ?? info.TypeIdType;
        var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(underlyingType);
        var refOrEmpty = target == "data" ? "ref " : "";
        sb.WriteLineFormat
        (
            "{0}.Write{1}({2}{3}, ({4}){5});",
            helper,
            typeIdTypeName,
            refOrEmpty,
            target,
            underlyingType,
            key
        );
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
            sb.WriteLine($"var typeId = SpanReader.Read{typeIdTypeName}(ref data);");
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

    public static void GenerateTypeIdPrePass(IndentedStringBuilder sb, TypeToGenerate typeToGenerate)
    {
        for (var i = 0; i < typeToGenerate.Members.Count; i++)
        {
            var member = typeToGenerate.Members[i];
            if (member.PolymorphicInfo is not { TypeIdPropertyIndex: not null } info)
            {
                continue;
            }

            if ((member.IsList || member.IsCollection) &&
                member.CollectionInfo?.PolymorphicMode == PolymorphicMode.SingleTypeId)
            {
                continue;
            }

            var referencedMember = typeToGenerate.Members[info.TypeIdPropertyIndex.Value];

            sb.WriteLineFormat("switch (obj.{0})", member.Name);
            using var __ = sb.BeginBlock();
            foreach (var option in info.Options)
            {
                var typeName = TypeHelper.GetSimpleTypeName(option.Type);
                var key = option.Key.ToString();
                if (info.EnumUnderlyingType is not null)
                {
                    key = $"({info.TypeIdType}){key}";
                }
                else if (info.TypeIdType.EndsWith("Enum"))
                {
                    key = $"{info.TypeIdType}.{key}";
                }

                sb.WriteLineFormat("case {0}:", typeName);
                sb.WriteLineFormat("    obj.{0} = {1};", referencedMember.Name, key);
                sb.WriteLine("    break;");
            }

            sb.WriteLine("case null:");
            sb.WriteLine("    break;");
        }
    }
}