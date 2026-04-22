using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using FourSer.Gen;
using System.Reflection;

namespace FourSer.Tests;

public class GeneratorTests
{
    private static readonly string[] s_contractsSource = Consts.ContractsSource;

    private static readonly string[] s_extensionsSource = Consts.ExtensionsSource;

    public static IEnumerable<object[]> GetTestCases()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        var testCaseFolders = resourceNames
            .Where(name => name.Contains("GeneratorTestCases") && name.EndsWith("input.cs"))
            .Select
            (
                name =>
                {
                    var parts = name.Split('.');
                    return parts[parts.Length - 3];
                }
            )
            .Distinct()
            .Where(name => name != "InvalidNestedCollection");

        foreach (var folder in testCaseFolders)
        {
            yield return new object[] { folder };
        }
    }

    [Fact]
    public void GeneratorFixtureDiscovery_ShouldIncludeRecordTypes()
    {
        var discoveredFixtures = GetTestCases()
            .Select(testCase => Assert.IsType<string>(testCase[0]))
            .ToArray();

        Assert.Contains("RecordTypes", discoveredFixtures);
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public Task RunGeneratorTest(string testCaseName)
    {
        var source = ReadSource(testCaseName);
       


        var syntaxTrees = s_contractsSource.Select(s => CSharpSyntaxTree.ParseText(s)).ToList();
        syntaxTrees.AddRange(s_extensionsSource.Select(s => CSharpSyntaxTree.ParseText(s)));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));

        var compilation = CSharpCompilation.Create
        (
            "TestProject",
            syntaxTrees,
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
        );

        var generator = new SerializerGenerator().AsSourceGenerator();
        var driver =  CSharpGeneratorDriver.Create(
            generators: new ISourceGenerator[] { generator },
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        var result = driver.GetRunResult();

        // Assert
        Assert.False(result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
        var generatedCodes = result.Results.Single()
            .GeneratedSources
            .Where(g => g.HintName.EndsWith("g.cs"))
            .Select(x => x.SourceText);

        var generatedCode = string.Join("\n\n", generatedCodes.Select(x => x.ToString()));

        return Verify(generatedCode)
                .UseDirectory(Path.Combine("GeneratorTestCases", testCaseName))
                .UseTypeName(testCaseName)
            ;
    }

    [Fact]
    public void DecimalProperty_ShouldGenerateCompilableSerializer()
    {
        const string source = """
        namespace FourSer.Tests.Custom.DecimalPacket;

        [GenerateSerializer]
        public partial class DecimalPacket
        {
            public decimal Amount { get; set; }
        }
        """;

        var compilation = CreateCompilation(AddDefaultUsings(source));
        var generator = new SerializerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();
        var finalCompilation = compilation.AddSyntaxTrees(runResult.GeneratedTrees);

        using var ms = new MemoryStream();
        var emitResult = finalCompilation.Emit(ms);

        Assert.True(emitResult.Success, $"Compilation failed with errors: {string.Join(", ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void UnlimitedCollection_ShouldNotEmitCountPrefix()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Unlimited;

        [GenerateSerializer]
        public partial class UnlimitedPacket
        {
            [SerializeCollection(Unlimited = true)]
            public List<int>? Values { get; set; }
        }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "UnlimitedPacket");

        Assert.DoesNotContain("Count size for Values", generatedCode);
        Assert.DoesNotContain("SpanWriter.WriteInt32(ref data, (int)(obj.Values.Count", generatedCode);
        Assert.DoesNotContain("StreamWriter.WriteInt32(stream, (int)(obj.Values.Count", generatedCode);
    }

    [Fact]
    public void ICollectionMembers_ShouldUseCountProperty()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Collections;

        [GenerateSerializer]
        public partial class InterfaceCollectionPacket
        {
            [SerializeCollection]
            public ICollection<int>? Numbers { get; set; }
        }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "InterfaceCollectionPacket");

        Assert.Contains("obj.Numbers?.Count ?? 0", generatedCode);
        Assert.Contains("obj.Numbers.Count", generatedCode);
        Assert.DoesNotContain("obj.Numbers?.Count() ?? 0", generatedCode);
        Assert.DoesNotContain("obj.Numbers.Count()", generatedCode);
    }

    [Fact]
    public void InterfaceBasedPolymorphicCollections_ShouldNotReportFsg0002()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Polymorphism;

        [GenerateSerializer]
        public partial class InterfaceCollectionPacket
        {
            [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId)]
            [PolymorphicOption((byte)1, typeof(Dog))]
            [PolymorphicOption((byte)2, typeof(Cat))]
            public IEnumerable<IAnimal> EnumerableAnimals { get; set; } = new List<IAnimal>();

            [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId)]
            [PolymorphicOption((byte)1, typeof(Dog))]
            [PolymorphicOption((byte)2, typeof(Cat))]
            public IReadOnlyCollection<IAnimal> ReadOnlyAnimals { get; set; } = new List<IAnimal>();
        }

        public interface IAnimal { }

        [GenerateSerializer]
        public partial class Dog : IAnimal { }

        [GenerateSerializer]
        public partial class Cat : IAnimal { }
        """;

        var compilation = CreateCompilation(AddDefaultUsings(source));
        var runResult = GetRunResult(compilation, out var finalCompilation);

        Assert.DoesNotContain(runResult.Diagnostics, d => d.Id == "FSG0002" && d.Severity == DiagnosticSeverity.Error);

        using var ms = new MemoryStream();
        var emitResult = finalCompilation.Emit(ms);
        Assert.True(emitResult.Success, $"Compilation failed with errors: {string.Join(", ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void SingleTypeIdTypeIdPropertyOnNonIndexableCollection_ShouldCompile()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Polymorphism;

        [GenerateSerializer]
        public partial class NonIndexableSingleTypeIdPacket
        {
            public byte AnimalType { get; set; }

            [SerializeCollection(
                PolymorphicMode = PolymorphicMode.SingleTypeId,
                TypeIdProperty = nameof(AnimalType),
                TypeIdType = typeof(byte))]
            [PolymorphicOption((byte)1, typeof(Dog))]
            [PolymorphicOption((byte)2, typeof(Cat), isDefault: true)]
            public IReadOnlyCollection<IAnimal> Animals { get; set; } = new List<IAnimal>();
        }

        public interface IAnimal { }

        [GenerateSerializer]
        public partial class Dog : IAnimal { }

        [GenerateSerializer]
        public partial class Cat : IAnimal { }
        """;

        var compilation = CreateCompilation(AddDefaultUsings(source));
        var runResult = GetRunResult(compilation, out var finalCompilation);

        Assert.DoesNotContain(runResult.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        using var ms = new MemoryStream();
        var emitResult = finalCompilation.Emit(ms);
        Assert.True(emitResult.Success, $"Compilation failed with errors: {string.Join(", ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void GeneratedDiscriminatorSelection_ShouldHonorIsDefault()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Polymorphism;

        [GenerateSerializer]
        public partial class DefaultedDiscriminatorPacket
        {
            public byte AnimalType { get; set; }

            [SerializeCollection(
                PolymorphicMode = PolymorphicMode.SingleTypeId,
                TypeIdProperty = nameof(AnimalType),
                TypeIdType = typeof(byte))]
            [PolymorphicOption((byte)1, typeof(Dog))]
            [PolymorphicOption((byte)2, typeof(Cat), isDefault: true)]
            public IReadOnlyCollection<IAnimal> Animals { get; set; } = new List<IAnimal>();
        }

        public interface IAnimal { }

        [GenerateSerializer]
        public partial class Dog : IAnimal { }

        [GenerateSerializer]
        public partial class Cat : IAnimal { }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "DefaultedDiscriminatorPacket");

        Assert.Contains("var animalsValidatedItems = obj.Animals is null ? null : global::System.Linq.Enumerable.ToList(obj.Animals);", generatedCode);
        Assert.Contains("if ((animalsValidatedItems?.Count ?? 0) == 0)", generatedCode);
        Assert.Contains("SpanWriter.WriteByte(ref data, (byte)(2));", generatedCode);
        Assert.DoesNotContain("SpanWriter.WriteByte(ref data, (byte)(obj.AnimalType));", generatedCode);
        Assert.Contains("StreamWriter.WriteByte(stream, (byte)(2));", generatedCode);
        Assert.DoesNotContain("StreamWriter.WriteByte(stream, (byte)(obj.AnimalType));", generatedCode);
    }

    [Fact]
    public void AdditionalImmutablePolymorphicCollections_ShouldCompile()
    {
        const string source = """
        using System.Collections.Immutable;

        namespace FourSer.Tests.Custom.Polymorphism;

        [GenerateSerializer]
        public partial class ImmutableCollectionPacket
        {
            [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte))]
            [PolymorphicOption((byte)1, typeof(Dog))]
            [PolymorphicOption((byte)2, typeof(Cat))]
            public ImmutableList<IAnimal> AnimalList { get; set; } = ImmutableList<IAnimal>.Empty;

            [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte))]
            [PolymorphicOption((byte)1, typeof(Dog))]
            [PolymorphicOption((byte)2, typeof(Cat))]
            public ImmutableHashSet<IAnimal> AnimalSet { get; set; } = ImmutableHashSet<IAnimal>.Empty;

            [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte))]
            [PolymorphicOption((byte)1, typeof(Dog))]
            [PolymorphicOption((byte)2, typeof(Cat))]
            public ImmutableSortedSet<ComparableAnimal> SortedAnimals { get; set; } = ImmutableSortedSet<ComparableAnimal>.Empty;
        }

        public interface IAnimal { }

        public interface ComparableAnimal : IAnimal, System.IComparable<ComparableAnimal> { }

        [GenerateSerializer]
        public partial class Dog : ComparableAnimal
        {
            public int CompareTo(ComparableAnimal? other) => 0;
        }

        [GenerateSerializer]
        public partial class Cat : ComparableAnimal
        {
            public int CompareTo(ComparableAnimal? other) => 0;
        }
        """;

        var compilation = CreateCompilation(AddDefaultUsings(source));
        var runResult = GetRunResult(compilation, out var finalCompilation);

        Assert.DoesNotContain(runResult.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        using var ms = new MemoryStream();
        var emitResult = finalCompilation.Emit(ms);
        Assert.True(emitResult.Success, $"Compilation failed with errors: {string.Join(", ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void UnlimitedConcreteCollections_ShouldCompile()
    {
        const string source = """
        using System.Collections.ObjectModel;

        namespace FourSer.Tests.Custom.Collections;

        [GenerateSerializer]
        public partial class UnlimitedQueuePacket
        {
            [SerializeCollection(Unlimited = true)]
            public Queue<int> Items { get; set; } = new();
        }

        [GenerateSerializer]
        public partial class UnlimitedStackPacket
        {
            [SerializeCollection(Unlimited = true)]
            public Stack<int> Items { get; set; } = new();
        }

        [GenerateSerializer]
        public partial class UnlimitedHashSetPacket
        {
            [SerializeCollection(Unlimited = true)]
            public HashSet<int> Items { get; set; } = new();
        }

        [GenerateSerializer]
        public partial class UnlimitedCollectionPacket
        {
            [SerializeCollection(Unlimited = true)]
            public Collection<int> Items { get; set; } = new();
        }

        [GenerateSerializer]
        public partial class UnlimitedObservableCollectionPacket
        {
            [SerializeCollection(Unlimited = true)]
            public ObservableCollection<int> Items { get; set; } = new();
        }
        """;

        var compilation = CreateCompilation(AddDefaultUsings(source));
        var runResult = GetRunResult(compilation, out var finalCompilation);

        Assert.DoesNotContain(runResult.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        using var ms = new MemoryStream();
        var emitResult = finalCompilation.Emit(ms);
        Assert.True(emitResult.Success, $"Compilation failed with errors: {string.Join(", ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void PolymorphicArrayCollections_ShouldCompile()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Polymorphism;

        [GenerateSerializer]
        public partial class IndividualTypeIdArrayPacket
        {
            [SerializeCollection(PolymorphicMode = PolymorphicMode.IndividualTypeIds, TypeIdType = typeof(byte))]
            [PolymorphicOption((byte)1, typeof(Dog))]
            [PolymorphicOption((byte)2, typeof(Cat))]
            public IAnimal[] Animals { get; set; } = Array.Empty<IAnimal>();
        }

        [GenerateSerializer]
        public partial class SingleTypeIdArrayPacket
        {
            [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId, TypeIdType = typeof(byte))]
            [PolymorphicOption((byte)1, typeof(Dog))]
            [PolymorphicOption((byte)2, typeof(Cat))]
            public IAnimal[] Animals { get; set; } = Array.Empty<IAnimal>();
        }

        public interface IAnimal { }

        [GenerateSerializer]
        public partial class Dog : IAnimal { }

        [GenerateSerializer]
        public partial class Cat : IAnimal { }
        """;

        var compilation = CreateCompilation(AddDefaultUsings(source));
        var runResult = GetRunResult(compilation, out var finalCompilation);

        Assert.DoesNotContain(runResult.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        using var ms = new MemoryStream();
        var emitResult = finalCompilation.Emit(ms);
        Assert.True(emitResult.Success, $"Compilation failed with errors: {string.Join(", ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CountedByteEnumerableSerialization_ShouldMaterializeAtMostOnce()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Collections;

        [GenerateSerializer]
        public partial class ByteEnumerablePacket
        {
            [SerializeCollection]
            public IEnumerable<byte>? Data { get; set; } = Array.Empty<byte>();
        }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "ByteEnumerablePacket");

        Assert.Contains("var data = SpanReader.ReadBytes(ref buffer, (int)dataCount);", generatedCode);
        Assert.Contains("var data = StreamReader.ReadBytes(stream, (int)dataCount);", generatedCode);
        Assert.Contains("SpanWriter.WriteBytes(ref data, dataCollection);", generatedCode);
        Assert.Contains("StreamWriter.WriteBytes(stream, dataCollection);", generatedCode);
        Assert.Contains("global::System.Linq.Enumerable.ToArray(obj.Data)", generatedCode);
        Assert.Contains("SpanWriter.WriteBytes(ref data, dataSequence);", generatedCode);
        Assert.Contains("StreamWriter.WriteBytes(stream, dataSequence);", generatedCode);
        Assert.DoesNotContain("var data = new System.Collections.Generic.List<byte>(dataCount);", generatedCode);
        Assert.DoesNotContain("data.Add(SpanReader.ReadByte(ref buffer));", generatedCode);
        Assert.DoesNotContain("data.Add(StreamReader.ReadByte(stream));", generatedCode);
    }

    [Fact]
    public void MemoryOwnerDeserialization_ShouldDisposeRentedOwnerOnFailure()
    {
        var generatedCode = GenerateSerializerSource(ReadSource("MemoryOwner"), "Parent");

        Assert.Contains("var dataOwner = MemoryPool<byte>.Shared.Rent", generatedCode);
        Assert.Contains("catch", generatedCode);
        Assert.Contains("dataOwner.Dispose();", generatedCode);
        Assert.Contains("throw;", generatedCode);
        Assert.Contains("data = dataOwner;", generatedCode);
    }

    [Fact]
    public void ParameterlessConstructor_ShouldPreserveSourceInitializers()
    {
        const string source = """
        using System.Collections.Generic;
        using System.Collections.Immutable;

        namespace FourSer.Tests.Custom.Collections;

        [GenerateSerializer]
        public partial class ImmutableArrayPacket
        {
            [SerializeCollection]
            public List<int> Items { get; set; } = new();

            [SerializeCollection]
            public int[] Numbers { get; set; } = System.Array.Empty<int>();

            [SerializeCollection]
            public ImmutableArray<int> Values { get; set; } = ImmutableArray<int>.Empty;
        }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "ImmutableArrayPacket");
        const string constructorSignature = "public ImmutableArrayPacket()";
        var constructorStart = generatedCode.IndexOf(constructorSignature, StringComparison.Ordinal);
        var deserializeStart = generatedCode.IndexOf("public static ImmutableArrayPacket Deserialize", StringComparison.Ordinal);
        var constructorBody = generatedCode.Substring(constructorStart, deserializeStart - constructorStart);

        Assert.DoesNotContain("this.Items =", constructorBody);
        Assert.DoesNotContain("this.Numbers =", constructorBody);
        Assert.DoesNotContain("this.Values =", constructorBody);
    }

    [Fact]
    public void StackCollections_ShouldDeserializeThroughStagingCollection()
    {
        const string source = """
        using System.Collections.Generic;

        namespace FourSer.Tests.Custom.Collections;

        [GenerateSerializer]
        public partial class StackPacket
        {
            [SerializeCollection]
            public Stack<uint> Values { get; set; } = new();
        }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "StackPacket");

        Assert.Contains("var valuesStaging = new System.Collections.Generic.List<uint>(valuesCount);", generatedCode);
        Assert.Contains("valuesStaging.Add(SpanReader.ReadUInt32(ref buffer));", generatedCode);
        Assert.Contains("var values = new System.Collections.Generic.Stack<uint>(global::System.Linq.Enumerable.Reverse(valuesStaging));", generatedCode);
    }

    [Fact]
    public void NarrowCountCollections_ShouldUseCheckedWrites()
    {
        const string source = """
        using System.Collections.Generic;

        namespace FourSer.Tests.Custom.Collections;

        [GenerateSerializer]
        public partial class NarrowCountPacket
        {
            [SerializeCollection(CountType = typeof(byte))]
            public HashSet<string> SmallSet { get; set; } = new();

            [SerializeCollection(CountType = typeof(ushort))]
            public Queue<int> MediumQueue { get; set; } = new();
        }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "NarrowCountPacket");

        Assert.Contains("checked((byte)(obj.SmallSet.Count))", generatedCode);
        Assert.Contains("checked((ushort)(obj.MediumQueue.Count))", generatedCode);
    }

    [Fact]
    public void NarrowCountCollectionSizing_ShouldNotEmitNoOpCheckedAssignments()
    {
        const string source = """
        using System.Collections.Generic;

        namespace FourSer.Tests.Custom.Collections;

        [GenerateSerializer]
        public partial class NarrowCountSizingPacket
        {
            [SerializeCollection(CountType = typeof(byte))]
            public List<Cat> Cats { get; set; } = new();
        }

        [GenerateSerializer]
        public partial class Cat
        {
            public int Id { get; set; }
        }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "NarrowCountSizingPacket");

        Assert.DoesNotContain("_ = checked(", generatedCode);
        Assert.Contains(
            """
                    size += sizeof(byte); // Count size for Cats
                    if (obj.Cats is not null)
                    {
                        byte catsValidatedCount = 0;
            """,
            generatedCode);
    }

    [Fact]
    public void ReferenceTypeListSerialization_ShouldNotPrevalidateWithSeparateEnumeration()
    {
        const string source = """
        using System.Collections.Generic;

        namespace FourSer.Tests.Custom.Collections;

        [GenerateSerializer]
        public partial class ListPacket
        {
            [SerializeCollection(CountType = typeof(byte))]
            public List<Cat> Cats { get; set; } = new();
        }

        [GenerateSerializer]
        public partial class Cat
        {
            public int Id { get; set; }
        }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "ListPacket");

        Assert.DoesNotContain("foreach (var item in obj.Cats)", generatedCode);
        Assert.Contains("if (obj.Cats[i] is null)", generatedCode);
    }

    [Fact]
    public void DefaultOptimizationLevel_ShouldFuseStreamStringWrites()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Options;

        [GenerateSerializer]
        public partial class StringPacket
        {
            public string Name { get; set; } = string.Empty;
        }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "StringPacket");

        Assert.Contains("System.Text.Encoding.UTF8.GetByteCount(obj.Name)", generatedCode);
        Assert.DoesNotContain("StreamWriter.WriteString(stream, obj.Name);", generatedCode);
    }

    [Fact]
    public void OptimizationLevelOff_ShouldDisableStringFusion()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Options;

        [GenerateSerializer]
        public partial class StringPacket
        {
            public string Name { get; set; } = string.Empty;
        }
        """;

        var generatedCode = GenerateSerializerSource(
            AddDefaultUsings(source),
            "StringPacket",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["build_property.FourSerOptimizationLevel"] = "Off",
            });

        Assert.Contains("StreamWriter.WriteString(stream, obj.Name);", generatedCode);
        Assert.DoesNotContain("System.Text.Encoding.UTF8.GetByteCount(obj.Name)", generatedCode);
    }

    [Fact]
    public void OptimizationLevelOff_ShouldDisableScalarBatching()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Options;

        [GenerateSerializer]
        public partial class BatchPacket
        {
            public int First { get; set; }
            public int Second { get; set; }
        }
        """;

        var generatedCode = GenerateSerializerSource(
            AddDefaultUsings(source),
            "BatchPacket",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["build_property.FourSerOptimizationLevel"] = "Off",
            });

        Assert.Contains("SpanWriter.WriteInt32(ref data, (int)(obj.First));", generatedCode);
        Assert.Contains("SpanWriter.WriteInt32(ref data, (int)(obj.Second));", generatedCode);
        Assert.DoesNotContain("BinaryPrimitives.WriteInt32LittleEndian", generatedCode);
    }

    [Fact]
    public void ConservativeOptimization_ShouldBatchAdjacentScalars()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Options;

        [GenerateSerializer]
        public partial class BatchPacket
        {
            public int First { get; set; }
            public int Second { get; set; }
        }
        """;

        var generatedCode = GenerateSerializerSource(
            AddDefaultUsings(source),
            "BatchPacket",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["build_property.FourSerOptimizationLevel"] = "Conservative",
            });

        Assert.Contains("BinaryPrimitives.WriteInt32LittleEndian", generatedCode);
        Assert.DoesNotContain("SpanWriter.WriteInt32(ref data, (int)(obj.First));", generatedCode);
    }

    [Fact]
    public void PolymorphicEnumerableSerialization_ShouldDisposeEnumerators()
    {
        const string source = """
        namespace FourSer.Tests.Custom.Polymorphism;

        [GenerateSerializer]
        public partial class Inventory
        {
            [SerializeCollection(PolymorphicMode = PolymorphicMode.SingleTypeId)]
            [PolymorphicOption((byte)1, typeof(Dog))]
            [PolymorphicOption((byte)2, typeof(Cat))]
            public IEnumerable<IAnimal> Animals { get; set; } = Array.Empty<IAnimal>();
        }

        public interface IAnimal { }

        [GenerateSerializer]
        public partial class Dog : IAnimal { }

        [GenerateSerializer]
        public partial class Cat : IAnimal { }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "Inventory");

        Assert.Contains("using var animalsEnumerator = ((global::System.Collections.Generic.IEnumerable<", generatedCode);
    }

    [Fact]
    public void MultipleNonNullableEnumerableCollections_ShouldUsePrefixedEndPositionLocals()
    {
        const string source = """
        #nullable enable

        namespace FourSer.Tests.Custom.Collections;

        [GenerateSerializer]
        public partial class EnumerablePacket
        {
            [SerializeCollection]
            public IEnumerable<int> FirstValues { get; set; } = Array.Empty<int>();

            [SerializeCollection]
            public IEnumerable<int> SecondValues { get; set; } = Array.Empty<int>();
        }
        """;

        var generatedCode = GenerateSerializerSource(AddDefaultUsings(source), "EnumerablePacket");

        Assert.Contains("var firstValuesEndPosition = stream.Position;", generatedCode);
        Assert.Contains("var secondValuesEndPosition = stream.Position;", generatedCode);
        Assert.DoesNotContain("var endPosition = stream.Position;", generatedCode);
    }

    [Fact]
    public void InvalidNestedCollection_ShouldReportDiagnosticAndSkipGeneration()
    {
        var source = ReadSource("InvalidNestedCollection");

        var syntaxTrees = s_contractsSource.Select(s => CSharpSyntaxTree.ParseText(s)).ToList();
        syntaxTrees.AddRange(s_extensionsSource.Select(s => CSharpSyntaxTree.ParseText(s)));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));

        var compilation = CSharpCompilation.Create
        (
            "TestProject",
            syntaxTrees,
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
        );

        var generator = new SerializerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        var runResult = driver.GetRunResult();

        Assert.Contains(runResult.Diagnostics, d => d.Id == "FSG0002" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(runResult.Results.Single().GeneratedSources, s => s.HintName.Contains("ContainerPacket"));
    }
    
    /// <summary>
    /// This test verifies that the source code produced by the generator compiles successfully.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void GeneratedSource_ShouldCompile(string testCaseName)
    {
        // Arrange
        var source = ReadSource(testCaseName);

        var syntaxTrees = s_contractsSource.Select(s => CSharpSyntaxTree.ParseText(s)).ToList();
        syntaxTrees.AddRange(s_extensionsSource.Select(s => CSharpSyntaxTree.ParseText(s)));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));

        var compilation = CSharpCompilation.Create(
            assemblyName: $"{testCaseName}.TestAssembly",
            syntaxTrees: syntaxTrees,
            references: Basic.Reference.Assemblies.Net90.References.All,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
        );

        var generator = new SerializerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        
        // Act
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();
        
        // Add the generated syntax trees to the compilation
        var finalCompilation = compilation.AddSyntaxTrees(runResult.GeneratedTrees);
        
        // Attempt to emit the final assembly
        using var ms = new MemoryStream();
        var emitResult = finalCompilation.Emit(ms);

        // Assert
        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToList();
            
            // Fail the test with a descriptive message if compilation fails
            Assert.True(emitResult.Success, $"Compilation failed with errors: \n{string.Join("\n", errors)}");
        }
    }

    private static string ReadSource(string testCaseName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"FourSer.Tests.GeneratorTestCases.{testCaseName}.input.cs";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();

        // source should not be null or empty
        if (string.IsNullOrEmpty(source))
        {
            throw new InvalidOperationException($"Resource '{resourceName}' not found or is empty.");
        }
        
        return AddDefaultUsings(source);
    }

    private static string AddDefaultUsings(string source)
    {
        var sb = new System.Text.StringBuilder(source);

        var requiredUsings = new[]
        {
            "using System;",
            "using System.Collections.Concurrent;",
            "using System.Collections.Generic;",
            "using System.Collections.Immutable;",
            "using FourSer.Consumer.Extensions;",
            "using FourSer.Contracts;"
        };

        foreach (var usingStatement in requiredUsings)
        {
            if (!source.Contains(usingStatement, StringComparison.OrdinalIgnoreCase))
            {
                sb.Insert(0, $"{usingStatement}\n");
            }
        }

        if (source.Length != sb.Length)
        {
            source = sb.ToString();
        }

        return source;
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTrees = s_contractsSource.Select(s => CSharpSyntaxTree.ParseText(s)).ToList();
        syntaxTrees.AddRange(s_extensionsSource.Select(s => CSharpSyntaxTree.ParseText(s)));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));

        return CSharpCompilation.Create
        (
            "TestProject",
            syntaxTrees,
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
        );
    }

    private static string GenerateSerializerSource(
        string source,
        string? hintNameContains = null,
        IReadOnlyDictionary<string, string>? globalOptions = null)
    {
        var compilation = CreateCompilation(source);
        var result = GetRunResult(compilation, out _, globalOptions);

        var sources = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Where(g => g.HintName.EndsWith("g.cs", StringComparison.Ordinal));

        if (!string.IsNullOrEmpty(hintNameContains))
        {
            sources = sources.Where(g => g.HintName.Contains(hintNameContains, StringComparison.Ordinal));
        }

        return string.Join("\n\n", sources.Select(g => g.SourceText.ToString()));
    }

    private static GeneratorDriverRunResult GetRunResult(
        CSharpCompilation compilation,
        out CSharpCompilation finalCompilation,
        IReadOnlyDictionary<string, string>? globalOptions = null)
    {
        var generator = new SerializerGenerator().AsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(
            generators: new ISourceGenerator[] { generator },
            parseOptions: compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions,
            optionsProvider: globalOptions is null ? null : new TestAnalyzerConfigOptionsProvider(globalOptions));

        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();
        finalCompilation = compilation.AddSyntaxTrees(runResult.GeneratedTrees);
        return runResult;
    }
}
