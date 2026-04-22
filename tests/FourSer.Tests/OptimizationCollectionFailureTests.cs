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

        Assert.Throws<ArgumentNullException>(
            () => OptimizationFailureTestHelpers.InvokeGetPacketSize(packetType, packet));

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
            () => OptimizationFailureTestHelpers.InvokeGetPacketSize(packetType, packet));

        Assert.Throws<InvalidOperationException>(
            () => OptimizationFailureTestHelpers.InvokeSerializeStream(packetType, packet, new MemoryStream()));
    }

    [Fact]
    public void RootNullEntryPoints_ShouldThrow()
    {
        const string rootTypeName = "FourSer.Tests.Custom.Failures.NullRootPacket";
        const string source = """
        namespace FourSer.Tests.Custom.Failures;

        [GenerateSerializer]
        public partial class NullRootPacket
        {
            public int Value { get; set; }

            public static NullRootPacket CreateSample() => new() { Value = 42 };
        }
        """;

        using var assembly = OptimizationTestCompiler.BuildRuntimeAssembly(
            new OptimizationRepresentativeCase("RootNull", rootTypeName, source),
            OptimizationLevels.AggressivePortable);

        var packetType = OptimizationFailureTestHelpers.GetPacketType(assembly, rootTypeName);

        Assert.Throws<ArgumentNullException>(
            () => OptimizationFailureTestHelpers.InvokeGetPacketSize(packetType, null));
        Assert.Throws<ArgumentNullException>(
            () => OptimizationFailureTestHelpers.InvokeSerializeStream(packetType, null, new MemoryStream()));
    }

    [Fact]
    public void ZeroCountPayloads_ShouldCanonicalizeNullableCollectionsAndMemoryOwnersToEmpty()
    {
        const string rootTypeName = "FourSer.Tests.Custom.Failures.ZeroCountCanonicalPacket";
        const string source = """
        namespace FourSer.Tests.Custom.Failures;

        [GenerateSerializer]
        public partial class ZeroCountCanonicalPacket
        {
            [SerializeCollection(CountType = typeof(byte))]
            public List<int>? Values { get; set; }

            [SerializeCollection(CountType = typeof(byte))]
            public IMemoryOwner<byte>? Data { get; set; }

            public static ZeroCountCanonicalPacket CreateSample() => new();
        }
        """;

        using var assembly = OptimizationTestCompiler.BuildRuntimeAssembly(
            new OptimizationRepresentativeCase("ZeroCountCanonical", rootTypeName, source),
            OptimizationLevels.AggressivePortable);

        var packetType = OptimizationFailureTestHelpers.GetPacketType(assembly, rootTypeName);
        var packet = Activator.CreateInstance(packetType)!;
        packetType.GetProperty("Values")!.SetValue(packet, null);
        packetType.GetProperty("Data")!.SetValue(packet, null);

        using var stream = new MemoryStream();
        OptimizationFailureTestHelpers.InvokeSerializeStream(packetType, packet, stream);

        var roundTripped = OptimizationFailureTestHelpers.InvokeDeserializeStream(packetType, stream.ToArray());
        try
        {
            var values = packetType.GetProperty("Values")!.GetValue(roundTripped);
            Assert.NotNull(values);
            Assert.Empty(((System.Collections.IEnumerable)values!).Cast<object>());

            var owner = packetType.GetProperty("Data")!.GetValue(roundTripped);
            Assert.NotNull(owner);

            var memory = owner!.GetType().GetProperty("Memory")!.GetValue(owner)!;
            var length = (int)memory.GetType().GetProperty("Length")!.GetValue(memory)!;
            Assert.Equal(0, length);
        }
        finally
        {
            if (roundTripped is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
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
