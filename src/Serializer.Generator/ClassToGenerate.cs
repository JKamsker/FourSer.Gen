using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Serializer.Generator;

public class ClassToGenerate
{
    public string Name { get; }
    public string Namespace { get; }
    public List<ISymbol> Members { get; }
    public bool IsValueType { get; }

    public ClassToGenerate(string name, string ns, List<ISymbol> members, bool isValueType)
    {
        Name = name;
        Namespace = ns;
        Members = members;
        IsValueType = isValueType;
    }
}