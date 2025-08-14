using FourSer.Gen.Helpers;
using FourSer.Gen.Models;
using System;
using System.Text;

namespace FourSer.Gen.CodeGenerators.Core
{
    public static class PolymorphicUtilities
    {
        /// <summary>
        /// Formats the key for a polymorphic switch case.
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

        public static string GenerateWriteTypeIdCode(PolymorphicOption option, PolymorphicInfo info, string indent = "                ")
        {
            var key = FormatTypeIdKey(option.Key, info);
            var underlyingType = info.EnumUnderlyingType ?? info.TypeIdType;
            var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(underlyingType);
            return $"{indent}FourSer.Gen.Helpers.SpanWriterHelpers.Write{typeIdTypeName}(ref data, ({underlyingType}){key});";
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
        public static string GenerateTypeIdVariable(StringBuilder sb, PolymorphicInfo info, string? typeIdProperty, bool isDeserialization, string indent = "        ")
        {
            if (!string.IsNullOrEmpty(typeIdProperty))
            {
                return $"obj.{typeIdProperty}";
            }

            if (isDeserialization)
            {
                var typeIdTypeName = GeneratorUtilities.GetMethodFriendlyTypeName(info.EnumUnderlyingType ?? info.TypeIdType);
                sb.AppendLine($"{indent}var typeId = FourSer.Gen.Helpers.RoSpanReaderHelpers.Read{typeIdTypeName}(ref data);");
                return "typeId";
            }

            // For serialization and size calculation, the type ID is handled differently (usually inside the switch cases based on the object type).
            // This method is primarily for deserialization when the ID is external.
            return string.Empty;
        }

        /// <summary>
        /// Generates a complete switch statement for polymorphic types.
        /// </summary>
        /// <param name="sb">The string builder to append the code to.</param>
        /// <param name="info">The polymorphic information for the member.</param>
        /// <param name="switchVariable">The variable or property to switch on.</param>
        /// <param name="caseHandler">An action that generates the code inside each case block.
        /// The action receives the polymorphic option and the formatted key.</param>
        /// <param name="defaultCaseHandler">An action that generates the code for the default case.</param>
        public static void GeneratePolymorphicSwitch(
            StringBuilder sb,
            PolymorphicInfo info,
            string switchVariable,
            Action<PolymorphicOption, string> caseHandler,
            Action defaultCaseHandler,
            string indent = "        ")
        {
            sb.AppendLine($"{indent}switch (({info.TypeIdType}){switchVariable})");
            sb.AppendLine($"{indent}{{");

            foreach (var option in info.Options)
            {
                var key = FormatTypeIdKey(option.Key, info);
                sb.AppendLine($"{indent}    case {key}:");
                caseHandler(option, key);
            }

            sb.AppendLine($"{indent}    default:");
            defaultCaseHandler();
            sb.AppendLine($"{indent}}}");
        }
    }
}
