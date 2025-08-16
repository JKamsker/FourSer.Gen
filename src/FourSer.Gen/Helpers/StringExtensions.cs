using System;
using System.Collections.Generic;
using System.Globalization;

namespace FourSer.Gen.Helpers;

public static class StringExtensions
{
    private static readonly HashSet<string> s_keywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
        "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        // Variables used in the generator
        "buffer", "bytesRead", "originalBuffer", "obj"
    };

    public static string ToCamelCase(this string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }

        var camelCased = char.ToLowerInvariant(str[0]) + str.Substring(1);
        return s_keywords.Contains(camelCased) ? "@" + camelCased : camelCased;
    }
}
