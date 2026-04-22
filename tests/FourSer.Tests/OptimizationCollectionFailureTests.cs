using FourSer.Tests.OptimizationTesting;

namespace FourSer.Tests;

public class OptimizationCollectionFailureTests
{
    [Fact]
    public void FixedCollectionNull_ShouldThrow()
    {
        const string rootTypeName = "FourSer.Tests.Custom.Failures.FixedPacket";
        const string source = """
        namespace FourSer.Tests.Custom.Failures;

        [GenerateSerializer]
        public partial class FixedPacket
        {
            [SerializeCollection(CountSize = 4)]
            public byte[]? Data { get; set; }

            public static FixedPacket CreateSample() => new() { Data = [1, 2, 3, 4] };
        }
        """;

        using var assembly = OptimizationTestCompiler.BuildRuntimeAssembly(
            new OptimizationRepresentativeCase("FixedNull", rootTypeName, source),
            OptimizationLevels.AggressivePortable);

        var packetType = OptimizationFailureTestHelpers.GetPacketType(assembly, rootTypeName);
        var packet = Activator.CreateInstance(packetType)!;
        packetType.GetProperty("Data")!.SetValue(packet, null);

        var exception = Assert.Throws<ArgumentNullException>(
            () => OptimizationFailureTestHelpers.InvokeSerializeStream(packetType, packet, new MemoryStream()));
        Assert.Contains("Data", exception.ParamName, StringComparison.Ordinal);
    }

    [Fact]
    public void FixedCollectionWrongSize_ShouldThrow()
    {
        const string rootTypeName = "FourSer.Tests.Custom.Failures.FixedPacket";
        const string source = """
        namespace FourSer.Tests.Custom.Failures;

        [GenerateSerializer]
        public partial class FixedPacket
        {
            [SerializeCollection(CountSize = 4)]
            public byte[] Data { get; set; } = new byte[4];

            public static FixedPacket CreateSample() => new() { Data = [1, 2, 3, 4] };
        }
        """;

        using var assembly = OptimizationTestCompiler.BuildRuntimeAssembly(
            new OptimizationRepresentativeCase("FixedWrongSize", rootTypeName, source),
            OptimizationLevels.AggressivePortable);

        var packetType = OptimizationFailureTestHelpers.GetPacketType(assembly, rootTypeName);
        var packet = Activator.CreateInstance(packetType)!;
        packetType.GetProperty("Data")!.SetValue(packet, new byte[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(
            () => OptimizationFailureTestHelpers.InvokeSerializeStream(packetType, packet, new MemoryStream()));
    }

    [Fact]
    public void NegativeAndOverflowingCounts_ShouldThrow()
    {
        var negativeCase = new OptimizationRepresentativeCase(
            "NegativeCount",
            "FourSer.Tests.Custom.Failures.NegativeCountPacket",
            """
            namespace FourSer.Tests.Custom.Failures;

            [GenerateSerializer]
            public partial class NegativeCountPacket
            {
                [SerializeCollection(CountType = typeof(int))]
                public List<int> Values { get; set; } = new();

                public static NegativeCountPacket CreateSample() => new();
            }
            """);

        using var negativeAssembly = OptimizationTestCompiler.BuildRuntimeAssembly(
            negativeCase,
            OptimizationLevels.AggressivePortable);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => negativeAssembly.DeserializeSpan(BitConverter.GetBytes(-1)));

        var overflowCase = new OptimizationRepresentativeCase(
            "OverflowCount",
            "FourSer.Tests.Custom.Failures.OverflowCountPacket",
            """
            namespace FourSer.Tests.Custom.Failures;

            [GenerateSerializer]
            public partial class OverflowCountPacket
            {
                [SerializeCollection(CountType = typeof(ulong))]
                public List<int> Values { get; set; } = new();

                public static OverflowCountPacket CreateSample() => new();
            }
            """);

        using var overflowAssembly = OptimizationTestCompiler.BuildRuntimeAssembly(
            overflowCase,
            OptimizationLevels.AggressivePortable);
        Assert.Throws<OverflowException>(
            () => overflowAssembly.DeserializeStream(BitConverter.GetBytes(ulong.MaxValue)));
    }

    [Fact]
    public void UnknownPolymorphicDiscriminator_ShouldThrow()
    {
        var testCase = new OptimizationRepresentativeCase(
            "TamperedPolymorphic",
            "FourSer.Tests.Custom.Failures.PolymorphicPacket",
            """
            namespace FourSer.Tests.Custom.Failures;

            public interface IAnimal { }

            [GenerateSerializer]
            public partial class Dog : IAnimal
            {
                public int BarkPitch { get; set; }
            }

            [GenerateSerializer]
            public partial class Cat : IAnimal
            {
                public int Lives { get; set; }
            }

            [GenerateSerializer]
            public partial class PolymorphicPacket
            {
                [SerializePolymorphic(TypeIdType = typeof(byte))]
                [PolymorphicOption((byte)1, typeof(Dog))]
                [PolymorphicOption((byte)2, typeof(Cat))]
                public IAnimal Pet { get; set; } = null!;

                public static PolymorphicPacket CreateSample() => new() { Pet = new Dog { BarkPitch = 9 } };
            }
            """);

        using var assembly = OptimizationTestCompiler.BuildRuntimeAssembly(testCase, OptimizationLevels.AggressivePortable);
        var bytes = assembly.SerializeSpan(assembly.CreateSample());
        bytes[0] = 99;

        Assert.Throws<InvalidDataException>(() => assembly.DeserializeSpan(bytes));
        Assert.Throws<InvalidDataException>(() => assembly.DeserializeStream(bytes));
    }
}
