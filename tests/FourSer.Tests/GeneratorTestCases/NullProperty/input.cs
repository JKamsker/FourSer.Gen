using FourSer.Contracts;
using System.Collections.Generic;

namespace FourSer.Tests.GeneratorTestCases.NullProperty;

[GenerateSerializer]
public partial class NestedObject
{
    public int Id { get; set; }
}

[GenerateSerializer]
public partial class MainObject
{
    public NestedObject Nested { get; set; }
}
