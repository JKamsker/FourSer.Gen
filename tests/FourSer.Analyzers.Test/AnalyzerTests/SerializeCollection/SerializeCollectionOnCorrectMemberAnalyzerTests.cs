using System.Threading.Tasks;
using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection
{
    public class SerializeCollectionOnCorrectMemberAnalyzerTests
    {
        private const string AttributesSource = @"
namespace FourSer.Contracts
{
    [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public class SerializeCollectionAttribute : System.Attribute { }

    public enum PolymorphicMode { None, SingleTypeId, IndividualTypeIds }
}";

        [Fact]
        public async Task OnNonIEnumerable_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection]
    public int A { get; set; }
}";
            var expected = new DiagnosticResult(SerializeCollectionOnCorrectMemberAnalyzer.Rule).WithLocation("/0/Test1.cs", 7, 6);
            await new CSharpAnalyzerTest<SerializeCollectionOnCorrectMemberAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { AttributesSource, testCode },
                    ExpectedDiagnostics = { expected },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task OnIEnumerable_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection]
    public IEnumerable<int> A { get; set; }
}";
            await new CSharpAnalyzerTest<SerializeCollectionOnCorrectMemberAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { AttributesSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task OnListOfT_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection]
    public List<int> A { get; set; }
}";
            await new CSharpAnalyzerTest<SerializeCollectionOnCorrectMemberAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { AttributesSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }

        [Fact]
        public async Task OnArray_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;

public class MyData
{
    [SerializeCollection]
    public int[] A { get; set; }
}";
            await new CSharpAnalyzerTest<SerializeCollectionOnCorrectMemberAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { AttributesSource, testCode },
                },
                ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            }.RunAsync();
        }
    }
}
