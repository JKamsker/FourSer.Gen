using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Serializer.Generator;

public class ClassToGenerate
{
    public string Name { get; }
    public string Namespace { get; }
    public List<IPropertySymbol> Members { get; }

    public ClassToGenerate(string name, string ns, List<IPropertySymbol> members)
    {
        Name = name;
        Namespace = ns;
        Members = members;
    }
}