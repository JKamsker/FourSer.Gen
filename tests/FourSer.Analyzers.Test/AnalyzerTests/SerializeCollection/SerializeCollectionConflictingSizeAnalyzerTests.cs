using FourSer.Analyzers.SerializeCollection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.SerializeCollection
{
    public class SerializeCollectionConflictingSizeAnalyzerTests
    {
        private const string AttributesSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializeCollectionAttribute : Attribute
    {
        public bool Unlimited { get; set; }
        public int CountSize { get; set; }
        public string CountSizeReference { get; set; }
    }
}";

        [Fact]
        public async Task UnlimitedWithCountSize_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(Unlimited = true, {|FSG1001:CountSize = 10|})]
    public List<int> A { get; set; }
}";
            await new CSharpAnalyzerTest<SerializeCollectionConflictingSizeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }

        [Fact]
        public async Task UnlimitedWithCountSizeReference_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(Unlimited = true, {|FSG1003:CountSizeReference = ""Size""|})]
    public List<int> A { get; set; }
    public int Size { get; set; }
}";
            await new CSharpAnalyzerTest<SerializeCollectionConflictingSizeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }

        [Fact]
        public async Task CountSizeWithCountSizeReference_ReportsDiagnostic()
        {
            var testCode = @"
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [SerializeCollection(CountSize = 10, {|FSG1002:CountSizeReference = ""Size""|})]
    public List<int> A { get; set; }
    public int Size { get; set; }
}";
            await new CSharpAnalyzerTest<SerializeCollectionConflictingSizeAnalyzer, DefaultVerifier>
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
    [SerializeCollection(CountSize = 10)]
    public List<int> A { get; set; }

    [SerializeCollection(CountSizeReference = ""Size"")]
    public List<int> B { get; set; }
    public int Size { get; set; }

    [SerializeCollection(Unlimited = true)]
    public List<int> C { get; set; }
}";
            await new CSharpAnalyzerTest<SerializeCollectionConflictingSizeAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { AttributesSource, testCode } },
            }.RunAsync();
        }
    }
}
