using FourSer.Tests.OptimizationTesting;

namespace FourSer.Tests;

public class OptimizationStreamFailureTests
{
    [Fact]
    public void EarlyEofDuringBatchedStreamRead_ShouldThrow()
    {
        var testCase = new OptimizationRepresentativeCase(
            "EarlyEof",
            "FourSer.Tests.Custom.Failures.BatchedPacket",
            """
            namespace FourSer.Tests.Custom.Failures;

            [GenerateSerializer]
            public partial class BatchedPacket
            {
                public int First { get; set; }
                public int Second { get; set; }
                public int Third { get; set; }

                public static BatchedPacket CreateSample() => new() { First = 1, Second = 2, Third = 3 };
            }
            """);

        using var assembly = OptimizationTestCompiler.BuildRuntimeAssembly(testCase, OptimizationLevels.Conservative);
        var bytes = assembly.SerializeStream(assembly.CreateSample());
        Array.Resize(ref bytes, bytes.Length - 1);

        Assert.Throws<EndOfStreamException>(() => assembly.DeserializeStream(bytes));
    }

    [Fact]
    public void NonSeekableEnumerableStream_ShouldThrow()
    {
        const string rootTypeName = "FourSer.Tests.Custom.Failures.EnumerablePacket";
        const string source = """
        namespace FourSer.Tests.Custom.Failures;

        [GenerateSerializer]
        public partial class EnumerablePacket
        {
            [SerializeCollection]
            public IEnumerable<int> Values { get; set; } = YieldValues();

            public static EnumerablePacket CreateSample() => new() { Values = YieldValues() };

            private static IEnumerable<int> YieldValues()
            {
                yield return 4;
                yield return 5;
                yield return 6;
            }
        }
        """;

        using var assembly = OptimizationTestCompiler.BuildRuntimeAssembly(
            new OptimizationRepresentativeCase("EnumerableNonSeekable", rootTypeName, source),
            OptimizationLevels.AggressivePortable);

        var packetType = OptimizationFailureTestHelpers.GetPacketType(assembly, rootTypeName);
        var packet = packetType.GetMethod("CreateSample")!.Invoke(null, null)!;

        Assert.Throws<NotSupportedException>(
            () => OptimizationFailureTestHelpers.InvokeSerializeStream(packetType, packet, new NonSeekableWriteStream()));
    }

    [Fact]
    public void LargePooledBatchWrite_ShouldPropagateExceptions()
    {
        const string rootTypeName = "FourSer.Tests.Custom.Failures.LargeBatchPacket";
        const string source = """
        namespace FourSer.Tests.Custom.Failures;

        [GenerateSerializer]
        public partial class LargeBatchPacket
        {
            public int Value01 { get; set; }
            public int Value02 { get; set; }
            public int Value03 { get; set; }
            public int Value04 { get; set; }
            public int Value05 { get; set; }
            public int Value06 { get; set; }
            public int Value07 { get; set; }
            public int Value08 { get; set; }
            public int Value09 { get; set; }
            public int Value10 { get; set; }
            public int Value11 { get; set; }
            public int Value12 { get; set; }
            public int Value13 { get; set; }
            public int Value14 { get; set; }
            public int Value15 { get; set; }
            public int Value16 { get; set; }
            public int Value17 { get; set; }
            public int Value18 { get; set; }
            public int Value19 { get; set; }
            public int Value20 { get; set; }
            public int Value21 { get; set; }
            public int Value22 { get; set; }
            public int Value23 { get; set; }
            public int Value24 { get; set; }
            public int Value25 { get; set; }
            public int Value26 { get; set; }
            public int Value27 { get; set; }
            public int Value28 { get; set; }
            public int Value29 { get; set; }
            public int Value30 { get; set; }
            public int Value31 { get; set; }
            public int Value32 { get; set; }
            public int Value33 { get; set; }
            public int Value34 { get; set; }
            public int Value35 { get; set; }
            public int Value36 { get; set; }
            public int Value37 { get; set; }
            public int Value38 { get; set; }
            public int Value39 { get; set; }
            public int Value40 { get; set; }
            public int Value41 { get; set; }
            public int Value42 { get; set; }
            public int Value43 { get; set; }
            public int Value44 { get; set; }
            public int Value45 { get; set; }
            public int Value46 { get; set; }
            public int Value47 { get; set; }
            public int Value48 { get; set; }
            public int Value49 { get; set; }
            public int Value50 { get; set; }
            public int Value51 { get; set; }
            public int Value52 { get; set; }
            public int Value53 { get; set; }
            public int Value54 { get; set; }
            public int Value55 { get; set; }
            public int Value56 { get; set; }
            public int Value57 { get; set; }
            public int Value58 { get; set; }
            public int Value59 { get; set; }
            public int Value60 { get; set; }
            public int Value61 { get; set; }
            public int Value62 { get; set; }
            public int Value63 { get; set; }
            public int Value64 { get; set; }
            public int Value65 { get; set; }

            public static LargeBatchPacket CreateSample() => new();
        }
        """;

        using var assembly = OptimizationTestCompiler.BuildRuntimeAssembly(
            new OptimizationRepresentativeCase("LargePooledBatchFailure", rootTypeName, source),
            OptimizationLevels.AggressivePortable);

        var packetType = OptimizationFailureTestHelpers.GetPacketType(assembly, rootTypeName);
        var packet = packetType.GetMethod("CreateSample")!.Invoke(null, null)!;

        Assert.Throws<IOException>(
            () => OptimizationFailureTestHelpers.InvokeSerializeStream(packetType, packet, new ThrowingWriteStream()));
    }

    private sealed class NonSeekableWriteStream : MemoryStream
    {
        public override bool CanSeek => false;
    }

    private sealed class ThrowingWriteStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Synthetic write failure.");
        public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("Synthetic write failure.");
    }
}
