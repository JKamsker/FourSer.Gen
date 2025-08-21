using FourSer.Analyzers.PolymorphicOption;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace FourSer.Analyzers.Test.AnalyzerTests.PolymorphicOption;

public class PolymorphicOptionIdAnalyzerTests
{
    private const string AttributesSource = @"
using System;
using System.Collections.Generic;

namespace FourSer.Contracts
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class PolymorphicOptionAttribute : Attribute
    {
        public PolymorphicOptionAttribute(int id, Type type) { }
        public PolymorphicOptionAttribute(byte id, Type type) { }
    }
}";

    [Fact]
    public async Task DuplicateIds_ReportsDiagnostic()
    {
        var testCode = @"
using System;
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [PolymorphicOption(10, typeof(int))]
    [PolymorphicOption({|FSG3000:10|}, typeof(string))]
    public object A { get; set; }
}";
        await new CSharpAnalyzerTest<PolymorphicOptionIdAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { AttributesSource, testCode } },
        }.RunAsync();
    }

    [Fact]
    public async Task MixedIdTypes_ReportsDiagnostic()
    {
        var testCode = @"
using System;
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [PolymorphicOption(10, typeof(int))]
    [PolymorphicOption({|FSG3001:(byte)20|}, typeof(string))]
    public object A { get; set; }
}";
        await new CSharpAnalyzerTest<PolymorphicOptionIdAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { AttributesSource, testCode } },
        }.RunAsync();
    }

    [Fact]
    public async Task ValidUsage_NoDiagnostic()
    {
        var testCode = @"
using System;
using FourSer.Contracts;
using System.Collections.Generic;

public class MyData
{
    [PolymorphicOption(10, typeof(int))]
    [PolymorphicOption(20, typeof(string))]
    public object A { get; set; }
}";
        await new CSharpAnalyzerTest<PolymorphicOptionIdAnalyzer, DefaultVerifier>
        {
            TestState = { Sources = { AttributesSource, testCode } },
        }.RunAsync();
    }
}