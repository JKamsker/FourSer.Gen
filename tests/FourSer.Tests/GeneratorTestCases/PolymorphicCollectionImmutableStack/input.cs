using System.Collections.Immutable;

namespace FourSer.Tests.GeneratorTestCases.PolymorphicCollectionImmutableStack;

[GenerateSerializer]
public partial class Inventory
{
    [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte))]
    [PolymorphicOption((byte)10, typeof(Sword))]
    [PolymorphicOption((byte)20, typeof(Shield))]
    [PolymorphicOption((byte)30, typeof(Potion))]
    public ImmutableStack<IItem> Items { get; set; } = ImmutableStack<IItem>.Empty;
}

public interface IItem
{
}

[GenerateSerializer]
public partial class Sword : IItem
{
}

[GenerateSerializer]
public partial class Shield : IItem
{
}

[GenerateSerializer]
public partial class Potion : IItem
{
}
