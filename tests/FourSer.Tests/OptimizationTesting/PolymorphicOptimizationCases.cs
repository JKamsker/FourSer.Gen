namespace FourSer.Tests.OptimizationTesting;

internal static class PolymorphicOptimizationCases
{
    public static IEnumerable<OptimizationRepresentativeCase> GetCases()
    {
        yield return new OptimizationRepresentativeCase(
            "PolymorphicMember",
            "FourSer.Tests.RuntimeCases.PolymorphicMemberPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            public interface IAnimal
            {
            }

            [GenerateSerializer]
            public partial class DogAnimal : IAnimal
            {
                public string Name { get; set; } = string.Empty;
                public int BarkPitch { get; set; }
            }

            [GenerateSerializer]
            public partial class CatAnimal : IAnimal
            {
                public string Name { get; set; } = string.Empty;
                public bool HasClaws { get; set; }
            }

            [GenerateSerializer]
            public partial class PolymorphicMemberPacket
            {
                [SerializePolymorphic(TypeIdType = typeof(byte))]
                [PolymorphicOption((byte)1, typeof(DogAnimal))]
                [PolymorphicOption((byte)2, typeof(CatAnimal))]
                public IAnimal Pet { get; set; } = null!;

                public static PolymorphicMemberPacket CreateSample()
                {
                    return new PolymorphicMemberPacket
                    {
                        Pet = new DogAnimal
                        {
                            Name = "Rex",
                            BarkPitch = 6,
                        },
                    };
                }
            }
            """);

        yield return new OptimizationRepresentativeCase(
            "SingleTypeIdPolymorphicCollection",
            "FourSer.Tests.RuntimeCases.SingleTypeIdCollectionPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            public interface ICollectionAnimal
            {
            }

            [GenerateSerializer]
            public partial class CollectionDog : ICollectionAnimal
            {
                public string Name { get; set; } = string.Empty;
                public int Speed { get; set; }
            }

            [GenerateSerializer]
            public partial class CollectionCat : ICollectionAnimal
            {
                public string Name { get; set; } = string.Empty;
                public int Lives { get; set; }
            }

            [GenerateSerializer]
            public partial class SingleTypeIdCollectionPacket
            {
                [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte), CountType = typeof(byte))]
                [PolymorphicOption((byte)1, typeof(CollectionDog))]
                [PolymorphicOption((byte)2, typeof(CollectionCat), isDefault: true)]
                public IReadOnlyCollection<ICollectionAnimal> Animals { get; set; } = Array.Empty<ICollectionAnimal>();

                public static SingleTypeIdCollectionPacket CreateSample()
                {
                    return new SingleTypeIdCollectionPacket
                    {
                        Animals =
                        [
                            new CollectionDog { Name = "Bolt", Speed = 10 },
                            new CollectionDog { Name = "Dash", Speed = 12 },
                        ],
                    };
                }
            }
            """);

        yield return new OptimizationRepresentativeCase(
            "IndividualTypeIdPolymorphicCollection",
            "FourSer.Tests.RuntimeCases.IndividualTypeIdCollectionPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            public interface IListAnimal
            {
            }

            [GenerateSerializer]
            public partial class ListDog : IListAnimal
            {
                public string Name { get; set; } = string.Empty;
                public int Speed { get; set; }
            }

            [GenerateSerializer]
            public partial class ListCat : IListAnimal
            {
                public string Name { get; set; } = string.Empty;
                public int Lives { get; set; }
            }

            [GenerateSerializer]
            public partial class IndividualTypeIdCollectionPacket
            {
                [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte), CountType = typeof(byte))]
                [PolymorphicOption((byte)1, typeof(ListDog))]
                [PolymorphicOption((byte)2, typeof(ListCat))]
                public List<IListAnimal> Animals { get; set; } = new();

                public static IndividualTypeIdCollectionPacket CreateSample()
                {
                    return new IndividualTypeIdCollectionPacket
                    {
                        Animals =
                        [
                            new ListDog { Name = "Scout", Speed = 8 },
                            new ListCat { Name = "Milo", Lives = 9 },
                        ],
                    };
                }
            }
            """);
    }
}
