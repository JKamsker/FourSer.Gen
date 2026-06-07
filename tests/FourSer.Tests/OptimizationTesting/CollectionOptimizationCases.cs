namespace FourSer.Tests.OptimizationTesting;

internal static class CollectionOptimizationCases
{
    public static IEnumerable<OptimizationRepresentativeCase> GetCases()
    {
        yield return new OptimizationRepresentativeCase(
            "FixedByteArray",
            "FourSer.Tests.RuntimeCases.FixedByteArrayPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            [GenerateSerializer]
            public partial class FixedByteArrayPacket
            {
                [SerializeCollection(CountSize = 4)]
                public byte[] Data { get; set; } = new byte[4];

                public static FixedByteArrayPacket CreateSample()
                {
                    return new FixedByteArrayPacket
                    {
                        Data = [1, 2, 3, 4],
                    };
                }
            }
            """);

        yield return new OptimizationRepresentativeCase(
            "FixedIntArray",
            "FourSer.Tests.RuntimeCases.FixedIntArrayPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            [GenerateSerializer]
            public partial class FixedIntArrayPacket
            {
                [SerializeCollection(CountSize = 3)]
                public int[] Values { get; set; } = new int[3];

                public static FixedIntArrayPacket CreateSample()
                {
                    return new FixedIntArrayPacket
                    {
                        Values = [4, 5, 6],
                    };
                }
            }
            """);

        yield return new OptimizationRepresentativeCase(
            "CountedByteList",
            "FourSer.Tests.RuntimeCases.CountedByteListPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            [GenerateSerializer]
            public partial class CountedByteListPacket
            {
                [SerializeCollection(CountType = typeof(byte))]
                public List<byte> Data { get; set; } = new();

                public static CountedByteListPacket CreateSample()
                {
                    return new CountedByteListPacket
                    {
                        Data = [7, 8, 9, 10, 11],
                    };
                }
            }
            """);

        yield return new OptimizationRepresentativeCase(
            "CountedIntList",
            "FourSer.Tests.RuntimeCases.CountedIntListPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            [GenerateSerializer]
            public partial class CountedIntListPacket
            {
                [SerializeCollection(CountType = typeof(byte))]
                public List<int> Values { get; set; } = new();

                public static CountedIntListPacket CreateSample()
                {
                    return new CountedIntListPacket
                    {
                        Values = [12, 13, 14, 15],
                    };
                }
            }
            """);

        yield return new OptimizationRepresentativeCase(
            "EnumerableInt",
            "FourSer.Tests.RuntimeCases.EnumerableIntPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            [GenerateSerializer]
            public partial class EnumerableIntPacket
            {
                [SerializeCollection]
                public IEnumerable<int> Values { get; set; } = Array.Empty<int>();

                public static EnumerableIntPacket CreateSample()
                {
                    return new EnumerableIntPacket
                    {
                        Values = YieldValues(),
                    };
                }

                private static IEnumerable<int> YieldValues()
                {
                    yield return 16;
                    yield return 17;
                    yield return 18;
                }
            }
            """);

        yield return new OptimizationRepresentativeCase(
            "MemoryOwnerByte",
            "FourSer.Tests.RuntimeCases.MemoryOwnerBytePacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            [GenerateSerializer]
            public partial class MemoryOwnerBytePacket
            {
                [SerializeCollection(CountType = typeof(byte))]
                public IMemoryOwner<byte> Data { get; set; } = MemoryPool<byte>.Shared.Rent(0).SliceToSize(0);

                public static MemoryOwnerBytePacket CreateSample()
                {
                    var owner = MemoryPool<byte>.Shared.Rent(4).SliceToSize(4);
                    owner.Memory.Span[0] = 21;
                    owner.Memory.Span[1] = 22;
                    owner.Memory.Span[2] = 23;
                    owner.Memory.Span[3] = 24;
                    return new MemoryOwnerBytePacket { Data = owner };
                }
            }
            """);

        yield return new OptimizationRepresentativeCase(
            "MemoryOwnerInt",
            "FourSer.Tests.RuntimeCases.MemoryOwnerIntPacket",
            """
            namespace FourSer.Tests.RuntimeCases;

            [GenerateSerializer]
            public partial class MemoryOwnerIntPacket
            {
                [SerializeCollection(CountType = typeof(byte))]
                public IMemoryOwner<int> Data { get; set; } = MemoryPool<int>.Shared.Rent(0).SliceToSize(0);

                public static MemoryOwnerIntPacket CreateSample()
                {
                    var owner = MemoryPool<int>.Shared.Rent(3).SliceToSize(3);
                    owner.Memory.Span[0] = 31;
                    owner.Memory.Span[1] = 32;
                    owner.Memory.Span[2] = 33;
                    return new MemoryOwnerIntPacket { Data = owner };
                }
            }
            """);
    }
}
