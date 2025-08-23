using System.Collections.Generic;
using FourSer.Contracts;

namespace FourSer.Tests.GeneratorTestCases.EnumerableWithCountSizeReference;

[GenerateSerializer]
public partial class EnumerableOfReferenceTypes
{
    public int Count { get; set; }

    [SerializeCollection(CountSizeReference = nameof(Count))]
    public IEnumerable<Entity> MyList { get; set; }
}

[GenerateSerializer]
public partial class Entity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
