using FourSer.Tests.OptimizationTesting;

namespace FourSer.Tests;

public class OptimizationSourceShapeTests
{
    [Fact]
    public void Off_ShouldDisablePortableCollectionFastPaths()
    {
        const string source = """
        namespace FourSer.Tests.Custom.SourceShape;

        [GenerateSerializer]
        public partial class IntListPacket
        {
            [SerializeCollection]
            public List<int> Values { get; set; } = new();
        }
        """;

        var generatedSource = OptimizationTestCompiler.Generate(
            source,
            OptimizationTestCompiler.CreateOptions(OptimizationLevels.Off)).GeneratedSource;

        Assert.DoesNotContain("CollectionsMarshal.SetCount", generatedSource);
        Assert.DoesNotContain("MemoryMarshal.AsBytes(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(obj.Values))", generatedSource);
    }

    [Fact]
    public void AggressivePortable_ShouldUseListFastPath()
    {
        const string source = """
        namespace FourSer.Tests.Custom.SourceShape;

        [GenerateSerializer]
        public partial class IntListPacket
        {
            [SerializeCollection]
            public List<int> Values { get; set; } = new();
        }
        """;

        var generatedSource = OptimizationTestCompiler.Generate(
            source,
            OptimizationTestCompiler.CreateOptions(OptimizationLevels.AggressivePortable)).GeneratedSource;

        Assert.Contains("CollectionsMarshal.SetCount", generatedSource);
        Assert.Contains("MemoryMarshal.AsBytes(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(obj.Values))", generatedSource);
        Assert.Contains("if (global::System.BitConverter.IsLittleEndian)", generatedSource);
    }

    [Fact]
    public void AggressiveNativeLayout_ShouldEmitRuntimeGuardedFallback()
    {
        const string source = """
        namespace FourSer.Tests.Custom.SourceShape;

        [GenerateSerializer]
        public partial class IntArrayPacket
        {
            [SerializeCollection]
            public int[] Values { get; set; } = System.Array.Empty<int>();
        }
        """;

        var generatedSource = OptimizationTestCompiler.Generate(
            source,
            OptimizationTestCompiler.CreateOptions(OptimizationLevels.AggressiveNativeLayout)).GeneratedSource;

        Assert.Contains("RuntimeHelpers.IsReferenceOrContainsReferences<int>()", generatedSource);
        Assert.Contains("if (global::System.BitConverter.IsLittleEndian && !global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<int>())", generatedSource);
        Assert.Contains("else", generatedSource);
    }

    [Fact]
    public void LargeStreamBatch_ShouldProtectPooledBufferLifetime()
    {
        const string source = """
        namespace FourSer.Tests.Custom.SourceShape;

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
        }
        """;

        var generatedSource = OptimizationTestCompiler.Generate(source).GeneratedSource;

        Assert.Contains("ArrayPool<byte>.Shared.Rent", generatedSource);
        Assert.Contains("try", generatedSource);
        Assert.Contains("finally", generatedSource);
        Assert.Contains("ArrayPool<byte>.Shared.Return", generatedSource);
    }
}
