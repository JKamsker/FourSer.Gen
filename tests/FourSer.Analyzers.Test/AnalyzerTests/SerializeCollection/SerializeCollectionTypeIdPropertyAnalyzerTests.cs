using System.Threading.Tasks;
using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection
{
    public class SerializeCollectionTypeIdPropertyAnalyzerTests
    {
        private const string AttributesSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public string TypeIdProperty { get; set; }
    }
}";

        [Fact]
        public async Task NotFound_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection({|FSG1007:TypeIdProperty = ""NonExistent""|})]
    public List<int> A { get; set; }
}";
            await new CSharpAnalyzerTest<SerializeCollectionTypeIdPropertyAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }

        [Fact]
        public async Task WrongType_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection({|#0:TypeIdProperty = ""TypeId""|})]
    public List<int> A { get; set; }
    public float TypeId { get; set; }
}";

            var expected1 = new DiagnosticResult(SerializeCollectionTypeIdPropertyAnalyzer.WrongTypeRule).WithLocation(0).WithArguments("TypeId");
            var expected2 = new DiagnosticResult(SerializeCollectionTypeIdPropertyAnalyzer.DeclaredAfterRule).WithLocation(0).WithArguments("TypeId");

            await new CSharpAnalyzerTest<SerializeCollectionTypeIdPropertyAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { AttributesSource, testCode },
                    ExpectedDiagnostics = { expected1, expected2 }
                },
            }.RunAsync();
        }

        [Fact]
        public async Task DeclaredAfter_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection({|FSG1009:TypeIdProperty = ""TypeId""|})]
    public List<int> A { get; set; }
    public int TypeId { get; set; }
}";
            await new CSharpAnalyzerTest<SerializeCollectionTypeIdPropertyAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }

        [Fact]
        public async Task ValidUsage_NoDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    public int TypeId { get; set; }
    [SerializeCollection(TypeIdProperty = ""TypeId"")]
    public List<int> A { get; set; }
}";
            await new CSharpAnalyzerTest<SerializeCollectionTypeIdPropertyAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }
    }
}
